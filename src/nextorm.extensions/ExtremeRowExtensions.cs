using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Extensions;

/// <summary>Component member.</summary>
public enum ExtremeKind
{
    /// <summary>Minimum extreme.</summary>
    Min,
    /// <summary>Maximum extreme.</summary>
    Max
}

/// <summary>
/// The reflection handles the component emits into its structural expression tree, resolved once for
/// the process instead of re-scanned on every cold preparation. Every handle is a method the public
/// engine surface already exposes; nothing internal is opened.
/// </summary>
internal static class ExtremeRowReflection
{
    internal static readonly MethodInfo Asc =
        typeof(CommonFunctions).GetMethod("asc", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo Desc =
        typeof(CommonFunctions).GetMethod("desc", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo RowNumber =
        typeof(CommonFunctions).GetMethod("row_number", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo Rank =
        typeof(CommonFunctions).GetMethod("rank", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo ColumnOfInt =
        typeof(SqlFunctions).GetMethod("Column", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(int));

    internal static readonly MethodInfo Over =
        typeof(WindowFunction<int>).GetMethod(
            "Over",
            new[] { typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame) })!;
}

/// <summary>Component member.</summary>
public static class ExtremeRowExtensions
{
    /// <summary>Component member.</summary>
    public static EntityBuilder<TEntity> SelectWhereMinEx<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Min, valueSelector, ties, groupBy));

    /// <summary>Component member.</summary>
    public static EntityBuilder<TEntity> SelectWhereMaxEx<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExtremeRowComponent<TEntity>(ExtremeKind.Max, valueSelector, ties, groupBy));

    /// <summary>Component member.</summary>
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

    /// <summary>Component member.</summary>
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

/// <summary>Component member.</summary>
public sealed class ExtremeRowComponent<TEntity> : IQueryComponent
{
    /// <summary>Component member.</summary>
    public static int BuildCount;
    /// <summary>Component member.</summary>
    public static void ResetBuildCount() => BuildCount = 0;
    private readonly ExtremeKind _kind;
    private readonly LambdaExpression _valueSelector;
    private readonly ExtremeRowTies _ties;
    private readonly Expression<Func<TEntity, object?>>? _groupBy;

    /// <summary>Component member.</summary>
    public ExtremeRowComponent(
        ExtremeKind kind,
        LambdaExpression valueSelector,
        ExtremeRowTies ties,
        Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        _kind = kind;
        _valueSelector = valueSelector;
        _ties = ties;
        _groupBy = groupBy;
    }

    /// <summary>Component member.</summary>
    public Type Slot => typeof(ExtremeRowComponent<TEntity>);

    /// <summary>Component member.</summary>
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

    /// <summary>Component member.</summary>
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

    /// <summary>Component member.</summary>
    public void BuildQuery(IQueryComponentBuildContext context)
    {
        ExtremeRowComponent<TEntity>.BuildCount++;
        ArgumentNullException.ThrowIfNull(context);
        var cmd = context.Command;
        // ClickHouse refuses bare IS NULL residuals in JOIN ON (#296), and the ext package cannot
        // reach the core internal null-safe join marker (no IVT). Keep semantic parity there by
        // using the rank-window form for All as well, which partitions on the group keys directly
        // and never produces a null-safe join.
        var isClickHouse = cmd.DataContext is not null
            && cmd.DataContext is DataContext dc
            && string.Equals(dc.Dialect.GetType().Name, "ClickHouseDialect", StringComparison.Ordinal);
        if (_ties == ExtremeRowTies.All && !isClickHouse)
        {
            BuildAllQuery(context);
            return;
        }
        var entityType = typeof(TEntity);
        var rankAlias = MakeRankAlias(entityType);
        var entityParam = Expression.Parameter(entityType, "x");
        var valueComponents = DecomposeSelector(_valueSelector, entityParam);
        var groupComponents = _groupBy is null ? Array.Empty<Expression>() : DecomposeSelector(_groupBy, entityParam);

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

    private void BuildAllQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var ctx = cmd.DataContext ?? throw new InvalidOperationException("DataContext is required.");
        var entityType = typeof(TEntity);
        var xParam = Expression.Parameter(entityType, "x");

        var valueComponents = DecomposeSelector(_valueSelector, xParam);
        var groupComponents = _groupBy is null ? Array.Empty<Expression>() : DecomposeSelector(_groupBy, xParam);
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
                        ? MakeNullSafeJoinEqual(left, right, groupComponents[i].Type, false)
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
                ? MakeNullSafeJoinEqual(left, right, groupComponents[i].Type, false)
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
            Condition = userLambda,
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

    private static Expression MakeNullSafeJoinEqual(Expression left, Expression right, Type type, bool nativeNullSafeJoin)
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
        var methodName = kind == ExtremeKind.Max ? "max" : "min";
        var genericDefinition = typeof(CommonFunctions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == methodName && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        var mi = genericDefinition.MakeGenericMethod(valueType);
        var sqlProp = Expression.Property(null, typeof(SqlFunctions), "Sql");
        return Expression.Call(sqlProp, mi, valueExpression);
    }

    private static Expression MakeColumnCall(ParameterExpression parameter, Type type, string alias)
    {
        var method = typeof(SqlFunctions).GetMethod("Column")!.MakeGenericMethod(type);
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
        var ctor = tupleType.GetConstructor(types)!;
        return Expression.New(ctor, exprs);
    }

    private sealed class CapturedState
    {
        /// <summary>Component member.</summary>
        public CapturedState(ExtremeKind kind, ExtremeRowTies ties, Expression valueSelector, Expression? groupBy, IEqualityComparer<Expression> comparer)
        {
            Kind = kind;
            Ties = ties;
            ValueSelector = valueSelector;
            GroupBy = groupBy;
            Comparer = comparer;
        }

        /// <summary>Component member.</summary>
        public ExtremeKind Kind { get; }
        /// <summary>Component member.</summary>
        public ExtremeRowTies Ties { get; }
        /// <summary>Component member.</summary>
        public Expression ValueSelector { get; }
        /// <summary>Component member.</summary>
        public Expression? GroupBy { get; }
        /// <summary>Component member.</summary>
        public IEqualityComparer<Expression> Comparer { get; }
    }

    private sealed class Descriptor : IQueryComponentDescriptor
    {
        private readonly ExtremeRowComponent<TEntity> _component;
        private readonly CapturedState _state;

        /// <summary>Component member.</summary>
        public Descriptor(ExtremeRowComponent<TEntity> component, CapturedState state, int hash)
        {
            _component = component;
            _state = state;
            StructuralHash = hash;
        }

        /// <summary>Component member.</summary>
        public Type Slot => _component.Slot;
        /// <summary>Component member.</summary>
        public Type ComponentType => _component.GetType();
        /// <summary>Component member.</summary>
        public QueryComponentContribution Contribution => _component.Contribution;
        /// <summary>Component member.</summary>
        public string Discriminator => _state.Kind == ExtremeKind.Min ? "Min" : "Max";
        /// <summary>Component member.</summary>
        public int StructuralHash { get; }

        /// <summary>Component member.</summary>
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

        /// <summary>Component member.</summary>
        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}

/// <summary>Component member.</summary>
public sealed class ConflictingReshapeComponent<TEntity> : IQueryComponent
{
    /// <summary>Component member.</summary>
    public Type Slot => typeof(ConflictingReshapeComponent<TEntity>);
    /// <summary>Component member.</summary>
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
    /// <summary>Component member.</summary>
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
        new SimpleDescriptor(Slot, GetType(), Contribution, "Reshape", 0, (a, b) => true);
    /// <summary>Component member.</summary>
    public void BuildQuery(IQueryComponentBuildContext context) { }
}

internal sealed class SimpleDescriptor : IQueryComponentDescriptor
{
    private readonly Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> _equals;
    /// <summary>Component member.</summary>
    public SimpleDescriptor(Type slot, Type componentType, QueryComponentContribution contribution, string discriminator, int hash, Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> equals)
    {
        Slot = slot;
        ComponentType = componentType;
        Contribution = contribution;
        Discriminator = discriminator;
        StructuralHash = hash;
        _equals = equals;
    }
    /// <summary>Component member.</summary>
    public Type Slot { get; }
    /// <summary>Component member.</summary>
    public Type ComponentType { get; }
    /// <summary>Component member.</summary>
    public QueryComponentContribution Contribution { get; }
    /// <summary>Component member.</summary>
    public string Discriminator { get; }
    /// <summary>Component member.</summary>
    public int StructuralHash { get; }
    /// <summary>Component member.</summary>
    public bool StructurallyEquals(IQueryComponentDescriptor? other) => other is not null && _equals(this, other);
}
/// <summary>Component member.</summary>
public sealed class AdditivePredicateComponent<TEntity> : IQueryComponent
{
    private readonly LambdaExpression _predicate;

    /// <summary>Component member.</summary>
    public AdditivePredicateComponent(LambdaExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    /// <summary>Component member.</summary>
    public Type Slot => typeof(AdditivePredicateComponent<TEntity>);
    /// <summary>Component member.</summary>
    public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

    /// <summary>Component member.</summary>
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
        => new SimpleDescriptor(Slot, GetType(), Contribution, "Predicate", 0, (a, b) => true);

    /// <summary>Component member.</summary>
    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var param = Expression.Parameter(entityType, "x");

        Expression? body = null;
        var existing = cmd.GetComponentPreparedCondition();
        if (existing is LambdaExpression existingLambda)
            body = new Replacer(existingLambda.Parameters[0], param).Visit(existingLambda.Body);

        var predicateBody = new Replacer(_predicate.Parameters[0], param).Visit(_predicate.Body);
        var combined = body is null ? predicateBody : Expression.AndAlso(body, predicateBody);
        cmd.SetComponentPreparedCondition(Expression.Lambda(combined, param));
    }

    private sealed class Replacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        /// <summary>Component member.</summary>
        public Replacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}
