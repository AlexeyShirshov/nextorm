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
    /// A reference navigation whose foreign key is unique. The uniqueness is a declaration-time trust:
    /// the core neither inspects the schema or the data nor imposes a uniqueness constraint, and a
    /// non-unique foreign key is accepted when the metadata is built. The only guard is per parent at
    /// load time: two distinct non-null child identities for one parent throw
    /// <see cref="InvalidOperationException"/> at materialization, while repeated identical rows and
    /// keyless/null-identity repeats are tolerated (the first child wins). Declarable through the fluent
    /// <c>HasOneToOne</c> or the model.
    /// </summary>
    OneToOne,

    /// <summary>
    /// A many-to-many relationship between collection navigations. Fluent declarations use
    /// <c>HasManyThrough</c> with an explicit junction entity.
    /// </summary>
    ManyToMany,
}
