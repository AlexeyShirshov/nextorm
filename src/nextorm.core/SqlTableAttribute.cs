namespace NextORM.Core;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
public class SqlTableAttribute : Attribute
{
    public SqlTableAttribute(string name)
    {
        Name=name;
    }

    public string Name { get; }
}