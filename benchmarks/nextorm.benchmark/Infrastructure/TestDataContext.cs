using NextORM.Core;

namespace NextORM.Benchmark;

public class TestDataRepository
{
    private readonly IDataContext _dataProvider;

    public TestDataRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.From<SimpleEntity>();
        LargeEntity = dataProvider.From<LargeEntity>();
        ComplexEntity = dataProvider.From<ComplexEntity>();
        _dataProvider = dataProvider;
    }
    public EntityBuilder<SimpleEntity> SimpleEntity { get; set; }
    public EntityBuilder<LargeEntity> LargeEntity { get; set; }
    public EntityBuilder<ComplexEntity> ComplexEntity { get; set; }

    public IDataContext DataContext => _dataProvider;
}