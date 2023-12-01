using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestDataRepository
{
    private readonly IDataContext _dataProvider;

    public TestDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.Create<ISimpleEntity>();
        ComplexEntity = dataProvider.Create<IComplexEntity>();
        SimpleEntityAsClass = dataProvider.Create<SimpleEntity>();
        _dataProvider = dataProvider;
    }
    public Entity<ISimpleEntity> SimpleEntity { get; set; }
    public Entity<IComplexEntity> ComplexEntity { get; set; }
    public Entity<SimpleEntity> SimpleEntityAsClass { get; set; }

    public IDataContext DataProvider => _dataProvider;

    public CommandBuilder From(string table) => (_dataProvider as DbContext).From(table);
    public Entity<TResult> From<TResult>(QueryCommand<TResult> query) => _dataProvider.From(query);
    public Entity<TResult> From<TResult>(Entity<TResult> builder) => _dataProvider.From(builder);
}