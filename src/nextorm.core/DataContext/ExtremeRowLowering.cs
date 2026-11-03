using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Core;

internal static class ExtremeRowLowering
{
    internal const string AggregateAlias = "__nextorm_ext_1_0";

    private static readonly MethodInfo s_nullSafeJoinEqual =
        typeof(ExtremeRowLowering).GetMethod(nameof(NullSafeJoinEqual), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Translation-only marker for the null-safe equality of a group-key join-back
    /// (<c>g = g' or (g is null and g' is null)</c>). The <c>All</c> lowering emits it instead of the
    /// portable OR-form when the dialect can express the comparison natively inside a <c>JOIN ON</c>
    /// predicate: ClickHouse refuses a bare <c>IS NULL</c> residual in <c>JOIN ON</c> (#296) but accepts
    /// its native <c>isNotDistinctFrom</c> there. The renderer decides the emitted SQL form
    /// (<see cref="ExtremeRowNullSafeJoinTranslator"/>), so the marker never leaks a provider name into
    /// the lowering and, on every other dialect, the byte-identical OR-form is still produced. It is
    /// never evaluated: it exists only inside a rendered query expression.
    /// </summary>
    internal static bool NullSafeJoinEqual<T>(T left, T right) => throw new NotSupportedException(
        "The internal ExtremeRow null-safe join marker is translation-only and cannot be evaluated.");

    internal static QueryCommand BuildAllGlobalScalar(QueryCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entityType = cmd.EntityType!;
        var extremeRow = cmd.ExtremeRow!;
        var valueColumn = cmd.ExtremeRowColumns![0];
        var valueExpression = valueColumn.Expression;
        var valueType = valueColumn.PropertyType;
        var originalParameter = extremeRow.ValueSelector.Parameters[0];

        // A bool/bool? value key aggregates through its order-preserving integer representation:
        // SQL Server rejects min/max over a raw bit column, and the representation keeps
        // false < true. A nullable bool keeps NULL as NULL so a NULL-valued source row can never
        // join the extremum 0 back. Every other component type keeps the expression itself.
        var (valueStorage, valueStorageType) = MakeValueStorage(valueExpression!);

        var aggregateSelect = new SelectExpression(valueStorageType)
        {
            Index = 0,
            PropertyName = AggregateAlias,
            Expression = MakeAggregate(extremeRow.Kind, valueStorageType, valueStorage),
            OutputName = AggregateAlias,
        };

        var eCommand = cmd.Clone();
        eCommand.ExtremeRow = null;
        eCommand.SetJoins(null);
        eCommand.SetGroupingList(null);
        eCommand.SetSorting(null);
        eCommand.SetSelectList(new[] { aggregateSelect });
        eCommand.From = cmd.From;
        eCommand.PreparedCondition = MakeLoweredCondition(cmd, valueExpression!, valueType, originalParameter);

        var xParameter = Expression.Parameter(entityType, "x");
        var eParameter = Expression.Parameter(entityType, "e");

        var leftValue = new ParameterReplacer(originalParameter, xParameter).Visit(valueStorage);
        var rightValue = MakeColumnCall(eParameter, valueStorageType, AggregateAlias);
        var joinConditionBody = Expression.Equal(leftValue!, rightValue);
        var joinLambda = Expression.Lambda(joinConditionBody, xParameter, eParameter);

        var join = new JoinExpression(joinLambda, JoinType.Inner)
        {
            From = new FromExpression(eCommand),
            EntityType = entityType,
            SourceParameter = eParameter,
        };

        var oCommand = cmd.Clone();
        oCommand.ExtremeRow = null;
        oCommand.From = cmd.From;
        oCommand.SetJoins(new[] { join });

        return oCommand;
    }

    /// <summary>
    /// Lowers the grouped <c>All</c> scalar form (<c>Ties == All</c>, one value key, one or more group
    /// keys) to the aggregate <c>E</c> joined back to the source <c>O</c>:
    /// <code>
    /// E = select g_1 as __nextorm_ext_1_1, ..., max|min(v1) as __nextorm_ext_1_0
    ///     from T where N and C group by g_1, ..., g_m
    /// O = &lt;projection&gt; from T join E
    ///       on nullSafeEq(T.g_1, Column(E,'__nextorm_ext_1_1')) and ...
    ///      and T.v1 = Column(E,'__nextorm_ext_1_0')
    ///     where C [distinct] [order by ...]
    /// </code>
    /// Each group key keeps its own explicitly assigned output alias, and the join references those
    /// aliases through <see cref="SqlFunctions.Column{T}"/>, so mapped physical names, naming
    /// conventions and case-sensitive providers cannot desynchronize the two sides. A nullable group
    /// key joins with the OR-form <c>a = b or (a is null and b is null)</c>.
    /// </summary>
    internal static QueryCommand BuildAllGroupedScalar(QueryCommand cmd, bool nativeNullSafeJoin)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entityType = cmd.EntityType!;
        var extremeRow = cmd.ExtremeRow!;
        var groupColumns = cmd.ExtremeRowGroupByColumns;
        if (groupColumns is not { Length: > 0 })
            throw new BuildSqlCommandException("SelectWhereMax/SelectWhereMin GROUP BY requires at least one group key.");

        var valueColumn = cmd.ExtremeRowColumns![0];
        var valueExpression = valueColumn.Expression;
        var valueType = valueColumn.PropertyType;
        var originalParameter = extremeRow.ValueSelector.Parameters[0];
        var groupParameter = extremeRow.GroupBy!.Parameters[0];

        // Same order-preserving bool representation as the composite and global-scalar forms.
        var (valueStorage, valueStorageType) = MakeValueStorage(valueExpression!);

        var aggregateSelect = new SelectExpression(valueStorageType)
        {
            Index = groupColumns.Length,
            PropertyName = AggregateAlias,
            OutputName = AggregateAlias,
            Expression = MakeAggregate(extremeRow.Kind, valueStorageType, valueStorage),
        };

        var groupSelects = new SelectExpression[groupColumns.Length];
        var groupConditions = new Expression[groupColumns.Length];
        for (var i = 0; i < groupColumns.Length; i++)
        {
            var alias = GroupAlias(i);
            // The public grouped overload types its selector as Func<TEntity, object?>, so every group
            // key carries a compiler boxing conversion to object at its root. Strip it before rendering:
            // the column keeps its real CLR type (GROUP BY, equality and the Column<T> reference all need
            // it), and only that sealed boxing node - a user-written conversion inside a composite key
            // argument stays untouched.
            var expression = StripBoxing(new ParameterReplacer(groupParameter, originalParameter)
                .Visit(groupColumns[i].Expression!)!);
            groupSelects[i] = new SelectExpression(expression.Type)
            {
                Index = i,
                PropertyName = alias,
                OutputName = alias,
                Expression = expression,
            };
            groupConditions[i] = expression;
        }

        var selectList = new SelectExpression[groupSelects.Length + 1];
        Array.Copy(groupSelects, selectList, groupSelects.Length);
        selectList[groupSelects.Length] = aggregateSelect;

        var eCommand = cmd.Clone();
        eCommand.ExtremeRow = null;
        eCommand.SetJoins(null);
        eCommand.SetSorting(null);
        eCommand.SetSelectList(selectList);
        eCommand.SetGroupingList(groupSelects);
        eCommand.From = cmd.From;
        eCommand.PreparedCondition = MakeLoweredCondition(cmd, valueExpression!, valueType, originalParameter);

        var xParameter = Expression.Parameter(entityType, "x");
        var eParameter = Expression.Parameter(entityType, "e");
        Expression? joinConditionBody = null;

        for (var i = 0; i < groupColumns.Length; i++)
        {
            var alias = GroupAlias(i);
            var keyType = groupConditions[i].Type;
            var left = new ParameterReplacer(originalParameter, xParameter).Visit(groupConditions[i])!;
            var right = MakeColumnCall(eParameter, keyType, alias);

            Expression groupCondition;
            if (IsNullable(keyType))
            {
                groupCondition = MakeNullSafeJoinEqual(left, right, keyType, nativeNullSafeJoin);
            }
            else
            {
                groupCondition = Expression.Equal(left, right);
            }

            joinConditionBody = joinConditionBody is null
                ? groupCondition
                : Expression.AndAlso(joinConditionBody, groupCondition);
        }

        var leftValue = new ParameterReplacer(originalParameter, xParameter).Visit(valueStorage)!;
        var valueCondition = Expression.Equal(leftValue, MakeColumnCall(eParameter, valueStorageType, AggregateAlias));
        joinConditionBody = joinConditionBody is null
            ? valueCondition
            : Expression.AndAlso(joinConditionBody, valueCondition);

        var join = new JoinExpression(Expression.Lambda(joinConditionBody, xParameter, eParameter), JoinType.Inner)
        {
            From = new FromExpression(eCommand),
            EntityType = entityType,
            SourceParameter = eParameter,
        };

        var oCommand = cmd.Clone();
        oCommand.ExtremeRow = null;
        oCommand.From = cmd.From;
        oCommand.SetJoins(new[] { join });

        return oCommand;
    }

    /// <summary>
    /// Lowers the composite <c>All</c> form (<c>Ties == All</c>, <see cref="QueryCommand.ExtremeRowColumns"/>
    /// with two or more value components, optionally one or more group keys) to the staged aggregate
    /// chain <c>E_1 … E_n</c> joined back to the source <c>O</c>:
    /// <code>
    /// E_1 = select g_* as __nextorm_ext_1_g*, max|min(v1) as __nextorm_ext_1_v1
    ///       from T where N and C [group by g_*]
    /// E_k = select g_* as __nextorm_ext_k_g*, v1..v(k-1) as __nextorm_ext_k_v*,
    ///              max|min(vk) as __nextorm_ext_k_vk
    ///       from T join E_(k-1)
    ///         on nullSafeEq(T.g_j, Column(E_(k-1),'__nextorm_ext_(k-1)_g_j'))
    ///        and T.v_i = Column(E_(k-1),'__nextorm_ext_(k-1)_v_i')     -- i &lt; k
    ///       where N and C group by g_*, v1..v(k-1)
    /// O   = &lt;projection&gt; from T join E_n
    ///         on nullSafeEq(T.g_j, Column(E_n,'__nextorm_ext_n_g_j'))
    ///        and T.v_i = Column(E_n,'__nextorm_ext_n_v_i')            -- every component
    ///       where C [distinct] [order by ...]
    /// </code>
    /// <para>
    /// <c>N</c> is the conjunction <c>v_i IS NOT NULL</c> over <b>every</b> value component in <b>every</b>
    /// stage, so a row with a NULL in a later component can never widen a surviving prefix. Stage
    /// <c>k</c> aggregates <c>v_k</c> strictly within the prefix that stage <c>k-1</c> selected, which
    /// is what makes the result lexicographic (<c>(9,2)</c> beats <c>(8,100)</c>) instead of
    /// component-wise (<c>(9,100)</c>). Each stage output carries a lowering-owned alias and is
    /// referenced through <see cref="SqlFunctions.Column{T}"/>, so mapped physical names cannot
    /// desynchronize the two sides. A nullable group key joins with the OR-form
    /// <c>a = b or (a is null and b is null)</c>. The user condition <c>C</c> is duplicated into
    /// every stage and into <c>O</c>, i.e. rendered <c>n+1</c> times deliberately.
    /// </para>
    /// <para>
    /// A <c>bool</c> value component is aggregated through its order-preserving integer representation
    /// <c>case when v then 1 else 0 end</c>: <c>min</c>/<c>max</c> over a SQL Server <c>bit</c> column is
    /// rejected by the server, while the representation keeps <c>false &lt; true</c> and the join-back
    /// comparison uses the same representation on both sides. A <c>bool?</c> component keeps its
    /// nullability (<c>case when v is null then null when v then 1 else 0 end</c>), so a row whose value
    /// key is NULL never joins the integer <c>0</c> back.
    /// </para>
    /// </summary>
    internal static QueryCommand BuildAllComposite(QueryCommand cmd, bool nativeNullSafeJoin)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entityType = cmd.EntityType!;
        var extremeRow = cmd.ExtremeRow!;
        var valueColumns = cmd.ExtremeRowColumns!;
        var valueCount = valueColumns.Length;
        if (valueCount < 2)
            throw new BuildSqlCommandException("The composite SelectWhereMax/SelectWhereMin lowering requires at least two value key components.");

        var groupColumns = cmd.ExtremeRowGroupByColumns;
        var groupCount = groupColumns?.Length ?? 0;
        var originalParameter = extremeRow.ValueSelector.Parameters[0];

        var groupExpressions = new Expression[groupCount];
        var groupTypes = new Type[groupCount];
        if (groupCount > 0)
        {
            var groupParameter = extremeRow.GroupBy!.Parameters[0];
            for (var i = 0; i < groupCount; i++)
            {
                // Same rebinding/stripping as the grouped scalar form: the grouped overload types its
                // selector as Func<TEntity, object?>, so the compiler's boxing conversion is stripped
                // and the key expression is rebound to the value-selector parameter instance that the
                // stage commands carry.
                var expression = StripBoxing(new ParameterReplacer(groupParameter, originalParameter)
                    .Visit(groupColumns![i].Expression!)!);
                groupExpressions[i] = expression;
                groupTypes[i] = expression.Type;
            }
        }

        // The aggregating/comparison representation of every value component (raw, except a bool
        // component, which is widened to its order-preserving integer form). The raw expression of each
        // component is kept too: a stage repeats a prefix component in its GROUP BY, and grouping by the
        // raw column (not by the storage form) is what keeps the two renders of one key identical.
        var valueExpressions = new Expression[valueCount];
        var valueTypes = new Type[valueCount];
        var rawExpressions = new Expression[valueCount];
        var rawTypes = new Type[valueCount];
        for (var i = 0; i < valueCount; i++)
        {
            var raw = valueColumns[i].Expression!;
            var (storage, storageType) = MakeValueStorage(raw);
            valueExpressions[i] = storage;
            valueTypes[i] = storageType;
            rawExpressions[i] = raw;
            rawTypes[i] = raw.Type;
        }

        var notNull = BuildNotNullPredicate(valueColumns);

        QueryCommand? stage = null;
        for (var k = 1; k <= valueCount; k++)
        {
            var selectList = new SelectExpression[groupCount + k];
            var groupingList = new SelectExpression[groupCount + (k - 1)];

            for (var i = 0; i < groupCount; i++)
            {
                var alias = GroupAlias(k, i);
                selectList[i] = new SelectExpression(groupTypes[i])
                {
                    Index = i,
                    PropertyName = alias,
                    OutputName = alias,
                    Expression = groupExpressions[i],
                };
                groupingList[i] = selectList[i];
            }

            for (var i = 0; i < k - 1; i++)
            {
                var alias = ValueAlias(k, i + 1);
                selectList[groupCount + i] = new SelectExpression(valueTypes[i])
                {
                    Index = groupCount + i,
                    PropertyName = alias,
                    OutputName = alias,
                    Expression = valueExpressions[i],
                };
                // GROUP BY repeats the *raw* component of the prefix, not its storage form. The two
                // renders must be identical for the server to accept the statement, and the storage
                // form is not always parameter-free: a value-converted component (a model bool in an
                // int column, an enum in a text column) folds its converted constant into a parameter,
                // and the SELECT list and the GROUP BY render the same node twice, i.e. as two distinct
                // parameters (SQL Server then rejects the select-list expression as "not contained in
                // either an aggregate function or the GROUP BY clause"). The storage form is a
                // deterministic function of the raw column, so grouping by the raw column is
                // equivalent, and for every non-converted component the raw expression *is* the
                // storage expression, so the rendered SQL is unchanged.
                groupingList[groupCount + i] = new SelectExpression(rawTypes[i])
                {
                    Index = groupCount + i,
                    PropertyName = alias,
                    Expression = rawExpressions[i],
                };
            }

            var aggregateAlias = ValueAlias(k, k);
            selectList[groupCount + k - 1] = new SelectExpression(valueTypes[k - 1])
            {
                Index = groupCount + k - 1,
                PropertyName = aggregateAlias,
                OutputName = aggregateAlias,
                Expression = MakeAggregate(extremeRow.Kind, valueTypes[k - 1], valueExpressions[k - 1]),
            };

            var stageCommand = cmd.Clone();
            stageCommand.ExtremeRow = null;
            stageCommand.From = cmd.From;
            stageCommand.SetSorting(null);
            stageCommand.SetSelectList(selectList);
            stageCommand.SetGroupingList(groupCount + k - 1 > 0 ? groupingList : null);
            stageCommand.PreparedCondition = MakeCompositeCondition(cmd, notNull, originalParameter);

            if (k == 1)
            {
                stageCommand.SetJoins(null);
            }
            else
            {
                var leftParameter = Expression.Parameter(entityType, "x");
                var stageParameter = Expression.Parameter(entityType, "e");
                Expression? joinCondition = null;

                for (var i = 0; i < groupCount; i++)
                {
                    var left = new ParameterReplacer(originalParameter, leftParameter).Visit(groupExpressions[i])!;
                    var right = MakeColumnCall(stageParameter, groupTypes[i], GroupAlias(k - 1, i));
                    joinCondition = And(joinCondition, MakeNullSafeJoinEqual(left, right, groupTypes[i], nativeNullSafeJoin));
                }

                for (var i = 0; i < k - 1; i++)
                {
                    var left = new ParameterReplacer(originalParameter, leftParameter).Visit(valueExpressions[i])!;
                    var right = MakeColumnCall(stageParameter, valueTypes[i], ValueAlias(k - 1, i + 1));
                    joinCondition = And(joinCondition, Expression.Equal(left, right));
                }

                stageCommand.SetJoins(
                [
                    new JoinExpression(Expression.Lambda(joinCondition!, leftParameter, stageParameter), JoinType.Inner)
                    {
                        From = new FromExpression(stage!) { BindOccurrenceByParameter = true },
                        EntityType = entityType,
                        SourceParameter = stageParameter,
                    },
                ]);
            }

            stage = stageCommand;
        }

        var outerLeft = Expression.Parameter(entityType, "x");
        var outerStage = Expression.Parameter(entityType, "e");
        Expression? outerCondition = null;

        for (var i = 0; i < groupCount; i++)
        {
            var left = new ParameterReplacer(originalParameter, outerLeft).Visit(groupExpressions[i])!;
            var right = MakeColumnCall(outerStage, groupTypes[i], GroupAlias(valueCount, i));
            outerCondition = And(outerCondition, MakeNullSafeJoinEqual(left, right, groupTypes[i], nativeNullSafeJoin));
        }

        for (var i = 0; i < valueCount; i++)
        {
            var left = new ParameterReplacer(originalParameter, outerLeft).Visit(valueExpressions[i])!;
            var right = MakeColumnCall(outerStage, valueTypes[i], ValueAlias(valueCount, i + 1));
            outerCondition = And(outerCondition, Expression.Equal(left, right));
        }

        var oCommand = cmd.Clone();
        oCommand.ExtremeRow = null;
        oCommand.From = cmd.From;
        oCommand.SetJoins(
        [
            new JoinExpression(Expression.Lambda(outerCondition!, outerLeft, outerStage), JoinType.Inner)
            {
                From = new FromExpression(stage!) { BindOccurrenceByParameter = true },
                EntityType = entityType,
                SourceParameter = outerStage,
            },
        ]);

        return oCommand;
    }

    /// <summary>
    /// The aggregating representation of one value component: the expression itself, or its
    /// order-preserving integer form for a <c>bool</c>/<c>bool?</c> component (see
    /// <see cref="BuildAllComposite"/>); a nullable bool keeps NULL as NULL.
    /// </summary>
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

        // A nullable bool keeps its nullability in the storage representation: mapping NULL to 0 would
        // let a NULL-valued source row join the extremum 0 back in the outer query (the non-null guard
        // only filters the aggregate stage), so NULL stays NULL and never equals a non-null extremum.
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

    /// <summary>
    /// The non-null guard <c>N</c>: <c>v_i IS NOT NULL</c> for every nullable value component. A
    /// non-nullable value-type component cannot be NULL, so it contributes no predicate.
    /// </summary>
    private static Expression? BuildNotNullPredicate(SelectExpression[] valueColumns)
    {
        Expression? notNull = null;
        for (var i = 0; i < valueColumns.Length; i++)
        {
            var expression = valueColumns[i].Expression!;
            var type = expression.Type;
            if (!IsNullable(type))
                continue;

            var nullConstant = Expression.Constant(null, type);
            notNull = And(notNull, Expression.NotEqual(expression, nullConstant));
        }

        return notNull;
    }

    /// <summary>Builds the <c>N AND C</c> stage condition, rebound to the value-selector parameter.</summary>
    private static LambdaExpression? MakeCompositeCondition(
        QueryCommand cmd,
        Expression? notNull,
        ParameterExpression originalParameter)
    {
        Expression? userCondition = null;
        if (cmd.PreparedCondition is LambdaExpression userLambda && userLambda.Parameters.Count > 0)
        {
            userCondition = new ParameterReplacer(userLambda.Parameters[0], originalParameter).Visit(userLambda.Body);
        }
        else if (cmd.PreparedCondition is { } rawCondition)
        {
            userCondition = rawCondition;
        }

        var combined = notNull is null
            ? userCondition
            : userCondition is null ? notNull : Expression.AndAlso(notNull, userCondition);

        return combined is null ? null : Expression.Lambda(combined, originalParameter);
    }

    private static Expression And(Expression? left, Expression right)
        => left is null ? right : Expression.AndAlso(left, right);

    /// <summary>
    /// Null-safe equality between two sides of a group-key join: a plain <c>=</c> for a non-nullable
    /// value type, the OR-form <c>a = b or (a is null and b is null)</c> otherwise, or — when the
    /// dialect can express it natively inside <c>JOIN ON</c> — the internal
    /// <see cref="NullSafeJoinEqual{T}(T, T)"/> marker the renderer turns into that dialect's form
    /// (ClickHouse <c>isNotDistinctFrom</c>; the #296 guard stays untouched for user predicates).
    /// </summary>
    internal static Expression MakeNullSafeJoinEqual(
        Expression left, Expression right, Type type, bool nativeNullSafeJoin)
    {
        if (!IsNullable(type))
            return Expression.Equal(left, right);

        if (nativeNullSafeJoin)
            return Expression.Call(s_nullSafeJoinEqual.MakeGenericMethod(type), left, right);

        var nullConstant = Expression.Constant(null, type);
        return Expression.OrElse(
            Expression.Equal(left, right),
            Expression.AndAlso(
                Expression.Equal(left, nullConstant),
                Expression.Equal(right, nullConstant)));
    }

    private static Expression MakeAggregate(ExtremeKind kind, Type valueType, Expression valueExpression)
    {
        var aggregateMethodInfo = (kind == ExtremeKind.Max ? CommonFunctions.MaxMI : CommonFunctions.MinMI)
            .MakeGenericMethod(valueType);
        var sqlInstance = Expression.Property(null, typeof(SqlFunctions).GetProperty("Sql")!);
        return Expression.Call(sqlInstance, aggregateMethodInfo, valueExpression);
    }

    private static Expression MakeColumnCall(ParameterExpression parameter, Type type, string alias)
    {
        var columnMethod = typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.Column))!.MakeGenericMethod(type);
        return Expression.Call(null, columnMethod, parameter, Expression.Constant(alias));
    }

    private static string GroupAlias(int index) => $"__nextorm_ext_1_{index + 1}";

    /// <summary>The output alias of group key <paramref name="index"/> in composite stage <paramref name="stage"/>.</summary>
    private static string GroupAlias(int stage, int index) => $"__nextorm_ext_{stage}_g{index}";

    /// <summary>
    /// The output alias of value component <paramref name="index"/> (1-based) in composite stage
    /// <paramref name="stage"/>; the stage aggregates its own component, earlier components pass
    /// through.
    /// </summary>
    private static string ValueAlias(int stage, int index) => $"__nextorm_ext_{stage}_v{index}";

    /// <summary>
    /// Removes the compiler's boxing conversion to <see cref="object"/> that the grouped selector's
    /// <c>Func&lt;TEntity, object?&gt;</c> signature introduces at the root of every key expression.
    /// </summary>
    private static Expression StripBoxing(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } unary
            && unary.Type == typeof(object))
        {
            expression = unary.Operand;
        }

        return expression;
    }

    /// <summary>
    /// Builds the aggregate subquery condition: the non-null guard <c>N</c> over the value key followed
    /// by the user condition <c>C</c>, both rebound to the value-selector parameter.
    /// </summary>
    private static LambdaExpression? MakeLoweredCondition(
        QueryCommand cmd,
        Expression valueExpression,
        Type valueType,
        ParameterExpression originalParameter)
    {
        Expression? notNullPredicate = null;
        if (IsNullable(valueType))
        {
            var nullConstant = Expression.Constant(null, valueType);
            notNullPredicate = Expression.NotEqual(valueExpression, nullConstant);
        }

        Expression? userCondition = null;
        if (cmd.PreparedCondition is LambdaExpression userLambda && userLambda.Parameters.Count > 0)
        {
            userCondition = new ParameterReplacer(userLambda.Parameters[0], originalParameter).Visit(userLambda.Body);
        }
        else if (cmd.PreparedCondition is { } rawCondition)
        {
            userCondition = rawCondition;
        }

        Expression? combinedCondition = notNullPredicate;
        if (userCondition is not null)
        {
            combinedCondition = combinedCondition is null
                ? userCondition
                : Expression.AndAlso(combinedCondition, userCondition);
        }

        return combinedCondition is null
            ? null
            : Expression.Lambda(combinedCondition, originalParameter);
    }

    private static bool IsNullable(Type type)
    {
        if (!type.IsValueType)
            return true;
        return Nullable.GetUnderlyingType(type) is not null;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _old;
        private readonly ParameterExpression _new;

        public ParameterReplacer(ParameterExpression old, ParameterExpression @new)
        {
            _old = old;
            _new = @new;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _old ? _new : base.VisitParameter(node);
    }
}
