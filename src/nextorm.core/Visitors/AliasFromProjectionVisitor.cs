using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Derives column aliases from a projection expression.
/// </summary>
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
            _alias = ResolveAlias(node.Member.Name);
            return node;
        }
        return base.VisitMember(node);
    }

    private static string ResolveAlias(string memberName)
    {
        // Projection members are named "Item1".."Item8"; their trailing digits are the 1-based
        // position, which maps to the deterministic table alias ("t1".."t8").
        var digitsStart = memberName.Length;
        while (digitsStart > 0 && char.IsAsciiDigit(memberName[digitsStart - 1])) digitsStart--;
        if (digitsStart == memberName.Length) return memberName;

        var position = 0;
        for (var i = digitsStart; i < memberName.Length; i++) position = position * 10 + (memberName[i] - '0');
        return DefaultAliasProvider.GetAliasName(position);
    }
}
