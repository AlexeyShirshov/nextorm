namespace nextorm.core.benchmark;

public class TestDataRepository
{
    private readonly IDataContext _dataProvider;

    public TestDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.Create<ISimpleEntity>();
        LargeEntity = dataProvider.Create<ILargeEntity>();
        _dataProvider = dataProvider;
    }
    public Entity<ISimpleEntity> SimpleEntity { get; set; }
    public Entity<ILargeEntity> LargeEntity { get; set; }

    public IDataContext DataProvider => _dataProvider;
}