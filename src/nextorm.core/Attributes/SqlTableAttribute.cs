namespace NextORM.Core;
/// <summary>
/// Maps a class or interface to a database table, overriding the table name that would otherwise be
/// derived from the CLR type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
public class SqlTableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTableAttribute"/> class.
    /// </summary>
    /// <param name="name">The database table name to use for the annotated type.</param>
    public SqlTableAttribute(string name)
    {
        Name=name;
    }

    /// <summary>
    /// The database table name for the annotated type.
    /// </summary>
    public string Name { get; }
}