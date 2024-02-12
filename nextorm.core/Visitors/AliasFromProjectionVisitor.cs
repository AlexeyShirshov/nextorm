using System.Linq.Expressions;

namespace nextorm.core;

public class AliasFromProjectionVisitor : ExpressionVisitor
{
    private string? _alias;

    public AliasFromProjectionVisitor()
    {
    }

    public string? Alias { get => _alias; }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression!.Type!.TryGetProjectionDimension(out _))
        {
            _alias = node.Member.Name;
            return node;
        }
        return base.VisitMember(node);
    }
}
