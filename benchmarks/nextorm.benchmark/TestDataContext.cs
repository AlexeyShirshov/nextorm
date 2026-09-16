using nextorm.core;

namespace nextorm.benchmark;

public class TestDataRepository
{
    private readonly IDataContext _dataProvider;

    public TestDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.Create<SimpleEntity>();
        LargeEntity = dataProvider.Create<LargeEntity>();
        _dataProvider = dataProvider;
    }
    public Entity<SimpleEntity> SimpleEntity { get; set; }
    public Entity<LargeEntity> LargeEntity { get; set; }

    public IDataContext DbContext => _dataProvider;
}