namespace NextORM.Core;

/// <summary>
/// Immutable metadata for the junction (link) entity of a many-to-many relationship: the junction type
/// and the four mapped key members that connect the two principal sides through the junction.
/// </summary>
/// <remarks>
/// The two principal sides each declare an ordered key (<see cref="ParentKey"/>, <see cref="ChildKey"/>);
/// the junction declares the two foreign keys pointing at them (<see cref="JunctionParentForeignKey"/>,
/// <see cref="JunctionChildForeignKey"/>). Every list keeps its declaration/selector order, and each
/// junction foreign-key component at position <c>i</c> pairs positionally with the component at
/// position <c>i</c> of the key it references; the two lists of a leg have the same arity after
/// resolution. An instance is published through <see cref="IRelationshipMetadata.Junction"/> and read
/// concurrently.
/// </remarks>
public sealed class RelationshipJunctionMetadata
{
    /// <summary>Initializes the junction metadata from its resolved mapped members.</summary>
    /// <param name="junctionType">The CLR type of the junction entity.</param>
    /// <param name="parentKey">The parent-side key members, resolved from the parent entity metadata.</param>
    /// <param name="childKey">The child-side key members, resolved from the child entity metadata.</param>
    /// <param name="junctionParentForeignKey">The junction members that reference the parent key.</param>
    /// <param name="junctionChildForeignKey">The junction members that reference the child key.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RelationshipJunctionMetadata(
        Type junctionType,
        IReadOnlyList<IPropertyMetadata> parentKey,
        IReadOnlyList<IPropertyMetadata> childKey,
        IReadOnlyList<IPropertyMetadata> junctionParentForeignKey,
        IReadOnlyList<IPropertyMetadata> junctionChildForeignKey)
    {
        ArgumentNullException.ThrowIfNull(junctionType);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(childKey);
        ArgumentNullException.ThrowIfNull(junctionParentForeignKey);
        ArgumentNullException.ThrowIfNull(junctionChildForeignKey);

        JunctionType = junctionType;
        ParentKey = parentKey;
        ChildKey = childKey;
        JunctionParentForeignKey = junctionParentForeignKey;
        JunctionChildForeignKey = junctionChildForeignKey;
    }

    /// <summary>The CLR type of the junction (link) entity.</summary>
    public Type JunctionType { get; }

    /// <summary>The parent-side key members, resolved from the parent entity metadata.</summary>
    public IReadOnlyList<IPropertyMetadata> ParentKey { get; }

    /// <summary>The child-side key members, resolved from the child entity metadata.</summary>
    public IReadOnlyList<IPropertyMetadata> ChildKey { get; }

    /// <summary>The junction members that reference <see cref="ParentKey"/>.</summary>
    public IReadOnlyList<IPropertyMetadata> JunctionParentForeignKey { get; }

    /// <summary>The junction members that reference <see cref="ChildKey"/>.</summary>
    public IReadOnlyList<IPropertyMetadata> JunctionChildForeignKey { get; }
}
