using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestDataContext : DataContext
{
    public TestDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<ISimpleEntity>();
    }
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}
}