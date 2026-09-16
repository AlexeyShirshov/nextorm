namespace nextorm.core.tests;

public interface ISimpleEntity
{
    int Id {get;set;}
}

public class SimpleEntity : ISimpleEntity
{
    public int Id {get;set;}
}