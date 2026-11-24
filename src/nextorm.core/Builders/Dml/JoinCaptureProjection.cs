using System.Linq.Expressions;

namespace NextORM.Core;

// Translate the joined selector to the target-only shape used by ordinary batch capture.
internal sealed class JoinCaptureProjection(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
{
    internal static LambdaExpression Rewrite(LambdaExpression projection, Type targetType)
    {
        var source = projection.Parameters[0];
        var target = Expression.Parameter(targetType, "target");
        var body = new JoinCaptureProjection(source, target).Visit(projection.Body);
        return Expression.Lambda(body, target);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is MemberExpression { Expression: var owner, Member.Name: "Item1" } slot
            && owner == source && slot.Type == target.Type)
            return node.Update(target);

        return base.VisitMember(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
        => node == source
            ? throw new NotSupportedException("Joined batch OUTPUT INTO requires an explicit projection of mapped target (Item1) columns.")
            : base.VisitParameter(node);
}
