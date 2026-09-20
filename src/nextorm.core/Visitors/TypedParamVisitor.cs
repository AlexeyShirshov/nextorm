using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Collects <c>SqlFunctions.Parameter&lt;T&gt;(index)</c> placeholders from a condition and exposes the
/// <c>index -&gt; parameter type</c> map. Used to build a strongly typed predicate factory so the
/// in-memory provider does not index/box an <c>object[]</c> on every row.
/// </summary>
internal sealed class ParamCollectorVisitor : ExpressionVisitor
{
    public SortedDictionary<int, Type> Parameters { get; } = new();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(SqlFunctions) && node.Method.Name == nameof(SqlFunctions.Parameter))
        {
            if (node.Arguments.Count == 1 && node.Arguments[0] is ConstantExpression { Value: int idx })
            {
                Parameters[idx] = node.Type;
                return node;
            }
            throw new NotSupportedException($"Unsupported {nameof(SqlFunctions.Parameter)} usage");
        }
        return base.VisitMethodCall(node);
    }
}

/// <summary>
/// Replaces <c>SqlFunctions.Parameter&lt;T&gt;(index)</c> placeholders with pre-created, strongly typed local
/// variables that are assigned once per query (see <see cref="ParamCollectorVisitor"/>).
/// </summary>
internal sealed class ParamLocalSubstitutionVisitor(IReadOnlyDictionary<int, ParameterExpression> locals) : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(SqlFunctions) && node.Method.Name == nameof(SqlFunctions.Parameter))
        {
            if (node.Arguments.Count == 1 && node.Arguments[0] is ConstantExpression { Value: int idx } && locals.TryGetValue(idx, out var local))
                return local;
            throw new NotSupportedException($"Unsupported {nameof(SqlFunctions.Parameter)} usage");
        }
        return base.VisitMethodCall(node);
    }
}
