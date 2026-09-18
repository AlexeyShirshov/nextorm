
using System.Linq.Expressions;
namespace nextorm.core;

/// <summary>
/// Detects calls to a small set of special methods while translating a predicate.
/// </summary>
/// <remarks>
/// Carries a <c>Test</c> prefix despite being production code; consider renaming and making it
/// <c>internal</c>. See <c>API-NAMING-REVIEW.md</c> finding P1-15.
/// </remarks>
public class TestSpecialMethodCallVisitor : ExpressionVisitor
{
    public bool Result { get; internal set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (
               (node.Object?.Type == typeof(NORM.NORM_SQL))
            || (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) ?? false)
            )
        {
            Result = true;
            return node;
        }
        return base.VisitMethodCall(node);
    }
}

/// <summary>
/// Binds the parameters of an expression while a command is built.
/// </summary>
/// <remarks>
/// The trailing <c>2</c> in the name should be replaced by a descriptive name; see
/// <c>API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
public class ParamExpressionVisitor2 : ExpressionVisitor
{
    private readonly ParameterExpression _p;
    private bool _converted;

    public ParamExpressionVisitor2(ParameterExpression p)
    {
        _p = p;
        _converted = false;
    }

    public bool Converted => _converted;
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(NORM) /*&& _tableProvider is IParamProvider paramProvider*/)
        {
            var paramIdx = node switch
            {
                {
                    Method.Name: nameof(NORM.Param),
                    Arguments: [ConstantExpression constExp]
                } => constExp.Value is int i ? i : -1,
                _ => -1
            };

            if (paramIdx >= 0)
            {
                //var paramName = string.Format("norm_p{0}", paramIdx);
                _converted = true;
                return Expression.Convert(Expression.ArrayIndex(_p, Expression.Constant(paramIdx)), node.Type);
            }
            else
                throw new NotSupportedException(node.Method.Name);
        }
        return base.VisitMethodCall(node);
    }
}