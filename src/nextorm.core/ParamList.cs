namespace nextorm.core;

public class ParamList : List<Param>
{

}

public class Param(string name, object? value)
{
    public string Name { get; set; } = name;
    public object? Value { get; set; } = value;
}