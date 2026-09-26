using System.Reflection;

namespace NextORM.Core;

internal class EntityMetadata : IEntityMetadata
{
    private readonly IReadOnlyList<IPropertyMetadata> _propertiesMeta;
    private readonly Dictionary<PropertyInfo, IPropertyMetadata> _propertiesByInfo;

    public EntityMetadata(string? tableName, IReadOnlyList<IPropertyMetadata> propertiesMetas, bool isTableNameAuto = false, IPropertyMetadata? dynamicColumnsStore = null)
    {
        _propertiesMeta = propertiesMetas;
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        DynamicColumnsStore = dynamicColumnsStore;

        // Indexed once at construction: property resolution runs per projected column and per
        // comparison operand, so the linear scan is on the prepare hot path.
        var byInfo = new Dictionary<PropertyInfo, IPropertyMetadata>(propertiesMetas.Count);
        for (var i = 0; i < propertiesMetas.Count; i++)
        {
            var property = propertiesMetas[i];
            byInfo.TryAdd(property.PropertyInfo, property);
        }

        _propertiesByInfo = byInfo;
    }

    public IReadOnlyList<IPropertyMetadata> Properties => _propertiesMeta;
    public string? TableName { get; }
    public bool IsTableNameAuto { get; }
    public IPropertyMetadata? DynamicColumnsStore { get; }

    /// <summary>Looks up a mapped property by its CLR member; <see langword="null"/> when it is not mapped.</summary>
    internal IPropertyMetadata? FindProperty(PropertyInfo property)
        => _propertiesByInfo.TryGetValue(property, out var metadata) ? metadata : null;
}
