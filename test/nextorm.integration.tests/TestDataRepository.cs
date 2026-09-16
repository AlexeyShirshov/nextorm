using nextorm.core;

namespace nextorm.integration.tests;

public class TestDataRepository(IDataContext dataProvider)
{
    private readonly IDataContext _dataProvider = dataProvider;

    public Entity<ISimpleEntity> SimpleEntity { get; } = dataProvider.Create<ISimpleEntity>();
    public Entity<IComplexEntity> ComplexEntity { get; } = dataProvider.Create<IComplexEntity>();
    public Entity<SimpleEntity> SimpleEntityAsClass { get; } = dataProvider.Create<SimpleEntity>();

    public IDataContext DataProvider => _dataProvider;

    public Entity From(string table) => ((DbContext)_dataProvider).From(table);
    public Entity<TResult> From<TResult>(QueryCommand<TResult> query) => _dataProvider.From(query);
    public Entity<TResult> From<TResult>(Entity<TResult> builder) => _dataProvider.From(builder);
}
