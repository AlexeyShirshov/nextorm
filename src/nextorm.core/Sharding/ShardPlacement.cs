namespace NextORM.Core;

/// <summary>Describes how one bucket is realized physically.</summary>
public enum BucketPlacementMode
{
    /// <summary>The bucket becomes a table-name suffix: <c>{mappedTable}_{bucket}</c> in the mapped schema.</summary>
    TableSuffix,

    /// <summary>The bucket becomes its own schema: <c>{mappedSchema}_{bucket}</c> with the mapped table name.</summary>
    SchemaPerBucket
}

/// <summary>Describes the physical schema and table used for one bucket.</summary>
/// <param name="Schema">The schema name, or <see langword="null"/> to keep the entity mapping.</param>
/// <param name="TableName">The table name, or <see langword="null"/> to keep the entity mapping.</param>
public readonly record struct ShardPlacement(string? Schema, string? TableName);
