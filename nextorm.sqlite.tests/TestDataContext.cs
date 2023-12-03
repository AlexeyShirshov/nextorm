using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestDataRepository(IDataContext dataProvider)
{
    private readonly IDataContext _dataProvider = dataProvider;

    public Entity<ISimpleEntity> SimpleEntity { get; set; } = dataProvider.Create<ISimpleEntity>();
    public Entity<IComplexEntity> ComplexEntity { get; set; } = dataProvider.Create<IComplexEntity>();
    public Entity<SimpleEntity> SimpleEntityAsClass { get; set; } = dataProvider.Create<SimpleEntity>();

    public IDataContext DataProvider => _dataProvider;

    public CommandBuilder From(string table) => (_dataProvider as DbContext).From(table);
    public Entity<TResult> From<TResult>(QueryCommand<TResult> query) => _dataProvider.From(query);
    public Entity<TResult> From<TResult>(Entity<TResult> builder) => _dataProvider.From(builder);
}