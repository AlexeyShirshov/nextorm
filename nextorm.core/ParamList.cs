namespace nextorm.core;

public class ParamList : List<Param>
{
    
}

public class Param
{
    public string Name { get; set; }
    public object? Value { get; set; }
}