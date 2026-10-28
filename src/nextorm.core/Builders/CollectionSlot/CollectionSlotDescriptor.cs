using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// One recognized collection-valued projection slot (#249 / T353): a destination member or constructor
/// argument of the projected shape, the mapped navigation it materializes from, and the adapter pipeline the
/// projection asked for. The descriptor is recursive by construction — a slot that projects a nested shape
/// carries that shape's own <see cref="Nested"/> descriptors — so the recognizer and the binder handle any
/// depth through one code path.
/// </summary>
internal sealed class CollectionSlotDescriptor
{
    /// <summary>The destination member on the projected shape, or <see langword="null"/> for a constructor argument.</summary>
    internal MemberInfo? Member { get; set; }

    /// <summary>The destination member's index in the constructor argument list when it is not a property binding.</summary>
    internal int ArgumentIndex { get; set; } = -1;

    /// <summary>
    /// Whether this slot is a <em>reference</em> edge (N:1 or 1:1) rather than a collection. A reference slot
    /// observes exactly one related identity per parent through the eager-loading reference route, which is
    /// where the projection-scoped cardinality guard lives.
    /// </summary>
    internal bool IsReference { get; init; }

    /// <summary>
    /// The entity type the slot's navigation is declared on: the projected entity at depth one, the parent
    /// slot's element type at every deeper level.
    /// </summary>
    internal required Type ParentType { get; init; }

    /// <summary>The declared CLR type of the destination (one of the seven supported collection shapes).</summary>
    internal required Type SlotType { get; init; }

    /// <summary>The element type the slot finally carries (the navigation element, or the adapter's projection type).</summary>
    internal required Type ElementType { get; init; }

    /// <summary>The navigation member the slot reads, rooted at the projection parameter.</summary>
    internal required Expression Navigation { get; init; }

    /// <summary>The projection parameter the navigation is rooted at.</summary>
    internal required ParameterExpression Root { get; init; }

    /// <summary>The navigation element type (the entity the eager-load declaration loads).</summary>
    internal required Type ChildType { get; init; }

    /// <summary>The declared relationship path, used in messages and in the plan fingerprint.</summary>
    internal required string Path { get; init; }

    /// <summary>The adapter operations, in pipeline order. Empty for a direct slot.</summary>
    internal IReadOnlyList<CollectionSlotOperation> Operations { get; init; } = [];

    /// <summary>
    /// The nested descriptors of the adapter projection shape, empty when the slot projects its element as-is.
    /// Each nested slot is rooted at the <c>Select(...)</c> lambda parameter, so it belongs to the child level.
    /// </summary>
    internal IReadOnlyList<CollectionSlotDescriptor> Nested { get; init; } = [];

    /// <summary>
    /// The expression nodes the nested descriptors replace inside the <c>Select(...)</c> lambda body,
    /// aligned with <see cref="Nested"/> by position.
    /// </summary>
    internal IReadOnlyList<Expression> NestedNodes { get; init; } = [];

    /// <summary>Whether the slot came from an adapter chain rather than a direct navigation member.</summary>
    internal bool IsAdapter => Operations.Count > 0;

    /// <summary>The structural identity this slot contributes to the plan key.</summary>
    internal string Fingerprint
    {
        get
        {
            var operations = string.Join(",", Operations.Select(static op => op.Fingerprint));
            var nested = string.Join(";", Nested.Select(static slot => slot.Fingerprint));
            var kind = IsReference ? "ref" : "col";
            return $"SLOT:{ParentType.Name}.{Path}|{kind}|{Describe(SlotType)}|{Describe(ElementType)}|{operations}|{nested}";
        }
    }

    private static string Describe(Type type)
        => type.IsArray ? type.GetElementType()!.Name + "[]" : type.Name;
}

/// <summary>One recorded adapter operation (#249 / T353), applied in memory after the entities materialize.</summary>
internal abstract class CollectionSlotOperation
{
    /// <summary>The plan-key fingerprint of this operation: its kind and shape, never a captured value.</summary>
    internal abstract string Fingerprint { get; }
}

/// <summary>A recorded <c>Where</c> predicate over the navigation element.</summary>
internal sealed class CollectionSlotWhere(LambdaExpression predicate) : CollectionSlotOperation
{
    internal LambdaExpression Predicate { get; } = predicate;

    internal override string Fingerprint => "where(" + Predicate + ")";
}

/// <summary>A recorded ordering key: the primary <c>OrderBy</c> and every subsequent <c>ThenBy</c>.</summary>
internal sealed class CollectionSlotOrder : CollectionSlotOperation
{
    internal CollectionSlotOrder(LambdaExpression selector, bool descending, bool thenBy)
    {
        Selector = selector;
        Descending = descending;
        ThenBy = thenBy;
    }

    internal LambdaExpression Selector { get; }

    internal bool Descending { get; }

    internal bool ThenBy { get; }

    internal override string Fingerprint => (ThenBy ? "then" : "order") + (Descending ? "Desc" : "Asc") + "(" + Selector + ")";
}

/// <summary>A recorded per-parent <c>Limit</c>. The count is evaluated at execution, never folded into the key.</summary>
internal sealed class CollectionSlotLimit(Expression count) : CollectionSlotOperation
{
    internal Expression Count { get; } = count;

    internal override string Fingerprint => "limit";
}

/// <summary>A recorded <c>Select</c> projecting the navigation element into the slot's final shape.</summary>
internal sealed class CollectionSlotSelect(LambdaExpression projection, Type resultType) : CollectionSlotOperation
{
    internal LambdaExpression Projection { get; } = projection;

    internal Type ResultType { get; } = resultType;

    internal override string Fingerprint => "select(" + Projection + ")";
}
