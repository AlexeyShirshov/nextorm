using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestRepository : Repository
{
    private readonly CommandBuilder<SimpleEntity> _simpleEntity;
    public TestRepository(TestDataContext ctx)
    {
        _simpleEntity = ctx.Create<SimpleEntity>(config =>
        {
            //config.HasKey(e=>new {e.Id});
            config.Property(e => e.Id).HasColumnName("id");
        });
    }
    public override CommandBuilder<SimpleEntity> SimpleEntity => _simpleEntity;
}