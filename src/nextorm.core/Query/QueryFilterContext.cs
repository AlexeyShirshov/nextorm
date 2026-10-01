using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Host object that lets a global query filter read the current <see cref="IDataContext"/> through a
/// member access instead of a bare constant. The filter's context parameter is substituted with
/// <see cref="Context"/>; because the host is reached through a member access, its identity is not part
/// of the plan or expression-cache key, so two contexts share one cached plan while each execution
/// still reads its own context value.
/// </summary>
internal sealed class QueryFilterContext
{
    /// <summary>Creates a host that serves filter reads from <paramref name="context"/>.</summary>
    /// <param name="context">The context whose per-query values the filter reads.</param>
    public QueryFilterContext(IDataContext context) => Context = context;

    /// <summary>The context the filter reads its per-query values from.</summary>
    public IDataContext Context { get; }
}

/// <summary>
/// Parameterizes the owner-getter/member subtree of a query filter imported from EF Core. Such a filter
/// reads its live owning <c>DbContext</c> as
/// <c>(TContext)&lt;owner-getter&gt;(QueryFilterContext.Context)</c>: the context access is swapped for
/// an <see cref="IDataContext"/> parameter, an accessor taking the executing context is compiled once
/// and cached under a host-free key, and it is invoked with the live host context on every render. The
/// EF owner instance and the value it produces therefore never enter a process-wide cache, the metadata
/// or the plan key.
/// </summary>
/// <remarks>
/// Only the exact owner-getter method the bridge registers through <see cref="RegisterOwnerGetter"/> is
/// recognised; a native filter that passes the context to some other static helper keeps its own path.
/// The accessor is invoked once per capture, so a null leaf value binds null (the predicate stays), while
/// a missing owner or a null intermediate member fails closed with a descriptive error instead of a raw
/// <see cref="NullReferenceException"/>.
/// </remarks>
internal static class QueryFilterContextAccessor
{
    private static MethodInfo? _ownerGetterMethod;

    /// <summary>
    /// Declares the exact owner-getter method of the imported filter (for example the EF Core bridge's
    /// <c>EfCoreFilterBinding.GetOwner</c>). Core never references the bridge type: the bridge passes its
    /// method identity in, and only that assembly/type/method/signature is treated as an owner getter.
    /// </summary>
    /// <param name="method">The static owner-getter method emitted by the bridge.</param>
    internal static void RegisterOwnerGetter(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        _ownerGetterMethod = method;
    }

    /// <summary>
    /// Returns whether <paramref name="node"/> calls exactly the registered owner-getter method. The
    /// argument shape is checked separately by the finder; this is the identity check that keeps a
    /// native static helper from being mistaken for the bridge getter.
    /// </summary>
    /// <param name="node">The method call to test.</param>
    internal static bool IsOwnerGetter(MethodCallExpression node)
        => _ownerGetterMethod is not null && node.Method == _ownerGetterMethod;

    /// <summary>
    /// Detects the owner-getter subtree in <paramref name="node"/>. When present, compiles/uses a
    /// host-free accessor, adds the value read from the live host as a query parameter and emits its
    /// placeholder. Returns <see langword="false"/> (and leaves <paramref name="node"/> untouched) for a
    /// native context filter, which has no owner-getter call and keeps its existing translation.
    /// </summary>
    /// <param name="visitor">The visitor rendering (or extracting parameters from) the condition.</param>
    /// <param name="node">The member access to translate.</param>
    public static bool TryTranslate(BaseExpressionVisitor visitor, MemberExpression node)
    {
        if (!OwnerGetterFinder.TryFind(node, out var contextAccess, out var accessorLambda))
            return false;

        var accessor = GetOrCompile(accessorLambda);

        if (contextAccess.Expression is not ConstantExpression { Value: QueryFilterContext host })
            return false;

        var parameterName = visitor.Options.ParameterNamePrefix + BuildSourceName(node);
        object? value;
        try
        {
            value = accessor(host.Context);
        }
        catch (NullReferenceException ex)
        {
            // A bound owner whose intermediate member is null (for example CurrentTenant is null) would
            // otherwise propagate a raw NullReferenceException out of the SQL build. Fail closed with a
            // message that names the owner read and the bridge requirement.
            throw new InvalidOperationException(
                "The EF Core query filter could not evaluate its owner member because a null owner value was " +
                "reached before the member chain completed. The filter was imported from an EF Core model and " +
                "reads the live owning DbContext; ensure the bridged DbContext instance exposes every member " +
                "the filter dereferences.",
                ex);
        }

        visitor.TryAddCapturedParameter(parameterName, value, node);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Dialect.MakeParam(parameterName));

        return true;
    }

    /// <summary>
    /// Builds the stable, host-free parameter identity of the owner read. The name is derived purely from
    /// the structural member/method chain (never from the owner instance or the value), so two distinct
    /// chains that end in the same leaf get distinct names while an identical chain reuses the same name
    /// (and the same captured source).
    /// </summary>
    /// <param name="node">The owner-getter/member subtree being parameterized.</param>
    internal static string BuildSourceName(MemberExpression node)
    {
        var parts = new List<string>();

        for (Expression? current = node; current is not null;)
        {
            current = current switch
            {
                MemberExpression member => Append(parts, member.Member.Name, member.Expression),
                MethodCallExpression call => Append(parts, call.Method.Name, null),
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary => unary.Operand,
                _ => null,
            };
        }

        parts.Reverse();
        return string.Join('_', parts);

        static Expression? Append(List<string> parts, string name, Expression? next)
        {
            parts.Add(name);
            return next;
        }
    }

    /// <summary>
    /// Rejects a filter body in which two distinct owner-getter member chains collapse to the same
    /// source name (for example <c>A.Get().Id</c> and <c>B.Get().Id</c>, where the walk truncates at the
    /// <c>Get</c> call). The generated SQL would name one placeholder for both reads and fail at
    /// parameter binding; detecting it here fails the query during preparation instead.
    /// </summary>
    /// <param name="body">The filter body, after its context parameter was substituted with the host access.</param>
    /// <exception cref="InvalidOperationException">Two distinct owner-getter chains share one source name.</exception>
    internal static void ValidateDistinctIdentities(Expression body)
    {
        if (_ownerGetterMethod is null)
            return;

        new IdentityCollector().Visit(body);
    }

    private sealed class IdentityCollector : ExpressionVisitor
    {
        private readonly Dictionary<string, string> _byName = new(StringComparer.Ordinal);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (!OwnerGetterFinder.TryFind(node, out _, out _))
                return base.VisitMember(node);

            var name = BuildSourceName(node);
            var text = node.ToString();
            if (_byName.TryGetValue(name, out var existing))
            {
                if (!string.Equals(existing, text, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"The EF Core query filter binds two distinct owner-getter member chains to the same parameter name '{name}': " +
                        $"'{existing}' and '{text}'. The generated SQL would reference one placeholder for both reads, so the query " +
                        "is rejected before execution. Rename one of the members or split the predicate so the chains produce distinct parameter names.");
            }
            else
            {
                _byName[name] = text;
            }

            // The owner-getter/member subtree is parameterised as a whole; do not descend into it.
            return node;
        }
    }

    private static Func<IDataContext, object> GetOrCompile(LambdaExpression accessor)
    {
        // The key is the canonical host-free accessor lambda text: the lambda takes only the executing
        // IDataContext and references no owner instance, so its structural text holds no live context.
        // Keying on the raw expression through ExpressionKey rooted the context (the key carried the
        // query command's plan comparer, hence the live IDataContext) process-wide; the string key is
        // host-free and equality is by value, so structurally identical accessors from independent
        // contexts still share one compiled delegate; the entry (and key) is released on Clear().
        var key = accessor.ToString();
        if (DataContextCache.QueryFilterContextAccessors.TryGetValue(key, out var cached))
            return cached;

        var compiled = (Func<IDataContext, object>)accessor.Compile();
        DataContextCache.QueryFilterContextAccessors[key] = compiled;
        return compiled;
    }

    // Finds the exact owner-getter call (for example EfCoreFilterBinding.GetOwner(context)) whose
    // argument is the QueryFilterContext.Context access. The method identity is registered by the
    // bridge; no other static helper over the context matches, so a native context read keeps its path.
    private sealed class OwnerGetterFinder : ExpressionVisitor
    {
        private MemberExpression? _contextAccess;

        private OwnerGetterFinder()
        {
        }

        public static bool TryFind(MemberExpression node, out MemberExpression contextAccess, out LambdaExpression accessorLambda)
        {
            var finder = new OwnerGetterFinder();
            finder.Visit(node);

            if (finder._contextAccess is null)
            {
                contextAccess = null!;
                accessorLambda = null!;
                return false;
            }

            contextAccess = finder._contextAccess;
            var parameter = Expression.Parameter(typeof(IDataContext), "context");
            var body = new ContextAccessReplacer(parameter).Visit(node)
                ?? throw new InvalidOperationException("Re-parameterizing the query-filter owner getter produced a null body.");
            accessorLambda = Expression.Lambda<Func<IDataContext, object>>(Expression.Convert(body, typeof(object)), parameter);
            return true;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsOwnerGetter(node)
                && node.Arguments.Count == 1
                && TryGetContextAccess(node.Arguments[0], out var access))
            {
                _contextAccess = access;
                return node;
            }

            return base.VisitMethodCall(node);
        }

        private static bool TryGetContextAccess(Expression expression, out MemberExpression access)
        {
            while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
                expression = unary.Operand;

            if (expression is MemberExpression member
                && member.Member.DeclaringType == typeof(QueryFilterContext)
                && member.Member.Name == nameof(QueryFilterContext.Context)
                && member.Expression is ConstantExpression { Value: QueryFilterContext })
            {
                access = member;
                return true;
            }

            access = null!;
            return false;
        }
    }

    // Swaps QueryFilterContext.Context for the accessor's IDataContext parameter, so the compiled
    // accessor is independent of the host instance (and therefore of the executing context).
    private sealed class ContextAccessReplacer(ParameterExpression parameter) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.DeclaringType == typeof(QueryFilterContext)
                && node.Member.Name == nameof(QueryFilterContext.Context)
                && node.Expression is ConstantExpression { Value: QueryFilterContext })
                return parameter;

            return base.VisitMember(node);
        }
    }
}

/// <summary>
/// Owner-free, per-context record of the global query filters a bridge-bound context imported into the
/// process-wide metadata. When the metadata is later dropped (by <see cref="DataContextCache.Clear"/>
/// or a sliding-expiration eviction), the expectation lets the engine refuse to run unfiltered instead
/// of silently losing the filter. It lives outside <see cref="DataContextCache"/>, which is precisely
/// why Clear does not erase it.
/// </summary>
/// <remarks>
/// The registry is keyed by the executing context through a <see cref="ConditionalWeakTable{TKey,TValue}"/>,
/// so it keeps neither the context nor any owner alive, and it stores only entity <see cref="Type"/>s,
/// the expected filter keys and the bridge-supplied diagnostic. Core never names the EF bridge type:
/// the diagnostic text is supplied by the bridge and core only rethrows it.
/// </remarks>
internal static class QueryFilterExpectations
{
    private static readonly ConditionalWeakTable<IDataContext, ExpectedFilters> Registry = new();
    private static int _registeredExpectationCount;

    /// <summary>Number of successful expectation registrations, exposed for tests.</summary>
    internal static int RegisteredExpectationCount => Volatile.Read(ref _registeredExpectationCount);

    /// <summary>
    /// Records that <paramref name="context"/> imported the given entity filters from the bridge. An
    /// empty expectation list registers nothing. <paramref name="diagnostic"/> is the message thrown
    /// when a later resolution cannot find the expected filters.
    /// </summary>
    /// <param name="context">The successfully created bridge context.</param>
    /// <param name="expectations">The imported entity types and their filter keys.</param>
    /// <param name="diagnostic">The bridge-supplied fail-closed message.</param>
    internal static void Register(
        IDataContext context,
        IReadOnlyList<(Type EntityType, IReadOnlyList<string> Keys)> expectations,
        string diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (expectations.Count == 0)
            return;

        Registry.GetOrCreateValue(context).Add(expectations, diagnostic);
        Interlocked.Increment(ref _registeredExpectationCount);
    }

    /// <summary>Returns whether <paramref name="context"/> expects an imported filter for <paramref name="entityType"/>.</summary>
    /// <param name="context">The executing context, or <see langword="null"/>.</param>
    /// <param name="entityType">The entity type to test.</param>
    internal static bool HasExpectation(IDataContext? context, Type entityType)
        => context is not null
            && Registry.TryGetValue(context, out var entry)
            && entry.Has(entityType);

    /// <summary>
    /// Throws the bridge-supplied fail-closed diagnostic when <paramref name="context"/> expects imported
    /// filters for <paramref name="entityType"/> but <paramref name="filters"/> is missing any of their
    /// keys. A non-bridge context (no expectations) is untouched.
    /// </summary>
    /// <param name="context">The executing context, or <see langword="null"/> when unknown.</param>
    /// <param name="entityType">The entity type whose resolved filters were read.</param>
    /// <param name="filters">The filters of the resolved metadata.</param>
    /// <exception cref="InvalidOperationException">An expected imported filter is missing.</exception>
    internal static void EnsureFiltersPresent(IDataContext? context, Type entityType, IReadOnlyList<IQueryFilterMetadata> filters)
    {
        if (context is null
            || !Registry.TryGetValue(context, out var entry)
            || !entry.TryGet(entityType, out var expected))
            return;

        for (var i = 0; i < expected.Keys.Length; i++)
        {
            if (!Contains(filters, expected.Keys[i]))
                throw new InvalidOperationException(expected.Diagnostic);
        }
    }

    /// <summary>
    /// Verifies, for every entity type <paramref name="context"/> imported a filter for, that the
    /// process-wide metadata still carries all expected filter keys. This is the render-time fail-closed
    /// funnel: a previously prepared <see cref="QueryCommand"/> skips re-preparation after
    /// <see cref="DataContextCache.Clear"/> or a sliding eviction, so the per-resolution checks in
    /// <c>ResolveMetadata</c>/<c>GetFilters</c> never run; this check refuses to render the unfiltered
    /// statement instead. A context with no expectations (every non-bridge context) is untouched.
    /// </summary>
    /// <param name="context">The executing context, or <see langword="null"/> when unknown.</param>
    /// <exception cref="InvalidOperationException">An expected imported filter is missing from the metadata.</exception>
    internal static void EnsureExpectedFiltersPresent(IDataContext? context)
    {
        if (context is null || Volatile.Read(ref _registeredExpectationCount) == 0)
            return;

        if (!Registry.TryGetValue(context, out var entry))
            return;

        entry.EnsurePresent();
    }

    private static bool Contains(IReadOnlyList<IQueryFilterMetadata> filters, string key)
    {
        for (var i = 0; i < filters.Count; i++)
        {
            if (string.Equals(filters[i].Key, key, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private sealed class ExpectedFilters
    {
        private readonly Dictionary<Type, ExpectedEntity> _byEntity = new();

        public void Add(IReadOnlyList<(Type EntityType, IReadOnlyList<string> Keys)> expectations, string diagnostic)
        {
            lock (_byEntity)
            {
                foreach (var (entityType, keys) in expectations)
                {
                    var copied = new string[keys.Count];
                    for (var i = 0; i < keys.Count; i++)
                        copied[i] = keys[i];

                    _byEntity[entityType] = new ExpectedEntity(copied, diagnostic);
                }
            }
        }

        public bool Has(Type entityType)
        {
            lock (_byEntity)
                return _byEntity.ContainsKey(entityType);
        }

        public bool TryGet(Type entityType, out ExpectedEntity expected)
        {
            lock (_byEntity)
                return _byEntity.TryGetValue(entityType, out expected!);
        }

        // Fails closed when the process-wide metadata no longer carries an expected imported filter:
        // either the entity mapping itself is gone, or the resolved mapping lacks one of its keys.
        public void EnsurePresent()
        {
            lock (_byEntity)
            {
                foreach (var (entityType, expected) in _byEntity)
                {
                    if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata)
                        || !HasAll(metadata.Filters, expected.Keys))
                        throw new InvalidOperationException(expected.Diagnostic);
                }
            }
        }

        private static bool HasAll(IReadOnlyList<IQueryFilterMetadata> filters, string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (!Contains(filters, keys[i]))
                    return false;
            }

            return true;
        }
    }

    private sealed record ExpectedEntity(string[] Keys, string Diagnostic);
}
