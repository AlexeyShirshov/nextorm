using System.Linq.Expressions;
using System.Reflection;
namespace NextORM.Core;
/// <summary>
/// Expression visitor that resolves the <see cref="System.Reflection.MemberInfo"/> referenced by a
/// member expression.
/// </summary>
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