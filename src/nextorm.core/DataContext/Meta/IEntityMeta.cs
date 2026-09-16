namespace nextorm.core;

public interface IEntityMeta
{
    IReadOnlyList<IPropertyMeta> Properties { get; }
    string? TableName { get; }
}
