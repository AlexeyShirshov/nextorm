namespace NextORM.Core;

/// <summary>Describes how one bucket is realized physically.</summary>
public enum BucketPlacementMode
{
    /// <summary>
    /// The bucket becomes its own schema: the table keeps its mapped name in <c>{mappedSchema}_{bucket}</c>,
    /// or in <c>bucket_{bucket}</c> when the entity declares no schema. This is the default.
    /// </summary>
    SchemaPerBucket = 0,

    /// <summary>The bucket becomes a table-name suffix: <c>{mappedTable}_{bucket}</c> in the mapped schema.</summary>
    TableSuffix = 1
}

/// <summary>Describes the physical schema and table used for one bucket.</summary>
/// <param name="Schema">The schema name, or <see langword="null"/> to keep the entity mapping.</param>
/// <param name="TableName">The table name, or <see langword="null"/> to keep the entity mapping.</param>
public readonly record struct ShardPlacement(string? Schema, string? TableName);
