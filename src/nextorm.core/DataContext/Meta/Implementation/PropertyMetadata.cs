using System.Reflection;

namespace NextORM.Core;

internal class PropertyMetadata : IPropertyMetadata
{
    public required PropertyInfo PropertyInfo { get; init; }
    public required string ColumnName { get; init; }
}