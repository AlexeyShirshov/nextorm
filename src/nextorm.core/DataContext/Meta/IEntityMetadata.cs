namespace NextORM.Core;

/// <summary>
/// Read-only mapping metadata for an entity type: the mapped table and its properties.
/// </summary>
/// <remarks>
/// Renamed from <c>IEntityMeta</c> (formerly inconsistent with <see cref="EntityMetadataBuilder{T}"/>).
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-11.
/// </remarks>
public interface IEntityMetadata
{
    /// <summary>
    /// The mapped properties of the entity, in the order they were declared or discovered.
    /// </summary>
    IReadOnlyList<IPropertyMetadata> Properties { get; }
    /// <summary>
    /// The mapped table name, or <see langword="null"/> when the entity is not table-mapped.
    /// </summary>
    string? TableName { get; }

    /// <summary>
    /// Whether <see cref="TableName"/> was derived from the CLR type name rather than declared with
    /// an attribute or a fluent mapping. An active <see cref="INamingConvention"/> is applied only to
    /// auto names. The default implementation returns <see langword="false"/> (treat an unknown
    /// mapping as declared) so existing external implementations keep compiling.
    /// </summary>
    bool IsTableNameAuto => false;

    /// <summary>
    /// The property that receives the entity's unmapped columns when a row is read, or
    /// <see langword="null"/> when the entity has none. Declared with
    /// <see cref="DynamicColumnsAttribute"/> or <see cref="EntityPropertyBuilder{T}.DynamicColumnsStore"/>.
    /// The store is excluded from <see cref="Properties"/>; use this member to reach it. The default
    /// implementation returns <see langword="null"/> so existing external implementations keep
    /// compiling.
    /// </summary>
    IPropertyMetadata? DynamicColumnsStore => null;
}
