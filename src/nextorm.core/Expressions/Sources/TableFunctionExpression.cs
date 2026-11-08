using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A table-valued function call used as a FROM source. The SQL name/schema and the other metadata are
/// snapshotted from the <see cref="TvfEntityBuilder{T}"/> descriptor the factory returned; the arguments
/// are kept as expressions and rendered/parameterised by <c>SqlBuilder</c>.
/// </summary>
public sealed class TableFunctionExpression
{
    /// <summary>Creates a table-function source without a trailing <c>WITH</c> clause.</summary>
    public TableFunctionExpression(string name, string? schema, MethodCallExpression call)
        : this(name, schema, null, null, null, call)
    {
    }

    /// <summary>Creates a table-function source, optionally with a trailing <c>WITH (...)</c> clause body.</summary>
    public TableFunctionExpression(string name, string? schema, string? withClause, MethodCallExpression call)
        : this(name, schema, withClause, null, null, call)
    {
    }

    /// <summary>Creates a table-function source with an in-call clause and verbatim identifier arguments.</summary>
    internal TableFunctionExpression(string name, string? schema, string? withClause, string? callClause, IReadOnlyList<int>? verbatimArguments, MethodCallExpression call)
        : this(name, schema, withClause, callClause, verbatimArguments, call, null, TableFunctionSchema.None)
    {
    }

    private TableFunctionExpression(string name, string? schema, string? withClause, string? callClause, IReadOnlyList<int>? verbatimArguments, MethodCallExpression call, Type? resultType, TableFunctionSchema resultSchema)
    {
        Name = name;
        Schema = schema;
        WithClause = withClause;
        CallClause = callClause;
        VerbatimArguments = verbatimArguments;
        Call = call;
        ResultType = resultType;
        ResultSchema = resultSchema;
    }

    /// <summary>SQL function name of the runtime descriptor (<see cref="TvfEntityBuilder{T}.Name"/>).</summary>
    public string Name { get; }

    /// <summary>Optional schema/owner prefix, rendered as <c>schema.name</c>.</summary>
    public string? Schema { get; }

    /// <summary>
    /// Optional trailing <c>WITH (...)</c> clause body (the runtime descriptor's
    /// <see cref="TvfEntityBuilder{T}.WithClause"/>), appended verbatim after the call.
    /// </summary>
    public string? WithClause { get; }

    /// <summary>
    /// Optional verbatim SQL appended inside the call parentheses after the arguments (the runtime
    /// descriptor's <see cref="TvfEntityBuilder{T}.CallClause"/>).
    /// </summary>
    public string? CallClause { get; }

    /// <summary>
    /// Indices of the arguments emitted verbatim as SQL identifiers (the runtime descriptor's
    /// <see cref="TvfEntityBuilder{T}.VerbatimArguments"/>); <c>null</c> when every argument is a value.
    /// </summary>
    public IReadOnlyList<int>? VerbatimArguments { get; }

    /// <summary>The SQL method call the FROM source was created from; its arguments are the TVF arguments.</summary>
    public MethodCallExpression Call { get; }

    /// <summary>
    /// The mapped row type supplied by the runtime descriptor whose metadata renders the result schema
    /// when <see cref="ResultSchema"/> is not <see cref="TableFunctionSchema.None"/>.
    /// </summary>
    public Type? ResultType { get; }

    /// <summary>
    /// Where the caller-declared result schema is rendered, or <see cref="TableFunctionSchema.None"/>
    /// when the function supplies its own result shape. See
    /// <see cref="TvfEntityBuilder{T}.ResultSchema"/>.
    /// </summary>
    public TableFunctionSchema ResultSchema { get; }

    /// <summary>The TVF argument expressions, in declaration order.</summary>
    public IReadOnlyList<Expression> Arguments => Call.Arguments;

    /// <summary>
    /// Builds a source node from runtime metadata of a <see cref="TvfEntityBuilder{T}"/> descriptor: the
    /// row type and the result placement come from the descriptor and the arguments stay in
    /// <paramref name="call"/> untouched.
    /// </summary>
    internal static TableFunctionExpression CreateRuntime(
        string name,
        string? schema,
        string? withClause,
        string? callClause,
        IReadOnlyList<int>? verbatimArguments,
        MethodCallExpression call,
        Type resultType,
        TableFunctionSchema resultSchema)
        => new(name, schema, withClause, callClause, verbatimArguments, call, resultType, resultSchema);
}
