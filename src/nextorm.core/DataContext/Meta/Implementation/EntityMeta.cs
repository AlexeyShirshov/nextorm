namespace nextorm.core;

internal class EntityMeta : IEntityMeta
{
    private readonly IReadOnlyList<IPropertyMeta> _propertiesMeta;

    public EntityMeta(string? tableName, IReadOnlyList<IPropertyMeta> propertiesMetas)
    {
        _propertiesMeta = propertiesMetas;
        TableName = tableName;
    }

    public IReadOnlyList<IPropertyMeta> Properties => _propertiesMeta;
    public string? TableName { get; }
}
