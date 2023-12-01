using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestRepository : Repository
{
    private readonly Entity<SimpleEntity> _simpleEntity;
    private readonly IDataContext _dataProvider;

    public TestRepository(IDataContext dataProvider)
    {
        _simpleEntity = dataProvider.Create<SimpleEntity>(config =>
        {
            //config.HasKey(e=>new {e.Id});
            config.Property(e => e.Id).HasColumnName("id");
        });
        _dataProvider = dataProvider;
    }
    public override Entity<SimpleEntity> SimpleEntity => _simpleEntity;

    public IDataContext DataProvider => _dataProvider;
}