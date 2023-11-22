using System.Reflection;

namespace nextorm.core;

internal class PropertyMeta : IPropertyMeta
{
    public required PropertyInfo PropertyInfo { get; init; }
    public required string ColumnName { get; init; }
}