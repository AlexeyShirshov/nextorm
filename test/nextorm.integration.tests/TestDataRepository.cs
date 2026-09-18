using nextorm.core;

namespace nextorm.integration.tests;

public class TestDataRepository(IDataContext dataProvider)
{
    private readonly IDataContext _dataProvider = dataProvider;

    public EntityBuilder<ISimpleEntity> SimpleEntity { get; } = dataProvider.From<ISimpleEntity>();
    public EntityBuilder<IComplexEntity> ComplexEntity { get; } = dataProvider.From<IComplexEntity>();
    public EntityBuilder<BinaryEntity> BinaryEntity { get; } = dataProvider.From<BinaryEntity>();
    public EntityBuilder<SimpleEntity> SimpleEntityAsClass { get; } = dataProvider.From<SimpleEntity>();

    public IDataContext DataProvider => _dataProvider;

    public EntityBuilder From(string table) => ((DbContext)_dataProvider).From(table);
    public EntityBuilder<TResult> From<TResult>(QueryCommand<TResult> query) => _dataProvider.From(query);
    public EntityBuilder<TResult> From<TResult>(EntityBuilder<TResult> builder) => _dataProvider.From(builder);
}
