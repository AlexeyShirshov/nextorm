using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Collects <c>NORM.Param&lt;T&gt;(index)</c> placeholders from a condition and exposes the
/// <c>index -&gt; parameter type</c> map. Used to build a strongly typed predicate factory so the
/// in-memory provider does not index/box an <c>object[]</c> on every row.
/// </summary>
internal sealed class ParamCollectorVisitor : ExpressionVisitor
{
    public SortedDictionary<int, Type> Parameters { get; } = new();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM) && node.Method.Name == nameof(NORM.Param))
        {
            if (node.Arguments.Count == 1 && node.Arguments[0] is ConstantExpression { Value: int idx })
            {
                Parameters[idx] = node.Type;
                return node;
            }
            throw new NotSupportedException($"Unsupported {nameof(NORM.Param)} usage");
        }
        return base.VisitMethodCall(node);
    }
}

/// <summary>
/// Replaces <c>NORM.Param&lt;T&gt;(index)</c> placeholders with pre-created, strongly typed local
/// variables that are assigned once per query (see <see cref="ParamCollectorVisitor"/>).
/// </summary>
internal sealed class ParamLocalSubstitutionVisitor(IReadOnlyDictionary<int, ParameterExpression> locals) : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM) && node.Method.Name == nameof(NORM.Param))
        {
            if (node.Arguments.Count == 1 && node.Arguments[0] is ConstantExpression { Value: int idx } && locals.TryGetValue(idx, out var local))
                return local;
            throw new NotSupportedException($"Unsupported {nameof(NORM.Param)} usage");
        }
        return base.VisitMethodCall(node);
    }
}
