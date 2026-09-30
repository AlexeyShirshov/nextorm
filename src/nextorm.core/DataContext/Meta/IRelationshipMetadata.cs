using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Read-only metadata for a declared navigation relationship between two entity types. A relationship
/// is declared explicitly (fluent <c>HasMany</c>/<c>HasOne</c> or <see cref="RelationshipAttribute"/>);
/// the core does not infer relationships from conventions.
/// </summary>
/// <remarks>
/// Both sides of a relationship are declared independently and symmetrically; the inverse is not
/// inferred. When both sides are declared and agree on the foreign key and the participating types,
/// <see cref="Inverse"/> points at the other side's metadata; otherwise it is <see langword="null"/>.
/// A key-only declaration (<c>HasMany</c>/<c>HasOne</c> taking explicit foreign- and principal-key
/// selectors, for an entity without a navigation property) has a <see langword="null"/>
/// <see cref="Navigation"/>.
/// </remarks>
public interface IRelationshipMetadata
{
    /// <summary>
    /// The cardinality of the relationship.
    /// </summary>
    RelationshipKind Kind { get; }

    /// <summary>
    /// The entity type that declares <see cref="Navigation"/>.
    /// </summary>
    Type DeclaringType { get; }

    /// <summary>
    /// The entity type the navigation points to.
    /// </summary>
    Type RelatedType { get; }

    /// <summary>
    /// The navigation property on <see cref="DeclaringType"/>; a collection for
    /// <see cref="RelationshipKind.OneToMany"/>, a reference otherwise. <see langword="null"/> for a
    /// key-only relationship declared without a navigation property.
    /// </summary>
    PropertyInfo? Navigation { get; }

    /// <summary>
    /// Whether <see cref="Navigation"/> is a collection (<see cref="RelationshipKind.OneToMany"/>).
    /// <see langword="false"/> when there is no navigation.
    /// </summary>
    bool IsCollection { get; }

    /// <summary>
    /// The foreign-key properties on the dependent side, resolved from that type's entity metadata.
    /// </summary>
    /// <exception cref="NotSupportedException">A foreign-key property is not mapped on the dependent type, or its bound type does not match <see cref="PrincipalKey"/>.</exception>
    IReadOnlyList<IPropertyMetadata> ForeignKey { get; }

    /// <summary>
    /// The principal-key properties of the principal side (the properties flagged with
    /// <see cref="IPropertyMetadata.IsKey"/> via <c>[Key]</c>, <c>.Key()</c>, or the <c>Id</c>/
    /// <c>&lt;TypeName&gt;Id</c> convention), resolved from that type's entity metadata.
    /// </summary>
    /// <exception cref="NotSupportedException">The principal type declares no key, or a bound type does not match <see cref="ForeignKey"/>.</exception>
    IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }

    /// <summary>
    /// The inverse relationship declared on <see cref="RelatedType"/>, or <see langword="null"/> when the
    /// other side has not been declared or does not converge on the same foreign key.
    /// </summary>
    IRelationshipMetadata? Inverse { get; }

    /// <summary>
    /// The junction descriptor of a <see cref="RelationshipKind.ManyToMany"/> relationship, or
    /// <see langword="null"/> for every other kind and for a many-to-many relationship that was declared
    /// without a junction. The default implementation returns <see langword="null"/> so existing external
    /// implementations keep compiling.
    /// </summary>
    RelationshipJunctionMetadata? Junction => null;
}
