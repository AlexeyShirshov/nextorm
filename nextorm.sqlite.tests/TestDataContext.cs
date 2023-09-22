using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestDataContext : DataContext
{
    public TestDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<ISimpleEntity>();
        ComplexEntity = Create<IComplexEntity>();
    }
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}
    public CommandBuilder<IComplexEntity> ComplexEntity {get;set;}
}