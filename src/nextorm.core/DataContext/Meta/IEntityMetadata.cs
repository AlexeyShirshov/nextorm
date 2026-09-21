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
    IReadOnlyList<IPropertyMetadata> Properties { get; }
    string? TableName { get; }

    /// <summary>
    /// Whether <see cref="TableName"/> was derived from the CLR type name rather than declared with
    /// an attribute or a fluent mapping. An active <see cref="INamingConvention"/> is applied only to
    /// auto names. The default implementation returns <see langword="false"/> (treat an unknown
    /// mapping as declared) so existing external implementations keep compiling.
    /// </summary>
    bool IsTableNameAuto => false;
}
