
using System.Linq.Expressions;
namespace nextorm.core;

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