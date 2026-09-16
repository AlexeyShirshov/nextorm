using System.Linq.Expressions;

public class ReplaceParameterVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    public ReplaceParameterVisitor(Expression parameter)
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
public class ReplaceConstantVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    public ReplaceConstantVisitor(Expression parameter)
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