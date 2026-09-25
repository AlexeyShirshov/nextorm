namespace NextORM.Core;

/// <summary>
/// Where the result-schema of a table function is rendered, when it is derived from the mapped row
/// type (<see cref="SqlTableFunctionAttribute.ResultSchema"/>). The listed providers recognize the
/// corresponding native form; a provider that cannot express it rejects the source with a
/// <see cref="System.NotSupportedException"/>.
/// </summary>
public enum TableFunctionSchema
{
    /// <summary>The schema is not rendered (the default; the function supplies its own result shape).</summary>
    None = 0,
    /// <summary>
    /// A quoted structure string is emitted as the leading call argument, before the arguments declared
    /// on the placeholder method. ClickHouse <c>values('a UInt8, b String', (1,'x'), ...)</c>.
    /// </summary>
    LeadingArgument = 1,
    /// <summary>
    /// A column-definition list is emitted after the source alias, as
    /// <c>as x(col type, ...)</c>. PostgreSQL <c>jsonb_to_record(json) AS x(a int, b text)</c>.
    /// </summary>
    AliasColumnList = 2,
}

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
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTableFunctionAttribute"/> class. The SQL function
    /// name defaults to the CLR method name.
    /// </summary>
    public SqlTableFunctionAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTableFunctionAttribute"/> class with an explicit
    /// SQL function name.
    /// </summary>
    /// <param name="name">The SQL table function name to call; must not be <see langword="null"/>.</param>
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

    /// <summary>
    /// When not <see cref="TableFunctionSchema.None"/>, the function's result schema is derived from
    /// the mapped row type (the <c>T</c> of the placeholder method's <c>IQueryable&lt;T&gt;</c> return
    /// type) and rendered as a column-definition list. Every readable property must be mapped with
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.ColumnAttribute"/> (or a fluent mapping)
    /// and its CLR type is translated to the
    /// provider's native type. The rows themselves still materialize into that same row type, exactly
    /// like any other table function. The schema is part of the query plan key, so two row shapes never
    /// share a cached plan. See <see cref="TableFunctionSchema"/>.
    /// </summary>
    public TableFunctionSchema ResultSchema { get; set; }
}
