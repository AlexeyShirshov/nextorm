using System.Linq.Expressions;
using System.Reflection;
namespace NextORM.Core;

/// <summary>
/// Rewrites an <see cref="EntityBuilder{TEntity}"/> aggregate terminal used inside a subquery
/// (<c>inner.Where(...).Count()</c>) into the equivalent scalar-subquery projection: the receiver is
/// projected through <see cref="EntityBuilder{TEntity}.Select{TResult}"/> with the matching
/// <see cref="CommonFunctions"/> aggregate body, so the query renders as
/// <c>(select count(*)/count_big(*)/sum(...) from ...)</c> instead of being executed as a separate query.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Static readonly reflection-metadata fields (CountMI) are intentionally PascalCase as immutable lookup tables; IDE1006 is a suggestion and is not enforced by the build.")]
internal static class AggregateTerminalRewriter
{
    private static readonly MethodInfo CountMI = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.count), [typeof(object[])])!;
    private static readonly MethodInfo CountBigMI = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.count_big), [typeof(object[])])!;

    /// <summary>
    /// Builds the aggregate projection call for the terminal <paramref name="node"/>: the receiver is
    /// projected through the non-generic <see cref="IAggregateProjectionHost.SelectAggregateProjection"/>
    /// entry point, so the aggregate's runtime result type never closes a generic method at run time.
    /// </summary>
    public static MethodCallExpression Rewrite(MethodCallExpression node, Expression builderReceiver)
    {
        var entityType = builderReceiver.Type.GetGenericArguments()[0];

        LambdaExpression selectLambda;
        Type resultType;

        if (node.Method.Name is nameof(EntityBuilderExtensions.Count) or nameof(EntityBuilderExtensions.LongCount))
        {
            // Both selector-free row counts travel the same projection route; LongCount() selects the
            // 64-bit count_big(*) form, which the dialects render as count_big / toInt64(count(...)).
            var isWide = node.Method.Name == nameof(EntityBuilderExtensions.LongCount);
            var countMethod = isWide ? CountBigMI : CountMI;
            resultType = isWide ? typeof(long) : typeof(int);
            var parameter = Expression.Parameter(entityType, "e");
            var body = Expression.Call(CommonFunctions.SQLExpression, countMethod, Expression.NewArrayInit(typeof(object)));
            selectLambda = Expression.Lambda(body, parameter);
        }
        else if (node.Method.Name == nameof(EntityBuilderExtensions.Avg))
        {
            // AVG's result type is declared by the selected numeric overload, not by the selector:
            // an integral selector returns a fractional nullable result. The concrete declaration is
            // resolved from the selector's own type, so the projection column keeps that contract.
            var selector = UnwrapLambda(node.Arguments[1]);
            var aggregate = CommonFunctions.AverageMethodFor(selector.Body.Type);
            resultType = aggregate.ReturnType;
            var body = Expression.Call(CommonFunctions.SQLExpression, aggregate, selector.Body);
            selectLambda = Expression.Lambda(body, selector.Parameters);
        }
        else
        {
            var selector = UnwrapLambda(node.Arguments[1]);
            var aggregateDefinition = AggregateMethodFor(node.Method.Name);

            // The variability aggregates declare `T? value` with `where T : struct`, so their declared
            // result is structurally nullable. Project it as Nullable<TResult> (the terminal's own
            // return type) and convert a non-nullable selector body to the nullable parameter;
            // without this the projection column would stay non-nullable and a SQL NULL would reach
            // the unguarded reader getter.
            if (aggregateDefinition.GetParameters()[0].ParameterType is { IsGenericType: true } nullableParameter
                && nullableParameter.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var declaredReturn = node.Method.ReturnType;
                resultType = Nullable.GetUnderlyingType(declaredReturn) is not null ? declaredReturn : selector.ReturnType;
                var valueType = Nullable.GetUnderlyingType(resultType) ?? resultType;
                var aggregate = aggregateDefinition.MakeGenericMethod(valueType);
                var parameter = aggregate.GetParameters()[0].ParameterType;
                var argument = selector.Body.Type == parameter
                    ? selector.Body
                    : Expression.Convert(selector.Body, parameter);
                var nullableBody = Expression.Call(CommonFunctions.SQLExpression, aggregate, argument);
                selectLambda = Expression.Lambda(nullableBody, selector.Parameters);
            }
            else
            {
                resultType = selector.ReturnType;
                var aggregate = aggregateDefinition.MakeGenericMethod(resultType);
                var body = Expression.Call(CommonFunctions.SQLExpression, aggregate, selector.Body);
                selectLambda = Expression.Lambda(body, selector.Parameters);
            }
        }

        return Expression.Call(
            Expression.Convert(builderReceiver, typeof(IAggregateProjectionHost)),
            SelectAggregateProjectionMI,
            Expression.Constant(selectLambda, typeof(LambdaExpression)),
            Expression.Constant(resultType, typeof(Type)));
    }

    /// <summary>
    /// The statically known non-generic projection entry point; see
    /// <see cref="IAggregateProjectionHost"/>.
    /// </summary>
    private static readonly MethodInfo SelectAggregateProjectionMI =
        typeof(IAggregateProjectionHost).GetMethod(nameof(IAggregateProjectionHost.SelectAggregateProjection))!;

    private static LambdaExpression UnwrapLambda(Expression expression) => expression switch
    {
        UnaryExpression { NodeType: ExpressionType.Quote } quote => (LambdaExpression)quote.Operand,
        LambdaExpression lambda => lambda,
        _ => throw new NotSupportedException($"Expected a selector lambda, got '{expression.NodeType}'.")
    };

    private static MethodInfo AggregateMethodFor(string methodName) => methodName switch
    {
        nameof(EntityBuilderExtensions.Sum) => CommonFunctions.SumMI,
        nameof(EntityBuilderExtensions.Min) => CommonFunctions.MinMI,
        nameof(EntityBuilderExtensions.Max) => CommonFunctions.MaxMI,
        nameof(EntityBuilderExtensions.Avg) => CommonFunctions.AvgMI,
        nameof(EntityBuilderExtensions.Stdev) => CommonFunctions.StdevMI,
        nameof(EntityBuilderExtensions.Stdevp) => CommonFunctions.StdevpMI,
        nameof(EntityBuilderExtensions.Var) => CommonFunctions.VarMI,
        nameof(EntityBuilderExtensions.Varp) => CommonFunctions.VarpMI,
        _ => throw new NotSupportedException($"The aggregate terminal '{methodName}' is not supported inside a subquery.")
    };

}
