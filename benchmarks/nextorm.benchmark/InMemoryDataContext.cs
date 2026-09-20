using NextORM.Core;

namespace NextORM.Benchmark;

public class InMemoryDataRepository
{
    private readonly IDataContext _dataProvider;

    public InMemoryDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.From<SimpleEntity>();
        _dataProvider = dataProvider;
    }
    public EntityBuilder<SimpleEntity> SimpleEntity { get; set; }

    public IDataContext DataProvider => _dataProvider;
}