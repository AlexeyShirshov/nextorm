using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Replaces each parameter expression whose type matches the replacement expression's type with the
/// replacement expression.
/// </summary>
public class ReplaceParameterExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    /// <summary>
    /// Creates a visitor that replaces each <see cref="ParameterExpression"/> whose type matches
    /// <paramref name="parameter"/>'s type with <paramref name="parameter"/>.
    /// </summary>
    /// <param name="parameter">The replacement expression.</param>
    public ReplaceParameterExpressionVisitor(Expression parameter)
    {
        _parameter = parameter;
    }
    /// <inheritdoc/>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameter.Type == node.Type)
            return _parameter;

        return base.VisitParameter(node);
    }
}
/// <summary>
/// Replaces each constant expression whose type matches the replacement expression's type with the
/// replacement expression.
/// </summary>
/// <remarks>
/// Renamed from <c>ReplaceConstantVisitor</c> so the suffix is <c>ExpressionVisitor</c>; the singular
/// <c>Constant</c> distinguishes it from <see cref="ReplaceConstantsExpressionVisitor"/> in
/// <c>ExpressionExtensions.cs</c>. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-14.
/// </remarks>
public class ReplaceConstantExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _parameter;

    /// <summary>
    /// Creates a visitor that replaces each <see cref="ConstantExpression"/> whose type matches
    /// <paramref name="parameter"/>'s type with <paramref name="parameter"/>.
    /// </summary>
    /// <param name="parameter">The replacement expression.</param>
    public ReplaceConstantExpressionVisitor(Expression parameter)
    {
        _parameter = parameter;
    }
    /// <inheritdoc/>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_parameter.Type == node.Type)
            return _parameter;

        return base.VisitConstant(node);
    }
}