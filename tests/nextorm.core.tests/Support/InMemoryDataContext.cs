namespace NextORM.Core.Tests;

public class InMemoryRepository
{
    private readonly IDataContext _dataProvider;

    public InMemoryRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.From<SimpleEntity>();
        AvgFixture = dataProvider.From<AvgFixture>();
        _dataProvider = dataProvider;
    }
    public IDataContext DataProvider => _dataProvider;
    public EntityBuilder<SimpleEntity> SimpleEntity { get; set; }
    public EntityBuilder<AvgFixture> AvgFixture { get; set; }

}