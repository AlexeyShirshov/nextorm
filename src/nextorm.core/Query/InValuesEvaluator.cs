using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Evaluates a value-list or lookup collection. A cached accessor reads the expression's constants
/// from the current invocation; neither its key nor its compiled body embeds those constants.
/// </summary>
internal static class InValuesEvaluator
{
    private static readonly ParameterExpression Arguments = Expression.Parameter(typeof(object), "arguments");
    private static readonly UnaryExpression ArgumentList = Expression.Convert(Arguments, typeof(List<object?>));
    private static readonly MethodInfo ReadArgument = typeof(List<object?>).GetProperty("Item")!.GetMethod!;

    public static object? Evaluate(Expression valuesExp, IQueryRegistry queryProvider)
    {
        // A directly embedded value has no accessor shape worth caching.
        if (valuesExp is ConstantExpression)
            return EvaluateUncached(valuesExp);

        var rewriter = new AccessorRewriter();
        var body = rewriter.Visit(valuesExp)!;
        if (!rewriter.CanCache)
            return EvaluateUncached(valuesExp);

        var key = new ExpressionKey(body, queryProvider);
        var cache = DataContextCache.InValuesCache;
        if (!cache.TryGetValue(key, out var accessor))
        {
            accessor = Expression.Lambda<Func<object?, object?>>(Expression.Convert(body, typeof(object)), Arguments).Compile();
            cache[key] = accessor;
        }

        return accessor(rewriter.Values);
    }

    private static object? EvaluateUncached(Expression expression)
        => Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();

    private sealed class AccessorRewriter : ExpressionVisitor
    {
        internal bool CanCache { get; private set; } = true;
        internal List<object?>? Values { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (node is null || !CanCache)
                return node;

            // Share only ordinary CLR expression nodes with structural equality. The only parameter
            // in the rewritten body is Arguments; do not compare scopes, extensions, or registry
            // indices using SQL-plan equality. Other expressions still evaluate on the uncached path.
            if (node is ConstantExpression or MemberExpression or MethodCallExpression or BinaryExpression
                or UnaryExpression or ConditionalExpression or NewExpression or NewArrayExpression
                or ListInitExpression or MemberInitExpression or DefaultExpression or TypeBinaryExpression)
                return base.Visit(node);

            CanCache = false;
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Use the same typed slot read in the key and in the compiled accessor. No receiver or
            // other runtime value is left in the expression that enters the cache.
            var values = Values ??= [];
            var access = Expression.Call(ArgumentList, ReadArgument, Expression.Constant(values.Count));
            values.Add(node.Value);
            return Expression.Convert(access, node.Type);
        }
    }
}
