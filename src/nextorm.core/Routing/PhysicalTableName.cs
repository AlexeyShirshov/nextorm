namespace NextORM.Core;

/// <summary>
/// The physical schema and table an entity uses under one mapping scope: the same names the planner
/// writes into a statement. Useful when a statement is assembled as raw SQL outside the query builder.
/// </summary>
/// <param name="Schema">The schema name, or <see langword="null"/> when the entity declares none.</param>
/// <param name="TableName">The table name, never empty for a mapped entity.</param>
public readonly record struct PhysicalTableName(string? Schema, string TableName)
{
    /// <summary>
    /// The mapped names as one qualified name: <c>schema.table</c>, or just <c>table</c> when the entity
    /// declares no schema. The names are returned exactly as the metadata provider produced them; quoting
    /// is the caller's (or the SQL builder's) business.
    /// </summary>
    public string QualifiedName => string.IsNullOrEmpty(Schema) ? TableName : string.Concat(Schema, ".", TableName);

    /// <summary>Returns <see cref="QualifiedName"/>.</summary>
    /// <returns><c>schema.table</c> or <c>table</c>.</returns>
    public override string ToString() => QualifiedName;
}
