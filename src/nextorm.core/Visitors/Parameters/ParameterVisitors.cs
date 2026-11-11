
using System.Linq.Expressions;
namespace NextORM.Core;

/// <summary>
/// Detects calls to a small set of special methods while translating a predicate.
/// </summary>
/// <remarks>
/// Internal helper. The former <c>Test</c> prefix no longer leaks into the public surface.
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-15.
/// </remarks>
internal class TestSpecialMethodCallVisitor : ExpressionVisitor
{
    public bool Result { get; internal set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (
               (node.Object?.Type is { } objectType && typeof(CommonFunctions).IsAssignableFrom(objectType))
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
/// Internal helper, renamed from <c>ParamExpressionVisitor2</c> (the trailing <c>2</c> carried no
/// meaning). See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-13.
/// </remarks>
internal class ParameterBinderVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _p;
    private bool _converted;

    public ParameterBinderVisitor(ParameterExpression p)
    {
        _p = p;
        _converted = false;
    }

    public bool Converted => _converted;
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(SqlFunctions) /*&& _tableProvider is IParameterProvider parameterProvider*/)
        {
            var paramIdx = node switch
            {
                {
                    Method.Name: nameof(SqlFunctions.Parameter),
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