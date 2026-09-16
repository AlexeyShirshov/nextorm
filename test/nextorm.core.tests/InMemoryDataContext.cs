namespace nextorm.core.tests;

public class InMemoryRepository
{
    private readonly IDataContext _dataProvider;

    public InMemoryRepository(IDataContext dataProvider)
    {
        SimpleEntity = dataProvider.Create<SimpleEntity>();
        _dataProvider = dataProvider;
    }
    public IDataContext DataProvider => _dataProvider;
    public Entity<SimpleEntity> SimpleEntity { get; set; }

}