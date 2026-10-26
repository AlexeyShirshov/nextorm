using System.Linq.Expressions;
using System.Reflection;
namespace NextORM.Core;

/// <summary>
/// Rewrites an <see cref="EntityBuilder{TEntity}"/> aggregate terminal used inside a subquery
/// (<c>inner.Where(...).Count()</c>) into the equivalent scalar-subquery projection: the receiver is
/// projected through <see cref="EntityBuilder{TEntity}.Select{TResult}"/> with the matching
/// <see cref="CommonFunctions"/> aggregate body, so the query renders as
/// <c>(select count(*)/sum(...) from ...)</c> instead of being executed as a separate query.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Static readonly reflection-metadata fields (CountMI) are intentionally PascalCase as immutable lookup tables; IDE1006 is a suggestion and is not enforced by the build.")]
internal static class AggregateTerminalRewriter
{
    private static readonly MethodInfo CountMI = typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.count), [typeof(object[])])!;

    /// <summary>
    /// Builds <c>builderReceiver.Select(aggregate)</c> for the aggregate terminal
    /// <paramref name="node"/>; the result is a <see cref="QueryCommand{TResult}"/> whose single
    /// projection is the aggregate.
    /// </summary>
    public static MethodCallExpression Rewrite(MethodCallExpression node, Expression builderReceiver)
    {
        var entityType = builderReceiver.Type.GetGenericArguments()[0];
        var selectMethod = SelectMethodFor(builderReceiver.Type, entityType);

        LambdaExpression selectLambda;
        Type resultType;

        if (node.Method.Name == nameof(EntityBuilderExtensions.Count))
        {
            resultType = typeof(int);
            var parameter = Expression.Parameter(entityType, "e");
            var body = Expression.Call(CommonFunctions.SQLExpression, CountMI, Expression.NewArrayInit(typeof(object)));
            selectLambda = Expression.Lambda(body, parameter);
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

        return Expression.Call(builderReceiver, selectMethod.MakeGenericMethod(resultType), Expression.Quote(selectLambda));
    }

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

    private static MethodInfo SelectMethodFor(Type builderType, Type entityType)
    {
        var selectMethod = builderType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(m => m.Name == nameof(EntityBuilder<object>.Select)
                && m.IsGenericMethodDefinition
                && m.GetParameters() is [{ ParameterType: { IsGenericType: true } parameterType }]
                && parameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && parameterType.GetGenericArguments()[0] is { IsGenericType: true } selectorType
                && selectorType.GetGenericTypeDefinition() == typeof(Func<,>)
                && selectorType.GetGenericArguments()[0] == entityType);

        return selectMethod
            ?? throw new NotSupportedException($"Cannot find the '{nameof(EntityBuilder<object>.Select)}' projection method on '{builderType.Name}'.");
    }
}
