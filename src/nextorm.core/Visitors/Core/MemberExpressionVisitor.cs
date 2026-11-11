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
    /// <summary>The member referenced by the visited member expression, or <c>null</c> when none was found.</summary>
    public MemberInfo? MemberInfo => _mi;

    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        _mi = node.Member;
        return base.VisitMember(node);
    }
}