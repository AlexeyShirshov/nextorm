using nextorm.core;

namespace nextorm.benchmark;

public class InMemoryDataRepository
{
    private readonly IDataContext _dataProvider;

    public InMemoryDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.Create<SimpleEntity>();
        _dataProvider = dataProvider;
    }
    public Entity<SimpleEntity> SimpleEntity { get; set; }

    public IDataContext DataProvider => _dataProvider;
}