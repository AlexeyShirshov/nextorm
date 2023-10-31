namespace nextorm.core.benchmark;

public class InMemoryDataContext : core.InMemoryDataContext
{
    public InMemoryDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create((InMemoryDataContext ctx)=>ctx.SimpleEntity);
    }
    public InMemoryCommandBuilder<SimpleEntity> SimpleEntity {get;set;}
}