using System.Reflection;

namespace NextORM.Core;

internal class PropertyMetadata : IPropertyMetadata
{
    public required PropertyInfo PropertyInfo { get; init; }
    public required string ColumnName { get; init; }
    public required bool IsColumnNameAuto { get; init; }
    public bool IsKey { get; set; }
    public bool IsIdentity { get; init; }
    public bool IsComputed { get; init; }
}
