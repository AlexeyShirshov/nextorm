namespace NextORM.Core;

/// <summary>
/// How a <c>FROM</c> source should be rendered: whether it needs an alias, its entity type, whether
/// joins follow it, the table hints to apply and the temporal clause (if any). Bundled so
/// <see cref="SqlSourceRenderer.MakeFrom"/> takes one value instead of a trailing list of flags.
/// </summary>
/// <param name="NeedAlias">Whether the source must be aliased.</param>
/// <param name="EntityType">Entity (or projection) type used to register the source columns.</param>
/// <param name="HasJoins">Whether joins follow the source, which changes how columns are registered.</param>
/// <param name="TableHints">Table-level hints rendered after the table name, or <c>null</c>.</param>
/// <param name="Temporal">The <c>FOR SYSTEM_TIME</c> clause rendered before the alias, or <c>null</c>.</param>
internal readonly record struct FromRenderOptions(
    bool NeedAlias,
    Type? EntityType,
    bool HasJoins,
    IReadOnlyList<string>? TableHints = null,
    TemporalClause? Temporal = null);
