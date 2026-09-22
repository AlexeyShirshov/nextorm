namespace NextORM.Core;

/// <summary>
/// Maps a CLR method to a database scalar function. Apply it to a placeholder method that is only ever
/// referenced inside a query expression (its body is never executed, so <c>=&gt; default!</c> or
/// <c>throw null!</c> is enough) or to the declaring type to map every method by name.
/// <para>
/// The call is translated to <c>[schema.]name(arg1, arg2, ...)</c>. For an instance method the target
/// (<c>node.Object</c>) is emitted as the first argument. The mapped function must already exist in the
/// target database: nextorm only emits a call to it, it does not create it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SqlFunctionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlFunctionAttribute"/> class. The SQL function name
    /// defaults to the CLR method name.
    /// </summary>
    public SqlFunctionAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlFunctionAttribute"/> class with an explicit SQL
    /// function name.
    /// </summary>
    /// <param name="name">The SQL function name to call; must not be <see langword="null"/>.</param>
    public SqlFunctionAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The SQL function name. Defaults to the CLR method name when it is not provided (or when the
    /// attribute is applied to the declaring type).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Optional schema/owner prefix, rendered as <c>schema.name</c>.</summary>
    public string? Schema { get; set; }
}
