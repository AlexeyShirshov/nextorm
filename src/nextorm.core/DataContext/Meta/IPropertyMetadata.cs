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
    PropertyInfo PropertyInfo { get; }
    string ColumnName { get; }
}