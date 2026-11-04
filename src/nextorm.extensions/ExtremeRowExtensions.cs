using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>The direction of the extreme-row selection.</summary>
public enum ExtremeKind
{
    /// <summary>Keep the row(s) with the smallest value.</summary>
    Min,

    /// <summary>Keep the row(s) with the greatest value.</summary>
    Max
}

/// <summary>
/// The reflection handles the component emits into its structural expression tree, resolved once for
/// the process instead of re-scanned on every cold preparation. Every handle is a method the public
/// engine surface already exposes; nothing internal is opened.
/// </summary>
internal static class ExtremeRowReflection
{
    internal static readonly MethodInfo Asc = RequireMethod(typeof(CommonFunctions), "asc", BindingFlags.Public | BindingFlags.Instance);

    internal static readonly MethodInfo Desc = RequireMethod(typeof(CommonFunctions), "desc", BindingFlags.Public | BindingFlags.Instance);

    internal static readonly MethodInfo RowNumber = RequireMethod(typeof(CommonFunctions), "row_number", BindingFlags.Public | BindingFlags.Instance);

    internal static readonly MethodInfo Rank = RequireMethod(typeof(CommonFunctions), "rank", BindingFlags.Public | BindingFlags.Instance);

    internal static readonly MethodInfo ColumnDefinition =
        RequireMethod(typeof(SqlFunctions), "Column", BindingFlags.Public | BindingFlags.Static);

    internal static readonly MethodInfo ColumnOfInt = ColumnDefinition.MakeGenericMethod(typeof(int));

    internal static readonly MethodInfo Over = RequireMethod(
        typeof(WindowFunction<int>),
        "Over",
        new[] { typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame) });

    internal static readonly MethodInfo MinAggregate = RequireAggregate("min");

    internal static readonly MethodInfo MaxAggregate = RequireAggregate("max");

    /// <summary>
    /// Resolves a public engine member once for the process; a missing member is a version mismatch
    /// between this package and <c>nextorm.core</c>, reported at first use instead of a silent null.
    /// </summary>
    private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags)
        => type.GetMethod(name, flags)
            ?? throw new InvalidOperationException(
                $"The nextorm.core member {type.FullName}.{name} was not found; nextorm.extensions targets a different nextorm.core version.");

    /// <summary>Resolves a public engine member by exact parameter types, once for the process.</summary>
    private static MethodInfo RequireMethod(Type type, string name, Type[] parameterTypes)
        => type.GetMethod(name, parameterTypes)
            ?? throw new InvalidOperationException(
                $"The nextorm.core member {type.FullName}.{name}({string.Join(", ", parameterTypes.Select(t => t.Name))}) was not found; nextorm.extensions targets a different nextorm.core version.");

    /// <summary>
    /// Resolves an aggregate generic method definition of <see cref="CommonFunctions"/> once for the
    /// process; each value type is closed over the cached definition at build time.
    /// </summary>
    private static MethodInfo RequireAggregate(string name)
    {
        var methods = typeof(CommonFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (var i = 0; i < methods.Length; i++)
        {
            var candidate = methods[i];
            if (candidate.Name == name && candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == 1)
                return candidate;
        }

        throw new InvalidOperationException(
            $"The nextorm.core aggregate CommonFunctions.{name} was not found; nextorm.extensions targets a different nextorm.core version.");
    }
}

/// <summary>
/// Extension methods that attach the <c>nextorm.extensions</c> extreme-row component: they keep the
/// row(s) holding the smallest or greatest value, globally or per group, exactly like the built-in
/// <c>SelectWhereMin</c>/<c>SelectWhereMax</c> but built entirely on the public query-component layer.
/// </summary>
public static class ExtremeRowExtensions
{
    /// <summary>
    /// Keeps the row(s) with the smallest <paramref name="valueSelector"/> value and returns the
    /// builder so the query can continue.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <param name="source">The builder the component is attached to.</param>
    /// <param name="valueSelector">The value whose minimum is selected; a composite key is compared lexicographically.</param>
    /// <param name="ties">Whether to keep one row or every row tied on the extreme value.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole source.</param>
    /// <returns>The same query shape with the extreme-row component attached.</returns>
    public static EntityBuilder<TEntity> SelectWhereMinEx<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Min, valueSelector, ties, groupBy));

    /// <summary>
    /// Keeps the row(s) with the greatest <paramref name="valueSelector"/> value and returns the
    /// builder so the query can continue.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <param name="source">The builder the component is attached to.</param>
    /// <param name="valueSelector">The value whose maximum is selected; a composite key is compared lexicographically.</param>
    /// <param name="ties">Whether to keep one row or every row tied on the extreme value.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole source.</param>
    /// <returns>The same query shape with the extreme-row component attached.</returns>
    public static EntityBuilder<TEntity> SelectWhereMaxEx<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Max, valueSelector, ties, groupBy));

    /// <summary>
    /// Keeps the row(s) with the smallest <paramref name="valueSelector"/> value and applies
    /// <paramref name="projection"/> to each surviving row.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="source">The builder the component is attached to.</param>
    /// <param name="valueSelector">The value whose minimum is selected.</param>
    /// <param name="projection">The projection applied to each surviving row; it never participates in winner selection.</param>
    /// <param name="ties">Whether to keep one row or every row tied on the extreme value.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole source.</param>
    /// <returns>The projected query command.</returns>
    public static QueryCommand<TResult> SelectWhereMinEx<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Min, valueSelector, ties, groupBy))
            .Select(projection);
    }

    /// <summary>
    /// Keeps the row(s) with the greatest <paramref name="valueSelector"/> value and applies
    /// <paramref name="projection"/> to each surviving row.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <typeparam name="TValue">The value type compared for the extremum.</typeparam>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="source">The builder the component is attached to.</param>
    /// <param name="valueSelector">The value whose maximum is selected.</param>
    /// <param name="projection">The projection applied to each surviving row; it never participates in winner selection.</param>
    /// <param name="ties">Whether to keep one row or every row tied on the extreme value.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole source.</param>
    /// <returns>The projected query command.</returns>
    public static QueryCommand<TResult> SelectWhereMaxEx<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Max, valueSelector, ties, groupBy))
            .Select(projection);
    }
}

/// <summary>
/// The public query component behind <see cref="ExtremeRowExtensions.SelectWhereMinEx{TEntity, TValue}(EntityBuilder{TEntity}, Expression{Func{TEntity, TValue}}, ExtremeRowTies, Expression{Func{TEntity, object?}})"/>
/// and its siblings: an exclusive row-source reshape that keeps the extreme row(s). The extension
/// methods construct and attach it; a caller that needs the component directly passes
/// <see cref="ExtremeKind"/> explicitly.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type the selectors are written over.</typeparam>
public sealed class ExtremeRowComponent<TEntity> : IQueryComponent
{
    private readonly ExtremeKind _kind;
    private readonly LambdaExpression _valueSelector;
    private readonly ExtremeRowTies _ties;
    private readonly Expression<Func<TEntity, object?>>? _groupBy;

    /// <summary>
    /// Creates the component for one direction and one selection state.
    /// </summary>
    /// <param name="kind">Whether the component keeps the minimum or the maximum value.</param>
    /// <param name="valueSelector">The value selector; its single parameter must be typed <typeparamref name="TEntity"/>.</param>
    /// <param name="ties">Whether to keep one row or every tied row.</param>
    /// <param name="groupBy">Optional grouping key; <c>null</c> selects across the whole source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="valueSelector"/> is not a single-parameter lambda over <typeparamref name="TEntity"/>.</exception>
    public ExtremeRowComponent(
        ExtremeKind kind,
        LambdaExpression valueSelector,
        ExtremeRowTies ties,
        Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        if (valueSelector.Parameters.Count != 1 || valueSelector.Parameters[0].Type != typeof(TEntity))
            throw new ArgumentException(
                $"The value selector must be a single-parameter lambda over {typeof(TEntity).FullName}.",
                nameof(valueSelector));

        _kind = kind;
        _valueSelector = valueSelector;
        _ties = ties;
        _groupBy = groupBy;
    }

    /// <summary>The component slot: one stable slot per entity type, so a second extreme-row call replaces this component in place.</summary>
    public Type Slot => typeof(ExtremeRowComponent<TEntity>);

    /// <summary>The contribution kind: the component replaces the row source, so only one exclusive reshape may be present.</summary>
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;


    private static void ValidateCompatibility(QueryCommand cmd)
    {
        // The engine enforces the complete built-in rejection boundary for every exclusive row-source
        // reshape (QueryComponentCompatibility → ExtremeRowCompatibility), including the state the
        // public surface does not expose (prepared HAVING, LIMIT BY, ARRAY JOIN, DISTINCT ON,
        // TABLESAMPLE, temporal clauses, prepared array joins and source-level locks). The checks below
        // are the subset a consumer can reproduce itself; they keep the component honest when it is
        // used through a command the engine did not prepare.
        if (cmd.Ctes is { Count: > 0 }) throw NotCompatible("common table expressions (WITH)");
        if (cmd.Having is not null) throw NotCompatible("HAVING");
        if (cmd.Windows is { Count: > 0 }) throw NotCompatible("named windows");
        if (cmd.GroupingList is { Length: > 0 } || cmd.GroupBy is not null) throw NotCompatible("GROUP BY");
        if (cmd.Joins is { Length: > 0 }) throw NotCompatible("joins");
        if (cmd.PreWhere is not null) throw NotCompatible("PREWHERE");
        if (!cmd.Paging.IsEmpty) throw NotCompatible("paging");
        if (cmd.UnionQuery is not null) throw NotCompatible("a set operation");
        if (cmd.TableHints is { Count: > 0 }) throw NotCompatible("table hints");
        if (cmd.IndexHints is { Count: > 0 }) throw NotCompatible("index hints");
        if (cmd.TablesInScopeHints is { Count: > 0 }) throw NotCompatible("tables-in-scope hints");
        if (cmd.Hints is { Count: > 0 }) throw NotCompatible("query hints");
        if (cmd.Final) throw NotCompatible("FINAL");
        if (cmd.SampleRatio is not null) throw NotCompatible("SAMPLE");
        if (cmd.Settings is { Count: > 0 }) throw NotCompatible("SETTINGS");
        if (cmd.ForJsonClause is not null || cmd.ForXmlClause is not null) throw NotCompatible("FOR JSON/FOR XML");
    }

    /// <summary>
    /// Aggregate function names the engine lowers through <c>CommonFunctions</c> (the same set the
    /// in-memory evaluator folds). A component projects its own hidden rank column into the select list,
    /// so an aggregate in that list would be evaluated over the reshaped rows; it is rejected explicitly
    /// instead of being composed.
    /// </summary>
    private static readonly HashSet<string> AggregateFunctionNames = new(StringComparer.Ordinal)
    {
        "min", "max", "sum", "avg", "count", "count_big", "stdev", "stdevp", "var", "varp",
    };

    private static void EnsureNoAggregates(SelectExpression[]? selectList)
    {
        if (selectList is null) return;

        for (var i = 0; i < selectList.Length; i++)
        {
            if (selectList[i].Expression is { } expression && AggregateDetector.Contains(expression))
                throw NotCompatible("aggregates");
        }
    }

    private static BuildSqlCommandException NotCompatible(string modifier)
        => new($"SelectWhereMax/SelectWhereMin cannot be combined with {modifier}.");

    /// <summary>
    /// Validates the component against the command and returns the immutable descriptor that joins the
    /// plan key: the direction, the tie policy and both selectors, compared with the engine's expression
    /// comparer. Preparation is also where the in-memory provider and the unsupported modifiers are
    /// rejected.
    /// </summary>
    /// <param name="context">The command being prepared and the engine expression comparer.</param>
    /// <returns>The component's structural descriptor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The command targets the in-memory provider.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tie policy is not <see cref="ExtremeRowTies.One"/> or <see cref="ExtremeRowTies.All"/>.</exception>
    /// <exception cref="BuildSqlCommandException">The command carries a modifier the reshape cannot express.</exception>
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("SelectWhereMinEx/Max is not supported by the in-memory provider.");

        // The ties value is validated here rather than at construction: attaching a component must stay
        // as cheap as attaching the built-in clause (AC7 construction phase), while an unsupported
        // value is still rejected explicitly during preparation.
        if (_ties is not (ExtremeRowTies.One or ExtremeRowTies.All))
            throw new ArgumentOutOfRangeException(nameof(_ties), _ties, "Unknown ExtremeRowTies value.");

        ValidateCompatibility(context.Command);

        var comparer = context.ExpressionComparer;
        var discriminator = _kind == ExtremeKind.Min ? "Min" : "Max";
        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        hash.Add(discriminator);
        hash.Add(_ties);
        hash.Add(_valueSelector, comparer);
        if (_groupBy is not null)
            hash.Add(_groupBy, comparer);

        var captured = new CapturedState(_kind, _ties, _valueSelector, _groupBy, comparer);
        return new Descriptor(this, captured, hash.ToHashCode());
    }

    /// <summary>
    /// Builds the component's structural contribution into the command. Runs on a plan-cache miss only:
    /// <see cref="ExtremeRowTies.One"/> becomes a ranked derived source filtered on rank 1, a non-nullable
    /// group key over a non-floating value key under <see cref="ExtremeRowTies.All"/> becomes the
    /// aggregate+join lowering, and a nullable group key or a floating value key under
    /// <see cref="ExtremeRowTies.All"/> uses the rank window (no null-safe join predicate is needed, and
    /// the winner set follows the NaN-ranks-last ordering rather than a server aggregate).
    /// </summary>
    /// <param name="context">The command being built and the engine expression comparer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public void BuildQuery(IQueryComponentBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var entityParam = Expression.Parameter(entityType, "x");
        var valueComponents = DecomposeSelector(_valueSelector, entityParam);
        var groupComponents = _groupBy is null ? Array.Empty<Expression>() : DecomposeSelector(_groupBy, entityParam);

        // `All` is lowered to the aggregate+join shape, which joins every aggregate row back to the
        // source. A nullable group key needs the null-safe `a = b or (a is null and b is null)` join
        // predicate, and ClickHouse rejects a bare `IS NULL` residual in `JOIN ON` (#296) — a
        // restriction no public dialect capability advertises. Rather than name a dialect, choose the
        // shape by what the expression needs: a nullable group key uses the rank-window form, which
        // partitions on the group keys directly and never emits a null-safe join; every other `All`
        // shape keeps the aggregate+join lowering. A floating value key also keeps the rank-window
        // form: its aggregate and its ordering disagree about NaN on ClickHouse (the server returns
        // NaN as the aggregate maximum while ORDER BY ranks it last, the contract the rank window
        // implements), and the component is built before any dialect is known.
        if (_ties == ExtremeRowTies.All
            && !NeedsNullSafeGroupJoin(groupComponents)
            && !HasFloatingComponent(valueComponents))
        {
            BuildAllQuery(context, entityParam, valueComponents, groupComponents);
            return;
        }

        var rankAlias = MakeRankAlias(entityType);
        var inner = cmd.Clone();
        inner.Components = null;
        // The hidden rank column is appended to the inner select list. An aggregate there would be
        // evaluated over the reshaped rows instead of the projected ones, so it is refused explicitly.
        // The clone shares the select list instance, so one walk covers both.
        EnsureNoAggregates(cmd.SelectList);
        // The built-in portable lowering excludes rows with a NULL in any value key. Apply the same
        // filter inside the ranked derived source, before the window is evaluated.
        Expression? innerFilter = null;
        for (var i = 0; i < valueComponents.Length; i++)
        {
            var component = valueComponents[i];
            if (IsNullable(component.Type))
            {
                var notNull = Expression.NotEqual(component, Expression.Constant(null, component.Type));
                innerFilter = innerFilter is null ? notNull : Expression.AndAlso(innerFilter, notNull);
            }
        }
        var existingCondition = inner.GetComponentPreparedCondition();
        if (existingCondition is LambdaExpression conditionLambda && conditionLambda.Parameters.Count > 0)
        {
            var conditionBody = new ParameterReplacer(conditionLambda.Parameters[0], entityParam).Visit(conditionLambda.Body);
            innerFilter = innerFilter is null ? conditionBody : Expression.AndAlso(innerFilter, conditionBody);
        }
        else if (existingCondition is not null)
        {
            innerFilter = innerFilter is null ? existingCondition : Expression.AndAlso(innerFilter, existingCondition);
        }
        inner.SetComponentPreparedCondition(innerFilter is null ? null : Expression.Lambda(innerFilter, entityParam));

        var sqlProperty = Expression.Property(null, typeof(SqlFunctions), "Sql");
        var rankMethod = _ties == ExtremeRowTies.All
            ? ExtremeRowReflection.Rank
            : ExtremeRowReflection.RowNumber;
        var rankCall = Expression.Call(sqlProperty, rankMethod);

        var directionMethod = _kind == ExtremeKind.Max
            ? ExtremeRowReflection.Desc
            : ExtremeRowReflection.Asc;
        var orderCalls = new Expression[valueComponents.Length];
        for (var i = 0; i < valueComponents.Length; i++)
        {
            var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(valueComponents[i], typeof(object)));
            orderCalls[i] = Expression.Call(sqlProperty, directionMethod, Expression.Quote(lambda));
        }
        var orderArray = Expression.NewArrayInit(typeof(WindowOrder), orderCalls);

        Expression partitionArg;
        if (groupComponents.Length == 0)
        {
            partitionArg = Expression.Constant(null, typeof(Expression<Func<object?>>[]));
        }
        else
        {
            var partitionLambdas = new Expression[groupComponents.Length];
            for (var i = 0; i < groupComponents.Length; i++)
            {
                var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(groupComponents[i], typeof(object)));
                partitionLambdas[i] = Expression.Quote(lambda);
            }
            partitionArg = Expression.NewArrayInit(typeof(Expression<Func<object?>>), partitionLambdas);
        }

        var overMethod = ExtremeRowReflection.Over;
        var rankExpression = Expression.Call(
            rankCall,
            overMethod,
            partitionArg,
            orderArray,
            Expression.Constant(null, typeof(WindowFrame)));

        var rankSelect = new SelectExpression(typeof(int))
        {
            Index = inner.SelectList?.Length ?? 0,
            PropertyName = rankAlias,
            Expression = rankExpression
        };

        // Exact-size copy: the previous LINQ chain allocated an iterator plus an intermediate array on
        // every cold build, which is pure overhead on the prepare path the component already pays for.
        var sourceSelects = inner.SelectList;
        var innerSelects = sourceSelects is null
            ? new[] { rankSelect }
            : new SelectExpression[sourceSelects.Length + 1];
        sourceSelects?.CopyTo(innerSelects, 0);
        innerSelects[^1] = rankSelect;
        inner.SetComponentSelectList(innerSelects);
        inner.SetComponentSorting(null);

        // The engine hands this method a private clone of the live command (SqlBuilder.MakeSelect), so
        // the rebuilt outer statement is written in place: no second full command clone per build.
        cmd.From = new FromExpression(inner);

        var outerParam = Expression.Parameter(entityType, "o");
        var columnCall = Expression.Call(null, ExtremeRowReflection.ColumnOfInt, outerParam, Expression.Constant(rankAlias));
        var rankFilter = Expression.Lambda(Expression.Equal(columnCall, Expression.Constant(1)), outerParam);
        cmd.SetComponentPreparedCondition(rankFilter);
    }


    private static Expression[] DecomposeSelector(LambdaExpression selector, ParameterExpression entityParameter)
    {
        var body = StripBoxing(selector.Body);
        if (body is NewExpression { Arguments.Count: > 0 } ctor)
        {
            var result = new Expression[ctor.Arguments.Count];
            for (var i = 0; i < result.Length; i++)
                result[i] = new ParameterReplacer(selector.Parameters[0], entityParameter).Visit(ctor.Arguments[i])!;
            return result;
        }

        return [new ParameterReplacer(selector.Parameters[0], entityParameter).Visit(body)!];
    }


    private static bool IsNullable(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    /// <summary>
    /// True when joining the aggregate back to the source needs the null-safe predicate
    /// <c>a = b or (a is null and b is null)</c> for at least one group key. ClickHouse cannot render
    /// that predicate inside <c>JOIN ON</c> (#296); such a shape is lowered through the rank window
    /// instead, so the component never needs to know which dialect it is running on.
    /// </summary>
    private static bool NeedsNullSafeGroupJoin(Expression[] groupComponents)
    {
        for (var i = 0; i < groupComponents.Length; i++)
        {
            if (IsNullable(groupComponents[i].Type))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when at least one value component is a floating-point key (<c>float</c>/<c>double</c>,
    /// nullable or not). Such a key keeps the rank-window form of <see cref="ExtremeRowTies.All"/> so the
    /// winner set follows the ordering contract (NaN ranks last) instead of the server's aggregate
    /// comparison, which disagrees with it on ClickHouse.
    /// </summary>
    private static bool HasFloatingComponent(Expression[] valueComponents)
    {
        for (var i = 0; i < valueComponents.Length; i++)
        {
            var type = Nullable.GetUnderlyingType(valueComponents[i].Type) ?? valueComponents[i].Type;
            if (type == typeof(float) || type == typeof(double))
                return true;
        }

        return false;
    }

    private static Expression StripBoxing(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert, Type: var type } convert && type == typeof(object)
            ? convert.Operand
            : expression;

    private static string MakeRankAlias(Type entityType)
    {
        const string seed = "__nextorm_ext_rn";
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return seed;

        var name = seed;
        while (true)
        {
            var collides = false;
            foreach (var property in metadata.Properties)
            {
                if (string.Equals(property.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                {
                    collides = true;
                    break;
                }
            }
            if (!collides)
                return name;
            name += "_";
        }
    }

    private void BuildAllQuery(
        IQueryComponentBuildContext context,
        ParameterExpression xParam,
        Expression[] valueComponents,
        Expression[] groupComponents)
    {
        var cmd = context.Command;
        var ctx = cmd.DataContext ?? throw new InvalidOperationException("DataContext is required.");
        var entityType = typeof(TEntity);
        var valueCount = valueComponents.Length;
        var groupCount = groupComponents.Length;

        var valueStorages = new Expression[valueCount];
        var valueTypes = new Type[valueCount];
        for (var i = 0; i < valueCount; i++)
        {
            var (storage, type) = MakeValueStorage(valueComponents[i]);
            valueStorages[i] = storage;
            valueTypes[i] = type;
        }

        var preparedCondition = cmd.GetComponentPreparedCondition();
        LambdaExpression? userLambda = preparedCondition as LambdaExpression;
        if (userLambda is null && preparedCondition is not null)
            userLambda = Expression.Lambda(preparedCondition, xParam);
        // The condition belongs to the aggregated source only. Leaving it on the outer command would
        // render the same predicate a second time around the reshape, duplicating its parameters; the
        // rank-window path replaces the outer condition the same way.
        cmd.SetComponentPreparedCondition(null);

        Expression? notNull = null;
        for (var i = 0; i < valueCount; i++)
        {
            if (!IsNullable(valueTypes[i]))
                continue;
            var nullConst = Expression.Constant(null, valueTypes[i]);
            var cond = Expression.NotEqual(valueStorages[i], nullConst);
            notNull = notNull is null ? cond : Expression.AndAlso(notNull, cond);
        }

        QueryCommand? lastStage = null;
        for (var k = 1; k <= valueCount; k++)
        {
            var eParam = Expression.Parameter(entityType, "e");

            var selectCount = groupCount + k;
            var selects = new SelectExpression[selectCount];

            for (var i = 0; i < groupCount; i++)
            {
                var alias = GroupAliasFor(valueCount, k, i);
                var exprOverE = new ParameterReplacer(xParam, eParam).Visit(groupComponents[i])!;
                selects[i] = new SelectExpression(groupComponents[i].Type)
                {
                    Index = i,
                    PropertyName = alias,
                    Expression = exprOverE,

                };
            }

            for (var i = 0; i < k - 1; i++)
            {
                var alias = ValueAliasFor(valueCount, k, i + 1);
                var exprOverE = new ParameterReplacer(xParam, eParam).Visit(valueStorages[i])!;
                selects[groupCount + i] = new SelectExpression(valueTypes[i])
                {
                    Index = groupCount + i,
                    PropertyName = alias,
                    Expression = exprOverE,

                };
            }

            var aggAlias = AggregateAliasFor(valueCount, k);
            var aggValueOverE = new ParameterReplacer(xParam, eParam).Visit(valueStorages[k - 1])!;
            var aggCall = MakeAggregate(_kind, valueTypes[k - 1], aggValueOverE);
            selects[groupCount + k - 1] = new SelectExpression(valueTypes[k - 1])
            {
                Index = groupCount + k - 1,
                PropertyName = aggAlias,
                Expression = aggCall,
            };

            var groupExprs = new List<Expression>();
            for (var i = 0; i < groupCount; i++)
                groupExprs.Add(groupComponents[i]);
            for (var i = 0; i < k - 1; i++)
                groupExprs.Add(valueComponents[i]);

            LambdaExpression? groupLambda = null;
            if (groupExprs.Count > 0)
            {
                Expression body = groupExprs.Count == 1
                    ? groupExprs[0]
                    : BuildValueTuple(groupExprs);
                groupLambda = Expression.Lambda(body, xParam);
            }

            Expression? stageCombined = notNull;
            if (userLambda is not null)
            {
                var userOverX = userLambda.Parameters.Count > 0
                    ? new ParameterReplacer(userLambda.Parameters[0], xParam).Visit(userLambda.Body)!
                    : userLambda.Body;
                stageCombined = stageCombined is null ? userOverX : Expression.AndAlso(stageCombined, userOverX);
            }
            var stageConditionLambda = stageCombined is null ? null : Expression.Lambda(stageCombined, xParam);

            JoinExpression[]? joins = null;
            if (k > 1 && lastStage is not null)
            {
                var prevParam = Expression.Parameter(entityType, "p");
                Expression? joinBody = null;

                for (var i = 0; i < groupCount; i++)
                {
                    var left = groupComponents[i];
                    var right = MakeColumnCall(prevParam, groupComponents[i].Type, GroupAliasFor(valueCount, k - 1, i));
                    var cond = IsNullable(groupComponents[i].Type)
                        ? MakeNullSafeJoinEqual(left, right, groupComponents[i].Type)
                        : Expression.Equal(left, right);
                    joinBody = joinBody is null ? cond : Expression.AndAlso(joinBody, cond);
                }

                for (var i = 0; i < k - 1; i++)
                {
                    var left = valueStorages[i];
                    var right = MakeColumnCall(prevParam, valueTypes[i], ValueAliasFor(valueCount, k - 1, i + 1));
                    var cond = Expression.Equal(left, right);
                    joinBody = joinBody is null ? cond : Expression.AndAlso(joinBody, cond);
                }

                var joinLambda = Expression.Lambda(joinBody!, xParam, prevParam);
                var join = new JoinExpression(joinLambda, JoinType.Inner)
                {
                    From = new FromExpression(lastStage),
                    EntityType = entityType,
                };
                joins = new[] { join };
            }

            var stageCmd = ctx.CreateCommand<TEntity>(new QueryDefinition
            {
                SrcType = entityType,
                Condition = stageConditionLambda,
                Joins = joins,
                Group = groupLambda,
            });
            stageCmd.SetComponentSelectList(selects);
            stageCmd.PrepareCommand(false, CancellationToken.None);
            lastStage = stageCmd;
        }

        var outerX = Expression.Parameter(entityType, "o");
        var outerP = Expression.Parameter(entityType, "p");
        Expression? outerBody = null;

        for (var i = 0; i < groupCount; i++)
        {
            var left = new ParameterReplacer(xParam, outerX).Visit(groupComponents[i])!;
            var right = MakeColumnCall(outerP, groupComponents[i].Type, GroupAliasFor(valueCount, valueCount, i));
            var cond = IsNullable(groupComponents[i].Type)
                ? MakeNullSafeJoinEqual(left, right, groupComponents[i].Type)
                : Expression.Equal(left, right);
            outerBody = outerBody is null ? cond : Expression.AndAlso(outerBody, cond);
        }

        for (var i = 0; i < valueCount; i++)
        {
            var left = new ParameterReplacer(xParam, outerX).Visit(valueStorages[i])!;
            var alias = ValueAliasFor(valueCount, valueCount, i + 1);
            var right = MakeColumnCall(outerP, valueTypes[i], alias);
            var cond = Expression.Equal(left, right);
            outerBody = outerBody is null ? cond : Expression.AndAlso(outerBody, cond);
        }

        var outerJoinLambda = Expression.Lambda(outerBody!, outerX, outerP);
        var outerJoin = new JoinExpression(outerJoinLambda, JoinType.Inner)
        {
            From = new FromExpression(lastStage!),
            EntityType = entityType,
        };

        var innerCmd = ctx.CreateCommand<TEntity>(new QueryDefinition
        {
            SrcType = entityType,
            Joins = new[] { outerJoin },
        });
        innerCmd.PrepareCommand(false, CancellationToken.None);

        cmd.From = new FromExpression(innerCmd);
    }

    private static (Expression Expression, Type Type) MakeValueStorage(Expression raw)
    {
        var type = raw.Type;
        if (type == typeof(bool))
        {
            var test = Expression.Equal(raw, Expression.Constant(true, type));
            return (Expression.Condition(test, Expression.Constant(1), Expression.Constant(0)), typeof(int));
        }

        if (type != typeof(bool?))
            return (raw, type);

        var nullConstant = Expression.Constant(null, type);
        var nullableInt = typeof(int?);
        var isNull = Expression.Equal(raw, nullConstant);
        var isTrue = Expression.Equal(raw, Expression.Constant(true, type));
        var integer = Expression.Condition(isTrue, Expression.Constant(1), Expression.Constant(0));
        return (
            Expression.Condition(
                isNull, Expression.Constant(null, nullableInt), Expression.Convert(integer, nullableInt)),
            nullableInt);
    }

    private static Expression MakeNullSafeJoinEqual(Expression left, Expression right, Type type)
    {
        if (!IsNullable(type))
            return Expression.Equal(left, right);

        var nullConstant = Expression.Constant(null, type);
        return Expression.OrElse(
            Expression.Equal(left, right),
            Expression.AndAlso(
                Expression.Equal(left, nullConstant),
                Expression.Equal(right, nullConstant)));
    }

    private static Expression MakeAggregate(ExtremeKind kind, Type valueType, Expression valueExpression)
    {
        var genericDefinition = kind == ExtremeKind.Max
            ? ExtremeRowReflection.MaxAggregate
            : ExtremeRowReflection.MinAggregate;
        var mi = genericDefinition.MakeGenericMethod(valueType);
        var sqlProp = Expression.Property(null, typeof(SqlFunctions), "Sql");
        return Expression.Call(sqlProp, mi, valueExpression);
    }

    private static Expression MakeColumnCall(ParameterExpression parameter, Type type, string alias)
    {
        var method = ExtremeRowReflection.ColumnDefinition.MakeGenericMethod(type);
        return Expression.Call(null, method, parameter, Expression.Constant(alias));
    }

    private static string GroupAliasFor(int valueCount, int stage, int index)
        => valueCount == 1 ? $"__nextorm_ext_1_{index + 1}" : $"__nextorm_ext_{stage}_g{index}";

    private static string ValueAliasFor(int valueCount, int stage, int index)
        => valueCount == 1 ? "__nextorm_ext_1_0" : $"__nextorm_ext_{stage}_v{index}";

    private static string AggregateAliasFor(int valueCount, int stage)
        => valueCount == 1 ? "__nextorm_ext_1_0" : $"__nextorm_ext_{stage}_v{stage}";

    private static Expression BuildValueTuple(List<Expression> exprs)
    {
        var types = exprs.Select(e => e.Type).ToArray();
        Type tupleType = exprs.Count switch
        {
            2 => typeof(ValueTuple<,>).MakeGenericType(types),
            3 => typeof(ValueTuple<,,>).MakeGenericType(types),
            4 => typeof(ValueTuple<,,,>).MakeGenericType(types),
            5 => typeof(ValueTuple<,,,,>).MakeGenericType(types),
            6 => typeof(ValueTuple<,,,,,>).MakeGenericType(types),
            7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(types),
            _ => throw new NotSupportedException("Too many components."),
        };
        var ctor = tupleType.GetConstructor(types)
            ?? throw new InvalidOperationException(
                $"The {tupleType.FullName} constructor for {exprs.Count} components was not found.");
        return Expression.New(ctor, exprs);
    }

    private sealed class CapturedState
    {
        public CapturedState(ExtremeKind kind, ExtremeRowTies ties, Expression valueSelector, Expression? groupBy, IEqualityComparer<Expression> comparer)
        {
            Kind = kind;
            Ties = ties;
            ValueSelector = valueSelector;
            GroupBy = groupBy;
            Comparer = comparer;
        }

        public ExtremeKind Kind { get; }
        public ExtremeRowTies Ties { get; }
        public Expression ValueSelector { get; }
        public Expression? GroupBy { get; }
        public IEqualityComparer<Expression> Comparer { get; }
    }

    private sealed class Descriptor : IQueryComponentDescriptor
    {
        private readonly ExtremeRowComponent<TEntity> _component;
        private readonly CapturedState _state;

        public Descriptor(ExtremeRowComponent<TEntity> component, CapturedState state, int hash)
        {
            _component = component;
            _state = state;
            StructuralHash = hash;
        }

        public Type Slot => _component.Slot;
        public Type ComponentType => _component.GetType();
        public QueryComponentContribution Contribution => _component.Contribution;
        public string Discriminator => _state.Kind == ExtremeKind.Min ? "Min" : "Max";
        public int StructuralHash { get; }

        public bool StructurallyEquals(IQueryComponentDescriptor? other)
        {
            if (other is not Descriptor rhs)
                return false;
            if (Slot != rhs.Slot || ComponentType != rhs.ComponentType || Contribution != rhs.Contribution)
                return false;
            if (Discriminator != rhs.Discriminator || _state.Ties != rhs._state.Ties)
                return false;
            if (!_state.Comparer.Equals(_state.ValueSelector, rhs._state.ValueSelector))
                return false;
            if (_state.GroupBy is null || rhs._state.GroupBy is null)
                return _state.GroupBy is null && rhs._state.GroupBy is null;
            return _state.Comparer.Equals(_state.GroupBy, rhs._state.GroupBy);
        }
    }

    /// <summary>
    /// True when the expression contains a call to one of the engine's aggregate functions
    /// (<see cref="CommonFunctions"/>), at any depth.
    /// </summary>
    private sealed class AggregateDetector : ExpressionVisitor
    {
        internal static bool Contains(Expression expression)
        {
            var visitor = new AggregateDetector();
            visitor.Visit(expression);
            return visitor._found;
        }

        private bool _found;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(CommonFunctions)
                && AggregateFunctionNames.Contains(node.Method.Name))
            {
                _found = true;
                return node;
            }

            return base.VisitMethodCall(node);
        }
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}
