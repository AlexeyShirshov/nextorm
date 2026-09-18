using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Replaces the query parameters referenced inside an expression.
/// </summary>
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
/// <summary>
/// Replaces constant nodes inside an expression.
/// </summary>
/// <remarks>
/// The singular name is inconsistent with <see cref="ReplaceConstantsExpressionVisitor"/> in
/// <c>ExpressionExtensions.cs</c>; see <c>API-NAMING-REVIEW.md</c> finding P1-14.
/// </remarks>
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