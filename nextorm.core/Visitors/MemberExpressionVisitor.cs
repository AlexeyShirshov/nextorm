using System.Linq.Expressions;
using System.Reflection;
namespace nextorm.core;
public class MemberExpressionVisitor : ExpressionVisitor
{
    private MemberInfo? _mi;
    public MemberInfo? MemberInfo => _mi;

    protected override Expression VisitMember(MemberExpression node)
    {
        _mi = node.Member;
        return base.VisitMember(node);
    }
}