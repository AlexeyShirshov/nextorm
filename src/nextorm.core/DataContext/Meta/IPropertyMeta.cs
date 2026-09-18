using System.Reflection;

namespace nextorm.core;

/// <summary>
/// Read-only mapping metadata for a single entity property: its CLR member and target column.
/// </summary>
/// <remarks>
/// <c>Meta</c> is an abbreviation that is inconsistent with the surrounding metadata types; the
/// recommended name is <c>IPropertyMetadata</c>.
/// See <c>API-NAMING-REVIEW.md</c> finding P1-11.
/// </remarks>
public interface IPropertyMeta
{
    PropertyInfo PropertyInfo { get; }
    string ColumnName { get; }
}