namespace NextORM.Core;

/// <summary>
/// The cardinality of a declared navigation relationship.
/// </summary>
/// <remarks>
/// A relationship is declared explicitly on both sides (fluent <c>HasMany</c>/<c>HasOne</c> or
/// <see cref="RelationshipAttribute"/>); the kind is derived from the navigation shape
/// (collection or reference) and is not inferred from conventions.
/// </remarks>
public enum RelationshipKind
{
    /// <summary>
    /// A collection navigation on the principal side, for example <c>Parent.Children</c>. The foreign
    /// key lives on the related (dependent) type.
    /// </summary>
    OneToMany,

    /// <summary>
    /// A reference navigation on the dependent side, for example <c>Child.Parent</c>. The foreign key
    /// lives on the declaring (dependent) type.
    /// </summary>
    ManyToOne,

    /// <summary>
    /// A reference navigation whose foreign key is unique. Uniqueness is not validated by the core; the
    /// declaration is trusted. Declarable through the fluent <c>HasOneToOne</c> or the model.
    /// </summary>
    OneToOne,

    /// <summary>
    /// Two collection navigations joined through a junction. Represented by the model but not creatable
    /// through the slice-A fluent API; loading it is rejected in a later slice.
    /// </summary>
    ManyToMany,
}
