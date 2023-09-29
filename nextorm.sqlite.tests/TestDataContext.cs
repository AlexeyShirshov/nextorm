using nextorm.core;

namespace nextorm.sqlite.tests;

public class TestDataContext : SqlDataContext
{
    public TestDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        SimpleEntity = Create<ISimpleEntity>();
        ComplexEntity = Create<IComplexEntity>();
        SimpleEntityAsClass = Create<SimpleEntity>();
    }
    public CommandBuilder<ISimpleEntity> SimpleEntity {get;set;}
    public CommandBuilder<IComplexEntity> ComplexEntity {get;set;}
    public CommandBuilder<SimpleEntity> SimpleEntityAsClass {get;set;}
}