using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Read-only mapping metadata for a single entity property: its CLR member and target column.
/// </summary>
/// <remarks>
/// Renamed from <c>IPropertyMeta</c> for consistency with the surrounding metadata types.
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-11.
/// </remarks>
public interface IPropertyMetadata
{
    /// <summary>
    /// The CLR property this mapping describes.
    /// </summary>
    PropertyInfo PropertyInfo { get; }
    /// <summary>
    /// The column that <see cref="PropertyInfo"/> maps to.
    /// </summary>
    string ColumnName { get; }

    /// <summary>
    /// Whether <see cref="ColumnName"/> was derived from the property name rather than declared with
    /// an attribute or a fluent mapping. An active <see cref="INamingConvention"/> is applied only to
    /// auto names. The default implementation returns <see langword="false"/> (treat an unknown
    /// mapping as declared) so existing external implementations keep compiling.
    /// </summary>
    bool IsColumnNameAuto => false;
}