namespace NextORM.Core;

internal class EntityMetadata : IEntityMetadata
{
    private readonly IReadOnlyList<IPropertyMetadata> _propertiesMeta;

    public EntityMetadata(string? tableName, IReadOnlyList<IPropertyMetadata> propertiesMetas)
    {
        _propertiesMeta = propertiesMetas;
        TableName = tableName;
    }

    public IReadOnlyList<IPropertyMetadata> Properties => _propertiesMeta;
    public string? TableName { get; }
}
