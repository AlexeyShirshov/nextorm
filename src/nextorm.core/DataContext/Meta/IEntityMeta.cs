namespace nextorm.core;

/// <summary>
/// Read-only mapping metadata for an entity type: the mapped table and its properties.
/// </summary>
/// <remarks>
/// <c>Meta</c> is an abbreviation that is inconsistent with <see cref="EntityMetadataBuilder{T}"/>;
/// the recommended name is <c>IEntityMetadata</c>.
/// See <c>API-NAMING-REVIEW.md</c> finding P1-11.
/// </remarks>
public interface IEntityMeta
{
    IReadOnlyList<IPropertyMeta> Properties { get; }
    string? TableName { get; }
}
