namespace NextORM.Core;

/// <summary>
/// Declares a navigation relationship on an entity property. Placed on the navigation property; the
/// kind is derived from the navigation shape (a collection is <see cref="RelationshipKind.OneToMany"/>, a
/// reference is <see cref="RelationshipKind.ManyToOne"/>) and <see cref="ForeignKey"/> names the
/// foreign-key property on the dependent side.
/// </summary>
/// <remarks>
/// The attribute and the fluent <c>HasMany</c>/<c>HasOne</c> registration produce the same metadata
/// model. When both are used for the same navigation property, the fluent declaration wins and the
/// attribute is ignored. Only declared navigations are excluded from column mapping; no convention is
/// applied.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class RelationshipAttribute : Attribute
{
    /// <summary>
    /// The name of the foreign-key property on the dependent side: a property of the related type for a
    /// collection navigation (the declaring type is the principal), or a property of the declaring type
    /// for a reference navigation (the declaring type is the dependent).
    /// </summary>
    public string? ForeignKey { get; set; }
}
