using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

// Both the key and accessor are built from typed slot reads. Only the evaluator's local
// argument list sees the original constants; SQL plan keys still decide which values shape SQL.
internal sealed class ValueExpressionAccessor : ExpressionVisitor
{
    private delegate object? ValueAccessor(object? arguments);

    private static readonly ParameterExpression Arguments = Expression.Parameter(typeof(object), "arguments");
    private static readonly UnaryExpression ArgumentList = Expression.Convert(Arguments, typeof(List<object?>));
    private static readonly MethodInfo ReadArgument = typeof(List<object?>).GetProperty("Item")!.GetMethod!;

    private bool _canCache = true;
    private List<object?>? _values;

    internal static object? Evaluate(Expression expression, IQueryRegistry registry, ILogger? logger)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var rewriter = new ValueExpressionAccessor();
        var body = rewriter.Visit(expression)!;
        if (!rewriter._canCache)
            return EvaluateUncached(expression);

        var key = new ExpressionKey(body, registry);
        var cache = DataContextCache.ExpressionsCache;
        if (cache.TryGetValue(key, out var cached) && cached is ValueAccessor hit)
            return hit(rewriter._values);

        var accessor = Compile<ValueAccessor>(body);
        cache[key] = accessor;
        var value = accessor(rewriter._values);
        if (logger?.IsEnabled(LogLevel.Trace) ?? false)
            logger.LogTrace("Value expression cache miss. Hash: {Hash}, value: {Value}", key.GetHashCode(), value);
        else if (logger?.IsEnabled(LogLevel.Debug) ?? false)
            logger.LogDebug("Value expression cache miss");

        return value;
    }

    internal static object? EvaluateCollection(Expression expression, IQueryRegistry registry)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var rewriter = new ValueExpressionAccessor();
        var body = rewriter.Visit(expression)!;
        if (!rewriter._canCache)
            return EvaluateUncached(expression);

        var key = new ExpressionKey(body, registry);
        var cache = DataContextCache.InValuesCache;
        if (!cache.TryGetValue(key, out var accessor))
        {
            accessor = Compile<Func<object?, object?>>(body);
            cache[key] = accessor;
        }

        return accessor(rewriter._values);
    }

    private static TDelegate Compile<TDelegate>(Expression body) where TDelegate : Delegate
        => Expression.Lambda<TDelegate>(Expression.Convert(body, typeof(object)), Arguments).Compile();

    private static object? EvaluateUncached(Expression expression)
        => Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();

    public override Expression? Visit(Expression? node)
    {
        if (node is null || !_canCache)
            return node;

        // Only the synthetic Arguments parameter appears in a cache key. Expressions with
        // lexical scopes, registry indices, or unknown nodes still evaluate locally.
        if (node is ConstantExpression or MemberExpression or MethodCallExpression or BinaryExpression
            or UnaryExpression or ConditionalExpression or NewExpression or NewArrayExpression
            or ListInitExpression or MemberInitExpression or DefaultExpression or TypeBinaryExpression)
            return base.Visit(node);

        _canCache = false;
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var values = _values ??= [];
        var access = Expression.Call(ArgumentList, ReadArgument, Expression.Constant(values.Count));
        values.Add(node.Value);
        return Expression.Convert(access, node.Type);
    }
}
