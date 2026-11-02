using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Core;

internal static class ExtremeRowLowering
{
    internal const string AggregateAlias = "__nextorm_ext_1_0";

    internal static QueryCommand BuildAllGlobalScalar(QueryCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var entityType = cmd.EntityType!;
        var extremeRow = cmd.ExtremeRow!;
        var valueColumn = cmd.ExtremeRowColumns![0];
        var valueExpression = valueColumn.Expression;
        var valueType = valueColumn.PropertyType;
        var originalParameter = extremeRow.ValueSelector.Parameters[0];

        var aggregateSelect = new SelectExpression(valueType)
        {
            Index = 0,
            PropertyName = AggregateAlias,
            Expression = MakeAggregate(extremeRow.Kind, valueType, valueExpression!),
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

        var leftValue = new ParameterReplacer(originalParameter, xParameter).Visit(valueExpression);
        var rightValue = MakeColumnCall(eParameter, valueType, AggregateAlias);
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
    internal static QueryCommand BuildAllGroupedScalar(QueryCommand cmd)
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

        var aggregateSelect = new SelectExpression(valueType)
        {
            Index = groupColumns.Length,
            PropertyName = AggregateAlias,
            OutputName = AggregateAlias,
            Expression = MakeAggregate(extremeRow.Kind, valueType, valueExpression!),
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
                var nullConstant = Expression.Constant(null, keyType);
                groupCondition = Expression.OrElse(
                    Expression.Equal(left, right),
                    Expression.AndAlso(
                        Expression.Equal(left, nullConstant),
                        Expression.Equal(right, nullConstant)));
            }
            else
            {
                groupCondition = Expression.Equal(left, right);
            }

            joinConditionBody = joinConditionBody is null
                ? groupCondition
                : Expression.AndAlso(joinConditionBody, groupCondition);
        }

        var leftValue = new ParameterReplacer(originalParameter, xParameter).Visit(valueExpression)!;
        var valueCondition = Expression.Equal(leftValue, MakeColumnCall(eParameter, valueType, AggregateAlias));
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
