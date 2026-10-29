namespace NextORM.Core;

/// <summary>
/// Declares a navigation relationship on an entity property. Placed on the navigation property; the
/// kind is derived from the navigation shape (a collection is <see cref="RelationshipKind.OneToMany"/>, a
/// reference is <see cref="RelationshipKind.ManyToOne"/>).
/// </summary>
/// <remarks>
/// The foreign key is declared either as one property name with <see cref="ForeignKey"/> (the legacy
/// single-column form) or as an ordered list of property names with <see cref="ForeignKeys"/>; exactly
/// one of the two forms must be set. <see cref="PrincipalKeys"/> optionally declares an ordered
/// principal key; when it is omitted the principal key is the full key of the principal entity in
/// entity-metadata order. Component pairing is positional and only the underlying mapped CLR types are
/// compared.
/// <para>
/// The attribute and the fluent <c>HasMany</c>/<c>HasOne</c> registration produce the same metadata
/// model. When both are used for the same navigation property, the fluent declaration wins and the
/// attribute is ignored. Only declared navigations are excluded from column mapping; no convention is
/// applied. The attribute does not declare one-to-one or many-to-many relationships; those are
/// fluent-only.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class RelationshipAttribute : Attribute
{
    /// <summary>
    /// The name of the single foreign-key property on the dependent side: a property of the related
    /// type for a collection navigation (the declaring type is the principal), or a property of the
    /// declaring type for a reference navigation (the declaring type is the dependent). Mutually
    /// exclusive with <see cref="ForeignKeys"/>.
    /// </summary>
    public string? ForeignKey { get; set; }

    /// <summary>
    /// The ordered names of the foreign-key properties on the dependent side. Mutually exclusive with
    /// <see cref="ForeignKey"/> and must not be empty, contain duplicates, or name an unmapped member.
    /// The declaration order is the component order used for positional pairing.
    /// </summary>
    public string[]? ForeignKeys { get; set; }

    /// <summary>
    /// The optional ordered names of the principal-key properties. When omitted, the principal key is
    /// the full key of the principal entity in entity-metadata order. When set, the component count
    /// must equal the foreign-key component count and the positional underlying types must match; an
    /// explicit principal key may select mapped non-key properties.
    /// </summary>
    public string[]? PrincipalKeys { get; set; }
}
