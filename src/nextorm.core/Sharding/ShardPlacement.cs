namespace NextORM.Core;

/// <summary>Describes the physical schema and table used for one shard.</summary>
/// <param name="Schema">The schema name, or <see langword="null"/> to keep the entity mapping.</param>
/// <param name="TableName">The table name, or <see langword="null"/> to append the shard id to the base table.</param>
public readonly record struct ShardPlacement(string? Schema, string? TableName);
