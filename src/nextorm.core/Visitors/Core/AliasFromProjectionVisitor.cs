using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Derives column aliases from a projection expression.
/// </summary>
public class AliasFromProjectionVisitor : ExpressionVisitor
{
    // Resolved alias per member: repeated visits to the same projection member must not re-probe the
    // custom attribute. ConditionalWeakTable avoids rooting the member's declaring assembly.
    private static readonly ConditionalWeakTable<MemberInfo, string> _aliases = new();

    private string? _alias;

    /// <summary>Initializes a new instance of the <see cref="AliasFromProjectionVisitor"/> class.</summary>
    public AliasFromProjectionVisitor()
    {
    }

    /// <summary>The alias derived from the visited projection member, or <c>null</c> when none was found.</summary>
    public string? Alias { get => _alias; }

    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression!.Type!.TryGetProjectionDimension(out _))
        {
            _alias = ResolveAlias(node.Member);
            return node;
        }
        return base.VisitMember(node);
    }

    private static string ResolveAlias(MemberInfo member)
    {
        if (_aliases.TryGetValue(member, out var cached))
            return cached;

        return _aliases.GetValue(member, static m => ResolveAliasCore(m));
    }

    private static string ResolveAliasCore(MemberInfo member)
    {
        // Engine projection members are named "Item1".."Item8"; their trailing digits are the 1-based
        // position, which maps to the deterministic table alias ("t1".."t8"). The cheap ItemN parse runs
        // first, and only a different shape (a generated alias such as "Buyer"/"Approver") probes the
        // JoinSlotAttribute that carries the same position.
        if (ProjectionAliasCache.TryParseItemPosition(member.Name, out var position))
            return DefaultAliasProvider.GetAliasName(position);

        if (Attribute.GetCustomAttribute(member, typeof(JoinSlotAttribute)) is JoinSlotAttribute slot)
            return DefaultAliasProvider.GetAliasName(slot.Position);

        return member.Name;
    }
}
