using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// The reference-navigation normalization pass (#148-B D3): scans a command's lambdas for
/// navigation-member accesses rooted at the query source parameter, injects one <c>LEFT JOIN</c> per
/// unique declared reference path and rewrites each access to the joined alias.
/// </summary>
/// <remarks>
/// <para>
/// Detection and hop identities come from <see cref="NavigationPathResolver"/> over the process-wide
/// configured, CLR-type-keyed metadata (<see cref="DataContextExtensions.ResolveMetadata(IDataContext?, Type)"/>);
/// the pass never writes, seeds or clears a metadata cache.
/// </para>
/// <para>
/// The rewrite is idempotent: once a navigation access is replaced by a joined parameter, a later
/// scan no longer recognizes it (its root is not the source parameter), so re-preparing a command does
/// not duplicate joins.
/// </para>
/// <para>
/// Occurrence identity (#148-B R2.2): each injected join carries its joined parameter on
/// <see cref="JoinExpression.SourceParameter"/>, and the columns provider binds aliases by that
/// parameter's reference identity, never by CLR type alone. The same path (root + navigation member
/// sequence, within one command's lexical scope) is reused; a self-reference, a second reference path
/// to the same type and a navigation to a type already exposed by a user join each get their own
/// occurrence and their own alias.
/// </para>
/// <para>
/// Fail-closed: collection navigations (one-to-many / many-to-many) as reference hops throw
/// <see cref="NotSupportedException"/> instead of emitting ambiguous or wrong SQL. Whole-reference
/// materialization, InMemory lowering and collection terminals are later units.
/// </para>
/// </remarks>
internal static class NavigationExpansion
{
    /// <summary>
    /// Expands reference navigations in <paramref name="cmd"/> using <paramref name="srcType"/> as the
    /// main source type. Called at the very start of preparation, before FROM/JOIN/columns/WHERE.
    /// </summary>
    internal static void Expand(QueryCommand cmd, Type srcType)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(srcType);

        // An untyped raw source (TableAlias) has no mapped entity to navigate from.
        if (srcType == typeof(TableAlias))
            return;

        // #148-B D6: the in-memory provider cannot materialize the SQL LEFT JOINs this pass injects.
        // It resolves declared navigations to metadata-based correlated subqueries at
        // expression-compilation time instead (CorrelatedQueryExpressionVisitor ->
        // InMemoryCorrelatedSubqueryRewriter). The collection terminals and the adapter are left
        // untouched here either way, so only the reference rewrite differs.
        if (cmd.DataContext is InMemoryDataContext)
            return;

        // Iteration 14 proposal 3: a root whose entity metadata is already known and declares no
        // relationship cannot produce a navigation path, so building the expansion state, rewriting every
        // lambda and allocating the join registries would be pure overhead. Only the side-effect-free
        // cache entry is consulted; an absent/unknown type falls through to the ordinary pass below,
        // which resolves (and may auto-build) metadata exactly as before, so the pass can never miss a
        // navigation below a subquery or join.
        if (DataContextCache.Metadata.TryGetValue(srcType, out var knownMetadata)
            && !string.IsNullOrEmpty(knownMetadata.TableName)
            && knownMetadata.Relationships.Count == 0)
        {
            return;
        }

        var state = new ExpansionState(cmd, srcType);

        var projection = Rewrite(state, cmd.ProjectionExpression);
        var condition = Rewrite(state, cmd.Condition as LambdaExpression);
        var having = Rewrite(state, cmd.Having);
        var group = Rewrite(state, cmd.GroupBy);
        var preWhere = Rewrite(state, cmd.PreWhere);
        var sorting = RewriteSorting(state, cmd.Sorting);

        if (!state.HasNavigation)
            return;

        // The joined parameters and their navigation paths are needed later: the projection-nullability
        // check (A7) and the provider outer-join null settings (ClickHouse join_use_nulls) both run off
        // this map. Set before the lambdas/joins are installed so a preparation that throws mid-way does
        // not leave a half-annotated command.
        cmd.NavigationPaths = state.NavigationPaths;

        cmd.ApplyNavigationExpansion(projection, condition, having, group, preWhere, sorting, state.JoinExpressions);
    }

    /// <summary>
    /// Fails closed when a projection materializes an unlifted non-nullable value scalar read through a
    /// reference navigation (#148-B A7): an absent principal yields SQL <c>NULL</c>, which the non-nullable
    /// result type cannot represent, so the silent column-default (or a raw reader error) is replaced with
    /// a diagnostic that names the navigation path and the result type. A nullable lift
    /// (<c>(int?)nav.Age</c>), a coalesce (<c>... ?? 0</c>) and a bool predicate projection are permitted.
    /// </summary>
    internal static void ValidateProjectionNullability(QueryCommand cmd, SelectExpression[]? selectList)
    {
        var paths = cmd.NavigationPaths;
        if (paths is not { Count: > 0 } || selectList is null)
            return;

        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            if (column.ProjectionItem is not null
                || column.Nullable
                || column.Expression is null
                || !column.PropertyType.IsValueType
                // A bool projection is a null-compensated predicate (SQL NULL -> false), so a navigation
                // scalar underneath it is not materialized as the scalar itself.
                || column.PropertyType == typeof(bool))
                continue;

            var visitor = new NavigationNullabilityVisitor(paths);
            visitor.Visit(column.Expression);
            if (visitor.Path is null)
                continue;

            throw new QueryPreparationException(
                $"The navigation scalar '{visitor.Path}.{visitor.Member}' is projected as the non-nullable type " +
                $"'{column.PropertyType}', but an absent principal yields SQL NULL, which cannot be stored in that type. " +
                $"Project it as a nullable value (for example '(int?)navigation.{visitor.Member}') or provide an explicit " +
                $"default (for example 'navigation.{visitor.Member} ?? 0').");
        }
    }

    /// <summary>
    /// Injects the dialect's query-local outer-join null settings for a reference-navigation query
    /// (#148-B A4/D1-Q3). Only the joined command is touched (a fresh list, never the builder's or a
    /// sibling's), the injection is idempotent and an explicit user setting that conflicts with a
    /// required value is rejected before execution.
    /// </summary>
    internal static void ApplyProviderOuterJoinNullSettings(QueryCommand cmd)
    {
        if (cmd.NavigationPaths is not { Count: > 0 })
            return;

        var required = (cmd.DataContext as DataContext)?.Dialect.OuterJoinNullSettings;
        if (required is not { Count: > 0 })
            return;

        List<KeyValuePair<string, string>>? merged = null;
        for (var i = 0; i < required.Count; i++)
        {
            var requirement = required[i];
            var current = FindSetting(cmd.Settings, requirement.Key);

            if (current is { } existing)
            {
                if (!string.Equals(existing.Value, requirement.Value, StringComparison.OrdinalIgnoreCase))
                    throw new QueryPreparationException(
                        $"The query sets the provider setting '{requirement.Key} = {existing.Value}', which conflicts with " +
                        $"'{requirement.Key} = {requirement.Value}' required by an implicit reference navigation. Remove the " +
                        $"explicit setting or set it to '{requirement.Value}'.");

                continue;
            }

            merged ??= new List<KeyValuePair<string, string>>(cmd.Settings ?? []);
            merged.Add(requirement);
        }

        if (merged is not null)
            cmd.Settings = merged;
    }

    /// <summary>
    /// Renders a resolved navigation chain for diagnostics, e.g. <c>NavChild.Parent.Profile</c>. Shared
    /// with the in-memory reference lowering so both providers name a rejected scalar the same way.
    /// </summary>
    internal static string DisplayPath(IReadOnlyList<ResolvedNavigationHop> hops)
    {
        ArgumentNullException.ThrowIfNull(hops);

        var builder = new StringBuilder();
        for (var i = 0; i < hops.Count; i++)
        {
            if (i == 0)
                builder.Append(hops[i].DeclaringType.Name).Append('.');

            builder.Append(hops[i].Navigation.Name);
            if (i < hops.Count - 1)
                builder.Append('.');
        }

        return builder.ToString();
    }

    private static KeyValuePair<string, string>? FindSetting(IReadOnlyList<KeyValuePair<string, string>>? settings, string key)
    {
        if (settings is null)
            return null;

        for (var i = 0; i < settings.Count; i++)
        {
            if (string.Equals(settings[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return settings[i];
        }

        return null;
    }

    /// <summary>
    /// Walks a projected expression and reports the first navigation scalar that is materialized without
    /// a nullable lift or coalesce. A nullable <c>Convert</c> and the left side of a <c>Coalesce</c> are
    /// treated as compensated, so the supported lifted and coalesced forms are not rejected.
    /// </summary>
    private sealed class NavigationNullabilityVisitor(IReadOnlyDictionary<ParameterExpression, string> paths) : ExpressionVisitor
    {
        private int _compensated;

        internal string? Path { get; private set; }

        internal string? Member { get; private set; }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert && Nullable.GetUnderlyingType(node.Type) is not null)
            {
                _compensated++;
                Visit(node.Operand);
                _compensated--;
                return node;
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Coalesce)
            {
                _compensated++;
                Visit(node.Left);
                _compensated--;
                Visit(node.Right);
                return node;
            }

            return base.VisitBinary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (Path is null
                && _compensated == 0
                && node.Expression is ParameterExpression parameter
                && paths.TryGetValue(parameter, out var path)
                && node.Type.IsValueType
                && Nullable.GetUnderlyingType(node.Type) is null)
            {
                Path = path;
                Member = node.Member.Name;
                return node;
            }

            return base.VisitMember(node);
        }
    }

    private static LambdaExpression? Rewrite(ExpansionState state, LambdaExpression? lambda)
        => lambda is null ? null : state.RewriteLambda(lambda);

    private static Sorting[]? RewriteSorting(ExpansionState state, Sorting[]? sorting)
    {
        if (sorting is null)
            return null;

        Sorting[]? result = null;
        for (var i = 0; i < sorting.Length; i++)
        {
            var sort = sorting[i];
            if (sort.SortExpression is not LambdaExpression { Parameters.Count: 1 } lambda)
                continue;

            var rewritten = state.RewriteLambda(lambda);
            if (ReferenceEquals(rewritten, lambda))
                continue;

            result ??= (Sorting[])sorting.Clone();
            var replacement = new Sorting(rewritten) { Direction = sort.Direction };
            result[i] = replacement;
        }

        return result;
    }

    /// <summary>Per-preparation expansion state: the join registry and the collision set.</summary>
    private sealed class ExpansionState
    {
        private readonly QueryCommand _command;
        private readonly Type _srcType;
        // D4: the join registry, its ordered entries and the parameter->path map are created on the
        // first write. A query whose lambdas navigate nothing (or whose navigations all fail to
        // resolve) never pays for three empty containers.
        private Dictionary<string, JoinEntry>? _byKey;
        private List<JoinEntry>? _joins;
        private Dictionary<ParameterExpression, string>? _paths;

        internal ExpansionState(QueryCommand command, Type srcType)
        {
            _command = command;
            _srcType = srcType;
            ScopeIdentity = command;
            // #148-B R2.2: same-typed sources (a self-reference, a second reference path to the same
            // type, or a user join of that type) are no longer rejected here. Alias resolution binds each
            // injected join by its occurrence parameter (JoinExpression.SourceParameter), so distinct
            // occurrences stay distinct.
        }

        internal object ScopeIdentity { get; }

        internal bool HasNavigation => _joins is { Count: > 0 };

        internal JoinExpression[] JoinExpressions
        {
            get
            {
                if (_joins is not { Count: > 0 } joins)
                    return [];

                var result = new JoinExpression[joins.Count];
                for (var i = 0; i < joins.Count; i++)
                    result[i] = joins[i].Join;
                return result;
            }
        }

        /// <summary>Maps each joined parameter to the display form of the path it was created for.</summary>
        internal IReadOnlyDictionary<ParameterExpression, string>? NavigationPaths => _paths;

        /// <summary>Whether <paramref name="parameter"/> denotes a navigation join alias.</summary>
        internal bool IsJoinedParameter(ParameterExpression parameter)
        {
            if (_joins is not { } joins)
                return false;

            for (var i = 0; i < joins.Count; i++)
            {
                if (ReferenceEquals(joins[i].Parameter, parameter))
                    return true;
            }

            return false;
        }

        internal LambdaExpression RewriteLambda(LambdaExpression lambda)
        {
            if (lambda.Parameters.Count == 0)
                return lambda;

            var visitor = new RewriteVisitor(this, lambda.Parameters[0]);
            var rewritten = (LambdaExpression)visitor.Visit(lambda);
            return rewritten;
        }

        /// <summary>The joined parameter for the resolved path, creating the joins if needed.</summary>
        internal ParameterExpression GetOrCreate(ResolvedNavigationPath path)
            => GetOrCreate(path.Hops, path.Hops.Count, path.Hops);

        /// <summary>
        /// #148-B D-R3-5-SQL: expands only the declared reference prefix of a path whose final hop is a
        /// collection, returning the joined parameter of the last reference. A collection reached through
        /// a reference is then correlated off that joined alias, so an absent reference is a NULL key
        /// (LEFT JOIN) and the collection is empty rather than matching a default-valued foreign key.
        /// </summary>
        internal ParameterExpression GetOrCreateReferencePrefix(ResolvedNavigationPath path)
        {
            // #148-B r3: ClickHouse cannot correlate a subquery on a joined source alias, so a
            // collection reached through a reference is rejected with a precise diagnostic instead of
            // emitting SQL the engine fails on (NOT_IMPLEMENTED: can't find correlated column).
            if ((_command.DataContext as DataContext)?.Dialect is { SupportsReferenceToCollectionNavigation: false })
                throw new NotSupportedException(
                    "A collection reached through a reference navigation is not supported by this provider: " +
                    "it cannot correlate a subquery on a joined source. Query the collection from its own " +
                    "source or use an explicit join.");

            var prefixCount = path.Hops.Count - 1;
            var prefix = new ResolvedNavigationHop[prefixCount];
            for (var i = 0; i < prefixCount; i++)
                prefix[i] = path.Hops[i];

            return GetOrCreate(prefix, prefixCount, prefix);
        }

        private ParameterExpression GetOrCreate(
            IReadOnlyList<ResolvedNavigationHop> hops,
            int hopCount,
            IReadOnlyList<ResolvedNavigationHop>? recordedPath)
        {
            JoinEntry? previous = null;
            var key = string.Empty;

            for (var i = 0; i < hopCount; i++)
            {
                var hop = hops[i];
                if (hop.IsCollection || hop.IsManyToMany || hop.Kind == RelationshipKind.ManyToMany)
                    throw new NotSupportedException(
                        $"The navigation '{hop.DeclaringType.Name}.{hop.Navigation.Name}' is a collection; " +
                        "implicit collection navigation is not supported yet (use an explicit join).");

                key = key.Length == 0 ? HopKey(hop) : key + ">" + HopKey(hop);

                if (_byKey is not null && _byKey.TryGetValue(key, out var existing))
                {
                    previous = existing;
                    continue;
                }

                // #148-B R2.2 occurrence identity: the left side of every non-first hop is the previous
                // hop's joined parameter (not a fresh same-typed parameter resolved by CLR type), so a
                // self-referential or same-typed chain binds each hop to its own occurrence. The first
                // hop keeps a declaring-type parameter, which resolves to the outer source.
                var leftParameter = previous is null
                    ? Expression.Parameter(hop.DeclaringType, "navigationLeft")
                    : previous.Parameter;
                var rightParameter = Expression.Parameter(hop.RelatedType, "navigation" + (_joins?.Count ?? 0));

                var (leftKey, rightKey) = hop.Direction == NavigationDirection.DependentToPrincipal
                    ? (hop.PrimaryLeg.ForeignKey.PropertyInfo, hop.PrimaryLeg.PrincipalKey.PropertyInfo)
                    : (hop.PrimaryLeg.PrincipalKey.PropertyInfo, hop.PrimaryLeg.ForeignKey.PropertyInfo);

                var condition = Expression.Lambda(
                    Expression.Equal(
                        Expression.Property(leftParameter, leftKey),
                        Expression.Property(rightParameter, rightKey)),
                    leftParameter,
                    rightParameter);

                var from = _command.DataContext?.GetFrom(hop.RelatedType, null)
                    ?? throw new NotSupportedException(
                        $"The navigation '{hop.DeclaringType.Name}.{hop.Navigation.Name}' cannot be expanded: " +
                        $"the source for '{hop.RelatedType.Name}' is not registered.");

                var entry = new JoinEntry(
                    new JoinExpression(condition, JoinType.Left)
                    {
                        EntityType = hop.RelatedType,
                        From = from,
                        SourceParameter = rightParameter,
                    },
                    rightParameter);

                (_byKey ??= new(StringComparer.Ordinal))[key] = entry;
                (_joins ??= []).Add(entry);
                previous = entry;
            }

            if (recordedPath is not null)
                (_paths ??= [])[previous!.Parameter] = DisplayPath(recordedPath);

            return previous!.Parameter;
        }

    /// <summary>
    /// Renders a resolved navigation chain for diagnostics, e.g. <c>NavChild.Parent.Profile</c>.
    /// </summary>
    private static string DisplayPath(IReadOnlyList<ResolvedNavigationHop> hops)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < hops.Count; i++)
            {
                if (i == 0)
                    builder.Append(hops[i].DeclaringType.Name).Append('.');

                builder.Append(hops[i].Navigation.Name);
                if (i < hops.Count - 1)
                    builder.Append('.');
            }

            return builder.ToString();
        }

        /// <summary>Builds the presence predicate for <c>nav == null</c> / <c>nav != null</c>.</summary>
        internal Expression BuildPresence(ResolvedNavigationPath path, bool isNull)
        {
            var parameter = GetOrCreate(path);
            var hop = path.FinalHop!;
            var keyProperty = JoinedKey(hop);
            var key = Expression.Property(parameter, keyProperty);

            var nullableType = key.Type.IsValueType && Nullable.GetUnderlyingType(key.Type) is null
                ? typeof(Nullable<>).MakeGenericType(key.Type)
                : key.Type;

            Expression left = key.Type == nullableType ? key : Expression.Convert(key, nullableType);
            var nullConstant = Expression.Constant(null, nullableType);

            return isNull ? Expression.Equal(left, nullConstant) : Expression.NotEqual(left, nullConstant);
        }

        private static PropertyInfo JoinedKey(ResolvedNavigationHop hop)
            => hop.Direction == NavigationDirection.DependentToPrincipal
                ? hop.PrimaryLeg.PrincipalKey.PropertyInfo
                : hop.PrimaryLeg.ForeignKey.PropertyInfo;

        private static string HopKey(ResolvedNavigationHop hop)
            => $"{hop.DeclaringType.FullName}.{hop.Navigation.Name}";
    }

    /// <summary>One injected navigation join and the parameter that denotes its right-hand source.</summary>
    private sealed class JoinEntry(JoinExpression join, ParameterExpression parameter)
    {
        internal JoinExpression Join { get; } = join;
        internal ParameterExpression Parameter { get; } = parameter;
    }

    /// <summary>
    /// Rewrites the navigation-member accesses of one lambda: a pure declared-reference chain becomes
    /// the joined parameter; a null comparison over such a chain becomes a principal/dependent key
    /// presence test.
    /// </summary>
    private sealed class RewriteVisitor(ExpansionState state, ParameterExpression root) : ExpressionVisitor
    {
        // D4: a supported collection terminal is not expanded here. It is lowered to a correlated
        // subquery by CorrelatedQueryExpressionVisitor once FROM/JOIN/columns are prepared, so this
        // pass only has to leave the whole call untouched instead of failing on the collection hop.
        private static readonly string[] CollectionTerminals = ["Any", "Count", "LongCount"];

        // #148-B D-R3-6-SQL: the supported reference-adapter terminals.
        private static readonly string[] AdapterTerminals = ["Any", "Count", "LongCount"];

        protected override Expression VisitMember(MemberExpression node)
        {
            // The Count property of a declared collection is a supported terminal too; leave it for the
            // correlated visitor instead of expanding (and rejecting) the collection hop. A collection
            // reached through a reference prefix is rewritten onto the prefix's joined alias first
            // (#148-B D-R3-5-SQL), so an absent reference yields an empty collection.
            if (node.Member.Name == "Count"
                && node.Expression is { } countReceiver
                && TryResolve(countReceiver, out var countPath)
                && countPath.FinalHop is { IsCollection: true })
            {
                if (countPath.Hops.Count > 1)
                {
                    var joined = state.GetOrCreateReferencePrefix(countPath);
                    var collection = Expression.Property(joined, countPath.FinalHop.Navigation);
                    return Expression.MakeMemberAccess(collection, node.Member);
                }

                return node;
            }

            if (TryResolve(node, out var path))
                return state.GetOrCreate(path);

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // #148-B D-R3-6-SQL: a reference-navigation AsEntityBuilder terminal is normalized to the
            // whole-reference access (the injected LEFT JOIN and its null semantics). The collection
            // adapter and the outside marker keep their existing handling.
            if (TryRewriteReferenceAdapterTerminal(node, out var adapterResult))
                return adapterResult;

            // D4 adapter: the whole call is lowered by the correlated visitor, which resolves the
            // navigation receiver itself.
            if (IsEntityBuilderAdapter(node))
                return node;

            if (TryGetCollectionTerminalReceiver(node, out var receiver) && TryResolve(receiver, out var path))
            {
                if (path.FinalHop is { IsCollection: true })
                {
                    // #148-B D-R3-5-SQL: a collection reached through a reference prefix is correlated
                    // off the prefix's joined alias; the absent reference is a NULL key (LEFT JOIN), so
                    // the collection is empty instead of matching a default-valued foreign key.
                    if (path.Hops.Count > 1)
                    {
                        var joined = state.GetOrCreateReferencePrefix(path);
                        return Expression.Call(node.Method, Expression.Property(joined, path.FinalHop.Navigation));
                    }

                    return node;
                }

                throw new NotSupportedException(
                    $"The navigation terminal '{node.Method.Name}' is only supported on a declared collection navigation; " +
                    $"'{receiver}' is a reference navigation.");
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// #148-B D-R3-6-SQL: normalizes <c>nav.AsEntityBuilder&lt;T&gt;().Any/Count/LongCount</c> on a
        /// declared reference navigation to the whole-reference presence (the same LEFT-JOIN null
        /// semantics as <c>nav != null</c>), returning 1/0 for the count forms. Returns
        /// <see langword="false"/> for the collection adapter and any shape the correlated visitor owns.
        /// </summary>
        private bool TryRewriteReferenceAdapterTerminal(MethodCallExpression node, out Expression result)
        {
            result = null!;

            if (Array.IndexOf(AdapterTerminals, node.Method.Name) < 0
                || node.Method.DeclaringType != typeof(EntityBuilderExtensions))
                return false;

            var builder = node.Object is { } instance
                ? instance
                : node.Arguments.Count > 0 ? TypeFacts.UnwrapConvert(node.Arguments[0]) : null;

            if (builder is not MethodCallExpression adapterCall
                || !IsEntityBuilderAdapter(adapterCall)
                || adapterCall.Arguments is not [Expression navigation]
                || !TryResolve(navigation, out var path)
                || path.FinalHop is not { IsCollection: false })
                return false;

            var presence = state.BuildPresence(path, isNull: false);
            result = node.Method.Name switch
            {
                "Any" => presence,
                // The scalar 1/0 is shaped through the declared CLR type so the renderer emits an
                // explicit cast: a dialect whose integer CASE result is inferred narrowly (ClickHouse
                // returns UInt8 for `case when ... then 1 else 0 end`) would otherwise hand the
                // Int32/Int64 reader a value it rejects.
                "Count" => Expression.Convert(
                    Expression.Condition(presence, Expression.Constant(1L), Expression.Constant(0L)), typeof(int)),
                _ => Expression.Convert(
                    Expression.Condition(presence, Expression.Constant(1), Expression.Constant(0)), typeof(long)),
            };
            return true;
        }

        private static bool IsEntityBuilderAdapter(MethodCallExpression node)
            => node.Method.DeclaringType == typeof(EntityBuilderExtensions)
               && node.Method.Name == nameof(EntityBuilderExtensions.AsEntityBuilder);

        private static bool TryGetCollectionTerminalReceiver(MethodCallExpression node, out Expression receiver)
        {
            receiver = null!;
            if (node.Method.DeclaringType != typeof(Enumerable))
                return false;

            if (Array.IndexOf(CollectionTerminals, node.Method.Name) < 0 || node.Arguments is not [Expression source])
                return false;

            receiver = source;
            return true;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                && TryGetNullComparison(node, out var path))
            {
                return state.BuildPresence(path, node.NodeType == ExpressionType.Equal);
            }

            var rewritten = base.VisitBinary(node);

            // #148-B R2.5 / A5: a navigation-derived value is NULL when the principal row is absent
            // (LEFT JOIN). SQL's `<>` on NULL is UNKNOWN, so `navigation.Name != 'x'` would silently
            // drop the absent rows; the design requires them to be included (`IS NULL OR <>`).
            if (rewritten is BinaryExpression { NodeType: ExpressionType.NotEqual } notEqual)
            {
                var navigationOperand = FindJoinedOperand(notEqual.Left) ?? FindJoinedOperand(notEqual.Right);
                if (navigationOperand is not null)
                    return CompensateAbsent(notEqual, navigationOperand);
            }

            return rewritten;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            // #148-B R2.5 / A5: `!(navigation.Name == 'x')` must include the absent rows exactly like
            // `navigation.Name != 'x'`. A presence check (`navigation == null`) is handled on the
            // binary path and must not be compensated again.
            if (node.NodeType == ExpressionType.Not
                && node.Operand is BinaryExpression { NodeType: ExpressionType.Equal } comparison
                && !TryGetNullComparison(comparison, out _))
            {
                var rewritten = (BinaryExpression)Visit(comparison);
                var negated = Expression.Not(rewritten);

                var navigationOperand = FindJoinedOperand(rewritten.Left) ?? FindJoinedOperand(rewritten.Right);
                return navigationOperand is null ? negated : CompensateAbsent(negated, navigationOperand);
            }

            return base.VisitUnary(node);
        }

        /// <summary>
        /// Returns the operand when it is a member access (possibly a lifted nullable convert) rooted at
        /// a navigation join alias, so a NULL check can be added for the absent principal.
        /// </summary>
        private Expression? FindJoinedOperand(Expression expression)
        {
            var unwrapped = TypeFacts.UnwrapConvert(expression);
            if (unwrapped is not MemberExpression member)
                return null;

            Expression? current = member;
            while (current is MemberExpression { Expression: { } inner })
                current = inner;

            return current is ParameterExpression parameter && state.IsJoinedParameter(parameter)
                ? unwrapped
                : null;
        }

        /// <summary>Builds <c>operand IS NULL OR expression</c> so an absent principal qualifies.</summary>
        private static Expression CompensateAbsent(Expression expression, Expression operand)
        {
            Expression nullable = operand;
            if (operand.Type.IsValueType && Nullable.GetUnderlyingType(operand.Type) is null)
                nullable = Expression.Convert(operand, typeof(Nullable<>).MakeGenericType(operand.Type));

            return Expression.OrElse(
                Expression.Equal(nullable, Expression.Constant(null, nullable.Type)),
                expression);
        }

        private bool TryGetNullComparison(BinaryExpression node, out ResolvedNavigationPath path)
        {
            if (node.Left is ConstantExpression { Value: null })
                return TryResolve(node.Right, out path);

            if (node.Right is ConstantExpression { Value: null })
                return TryResolve(node.Left, out path);

            path = null!;
            return false;
        }

        /// <summary>Resolves a pure declared-navigation member chain rooted at the lambda parameter.</summary>
        private bool TryResolve(Expression expression, out ResolvedNavigationPath path)
            => TryResolvePath(expression, root, state.ScopeIdentity, out path);
    }

    /// <summary>
    /// Resolves a declared-navigation member chain rooted at <paramref name="root"/> against the
    /// configured metadata, without expanding anything. Shared by the reference expansion and by the
    /// correlated-subquery lowering of collection terminals (D4). Returns <see langword="false"/> when
    /// the chain is not a pure navigation path, so a non-navigation member can fall through unchanged.
    /// </summary>
    internal static bool TryResolvePath(Expression expression, ParameterExpression root, object scopeIdentity, out ResolvedNavigationPath path)
    {
        path = null!;
        expression = TypeFacts.UnwrapConvert(expression);
        if (expression is not MemberExpression member)
            return false;

        // D4: validate the chain before allocating the member list. A non-property member or a chain
        // that is not rooted at the parameter is a negative path, so it must allocate nothing.
        if (!IsPureMemberChain(member, root, out var memberCount))
            return false;

        var members = new List<PropertyInfo>(memberCount);
        var node = member;
        while (true)
        {
            members.Add((PropertyInfo)node.Member);
            if (node.Expression is MemberExpression inner)
            {
                node = inner;
                continue;
            }

            break;
        }

        members.Reverse();

        var type = root.Type;
        for (var i = 0; i < members.Count; i++)
        {
            var relationship = FindRelationship(type, members[i]);
            if (relationship is null)
                return false;

            type = relationship.RelatedType;
        }

        Expression navigationExpression = root;
        for (var i = 0; i < members.Count; i++)
            navigationExpression = Expression.Property(navigationExpression, members[i]);

        var scope = new NavigationResolutionScope(
            scopeIdentity,
            [new NavigationSourceBinding(scopeIdentity, root, root, root.Type)]);

        path = NavigationPathResolver.Resolve(navigationExpression, scope);
        return true;
    }

    /// <summary>
    /// Walks a member chain, proving every step is a <see cref="PropertyInfo"/> access and that the
    /// chain is rooted at <paramref name="root"/>, without allocating. On success
    /// <paramref name="count"/> is the number of member accesses in the chain.
    /// </summary>
    private static bool IsPureMemberChain(MemberExpression member, ParameterExpression root, out int count)
    {
        count = 0;
        Expression? current = member;
        while (current is MemberExpression { Expression: { } inner } node)
        {
            if (node.Member is not PropertyInfo)
                return false;

            count++;
            current = inner;
        }

        return current is ParameterExpression parameter && ReferenceEquals(parameter, root);
    }

    private static IRelationshipMetadata? FindRelationship(Type entityType, PropertyInfo member)
    {
        var relationships = DataContextExtensions.ResolveMetadata(null, entityType).Relationships;
        for (var i = 0; i < relationships.Count; i++)
        {
            if (relationships[i].Navigation is { } navigation && navigation.Equals(member))
                return relationships[i];
        }

        return null;
    }
}
