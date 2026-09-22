namespace NextORM.Core;

/// <summary>
/// Maps a CLR method to a database table-valued function used as a FROM source. Apply it to a
/// placeholder static method that returns <see cref="IQueryable{T}"/> and is only ever referenced
/// inside an expression passed to <see cref="DataContextExtensions.FromTableFunction{T}"/> (its body
/// is never executed, so <c>=&gt; throw new NotSupportedException()</c> or <c>=&gt; default!</c> is
/// enough). Alternatively apply it to the declaring type to map every method by name.
/// <para>
/// The call is translated to <c>[schema.]name(arg1, arg2, ...)</c>. The arguments are rendered through
/// the regular expression visitor, so captured values become parameters exactly like everywhere else.
/// The mapped function must already exist in the target database: nextorm only emits a call to it, it
/// does not create it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SqlTableFunctionAttribute : Attribute
{
    public SqlTableFunctionAttribute()
    {
    }

    public SqlTableFunctionAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The SQL table function name. Defaults to the CLR method name when it is not provided (or when
    /// the attribute is applied to the declaring type).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Optional schema/owner prefix, rendered as <c>schema.name</c>.</summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Optional trailing <c>WITH (...)</c> clause body appended to the rendered call, for table
    /// functions that take an explicit schema definition (for example SQL Server
    /// <c>OPENJSON(json) WITH (col type '$.path', ...)</c>). The value is emitted verbatim inside the
    /// parentheses: <c>WithClause = "name nvarchar(50) '$.name'"</c> renders
    /// <c>openjson(@json) with (name nvarchar(50) '$.name')</c>. The mapped row type still supplies the
    /// CLR types used by the projection.
    /// </summary>
    public string? WithClause { get; set; }

    /// <summary>
    /// Optional verbatim SQL appended inside the call parentheses, after the arguments, for table
    /// functions whose schema is part of the call itself (for example MySQL
    /// <c>JSON_TABLE(doc, path COLUMNS(...))</c>). The value is emitted verbatim, so include your own
    /// leading separator; set <c>CallClause = ",'$[*]' columns(id int path '$.id')"</c> to render
    /// <c>json_table(@doc,'$[*]' columns(id int path '$.id'))</c>. The mapped row type still supplies
    /// the CLR types used by the projection. This is developer-authored SQL only — never build it from
    /// user input.
    /// </summary>
    public string? CallClause { get; set; }

    /// <summary>
    /// Indices of the arguments that must be emitted verbatim as SQL identifiers instead of values
    /// (for example the <c>table</c> and <c>column</c> names of SQL Server
    /// <c>CONTAINSTABLE(table, column, search)</c>). Each such argument must be a constant string and
    /// is rendered unquoted; only pass trusted values.
    /// </summary>
    public int[]? VerbatimArguments { get; set; }
}
