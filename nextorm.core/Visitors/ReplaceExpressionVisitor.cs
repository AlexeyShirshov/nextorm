using System.Linq.Expressions;

public class ReplaceExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    public ReplaceExpressionVisitor(Expression parameter)
    {
        _parameter = parameter;
    }
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameter.Type == node.Type)
            return _parameter;

        return base.VisitParameter(node);
    }
}
public class ReplaceConstantExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    public ReplaceConstantExpressionVisitor(Expression parameter)
    {
        _parameter = parameter;
    }
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_parameter.Type == node.Type)
            return _parameter;

        return base.VisitConstant(node);
    }
}