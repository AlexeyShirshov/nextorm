using nextorm.core;

namespace nextorm.core.benchmark;

public class TestDataContext : SqlDataContext
{
    public TestDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<ISimpleEntity>();
        LargeEntity = Create<ILargeEntity>();
    }
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}
    public CommandBuilder<ILargeEntity> LargeEntity {get;set;}
}