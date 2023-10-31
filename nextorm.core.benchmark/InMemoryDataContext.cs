namespace nextorm.core.benchmark;

public class InMemoryDataContext : core.InMemoryDataContext
{
    public InMemoryDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<SimpleEntity>();
    }
    public CommandBuilder<SimpleEntity> SimpleEntity {get;set;}
}