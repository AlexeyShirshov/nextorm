namespace nextorm.core;

public class ParamList : List<Param>
{
    
}

public class Param
{
    public Param(string name, object? value)
    {
        Name= name;
        Value = value;
    }
    public string Name { get; set; }
    public object? Value { get; set; }
}