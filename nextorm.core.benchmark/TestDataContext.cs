using nextorm.core;

namespace nextorm.core.benchmark;

public class TestDataContext : SqlDataContext
{
    public TestDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<ISimpleEntity>();
    }
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}
}