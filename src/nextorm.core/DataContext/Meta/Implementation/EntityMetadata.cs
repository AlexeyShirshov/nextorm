namespace NextORM.Core;

internal class EntityMetadata : IEntityMetadata
{
    private readonly IReadOnlyList<IPropertyMetadata> _propertiesMeta;

    public EntityMetadata(string? tableName, IReadOnlyList<IPropertyMetadata> propertiesMetas, bool isTableNameAuto = false, IPropertyMetadata? dynamicColumnsStore = null)
    {
        _propertiesMeta = propertiesMetas;
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        DynamicColumnsStore = dynamicColumnsStore;
    }

    public IReadOnlyList<IPropertyMetadata> Properties => _propertiesMeta;
    public string? TableName { get; }
    public bool IsTableNameAuto { get; }
    public IPropertyMetadata? DynamicColumnsStore { get; }
}
