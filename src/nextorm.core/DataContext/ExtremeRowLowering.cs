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

        var aggregateMethodInfo = (extremeRow.Kind == ExtremeKind.Max ? CommonFunctions.MaxMI : CommonFunctions.MinMI)
            .MakeGenericMethod(valueType);
        var sqlProperty = typeof(SqlFunctions).GetProperty("Sql")!;
        var sqlInstance = Expression.Property(null, sqlProperty);
        var aggregateExpression = Expression.Call(sqlInstance, aggregateMethodInfo, valueExpression!);

        var aggregateSelect = new SelectExpression(valueType)
        {
            Index = 0,
            PropertyName = AggregateAlias,
            Expression = aggregateExpression,
            OutputName = AggregateAlias,
        };

        var eCommand = cmd.Clone();
        eCommand.ExtremeRow = null;
        eCommand.SetJoins(null);
        eCommand.SetGroupingList(null);
        eCommand.SetSorting(null);
        eCommand.SetSelectList(new[] { aggregateSelect });
        eCommand.From = cmd.From;

        Expression? notNullPredicate = null;
        if (IsNullable(valueType))
        {
            var nullConstant = Expression.Constant(null, valueType);
            notNullPredicate = Expression.NotEqual(valueExpression!, nullConstant);
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
        eCommand.PreparedCondition = combinedCondition is null
            ? null
            : Expression.Lambda(combinedCondition, originalParameter);

        var xParameter = Expression.Parameter(entityType, "x");
        var eParameter = Expression.Parameter(entityType, "e");

        var leftValue = new ParameterReplacer(originalParameter, xParameter).Visit(valueExpression);
        var columnMethod = typeof(SqlFunctions).GetMethod("Column")!.MakeGenericMethod(valueType);
        var rightValue = Expression.Call(null, columnMethod, eParameter, Expression.Constant(AggregateAlias));
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
