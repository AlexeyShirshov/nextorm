using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A table-valued function call used as a FROM source. The SQL name/schema and the other metadata are
/// snapshotted from the <see cref="TvfEntityBuilder{T}"/> descriptor the factory returned; the arguments
/// are kept as expressions and rendered/parameterised by <c>SqlBuilder</c>.
/// </summary>
public sealed class TableFunctionExpression
{
    private readonly IReadOnlyList<Expression>? _arguments;

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

    private TableFunctionExpression(
        string name,
        MethodCallExpression call,
        IReadOnlyList<Expression> arguments,
        IReadOnlyList<string> argumentNames,
        IReadOnlyList<int> verbatimArguments,
        Type resultType,
        Expression approximateTopK)
        : this(name, null, null, null, verbatimArguments, call, resultType, TableFunctionSchema.None)
    {
        _arguments = arguments;
        ArgumentNames = argumentNames;
        ApproximateTopK = approximateTopK;
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

    private TableFunctionExpression(TableFunctionExpression source, MethodCallExpression call,
        IReadOnlyList<Expression>? arguments, Expression? topK)
        : this(source.Name, source.Schema, source.WithClause, source.CallClause, source.VerbatimArguments, call,
            source.ResultType, source.ResultSchema)
    {
        _arguments = arguments;
        ArgumentNames = source.ArgumentNames;
        ApproximateTopK = topK;
        WithOrdinality = source.WithOrdinality;
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
    public IReadOnlyList<Expression> Arguments => _arguments ?? Call.Arguments;

    /// <summary>Named argument labels for an internal table-function lowering, or <see langword="null"/> for positional calls.</summary>
    internal IReadOnlyList<string>? ArgumentNames { get; }

    /// <summary>The captured TOP value for SQL Server's approximate vector-search form.</summary>
    internal Expression? ApproximateTopK { get; }

    /// <summary>True when this source is the SQL Server approximate VECTOR_SEARCH table construct.</summary>
    internal bool IsApproximateVectorSearch => ApproximateTopK is not null;

    internal bool WithOrdinality { get; private init; }

    internal TableFunctionExpression ForSqlPlanKey(ExpressionVisitor rewriter)
    {
        // The descriptor already resolved factory metadata. Comparison needs the call's method for
        // built-in classification and its argument shape, never an executable receiver.
        var call = Call;
        var receiver = call.Object is { } instance ? Expression.Default(instance.Type) : null;
        call = call.Update(receiver, rewriter.Visit(call.Arguments));
        var topK = rewriter.Visit(ApproximateTopK);
        Expression[]? arguments = null;
        if (_arguments is { } overrides)
        {
            arguments = new Expression[overrides.Count];
            for (var i = 0; i < overrides.Count; i++)
                arguments[i] = rewriter.Visit(overrides[i])!;
        }

        return ReferenceEquals(call, Call) && arguments is null && ReferenceEquals(topK, ApproximateTopK)
            ? this
            : new TableFunctionExpression(this, call, arguments, topK);
    }

    /// <summary>Creates the internal SQL Server VECTOR_SEARCH source with its four named arguments.</summary>
    internal static TableFunctionExpression CreateApproximateVectorSearch(
        string tableName,
        string columnName,
        MethodCallExpression call,
        Expression queryVector,
        string metric,
        Expression topK,
        Type resultType)
    {
        Expression[] arguments =
        [
            Expression.Constant(tableName),
            Expression.Constant(columnName),
            queryVector,
            Expression.Constant(metric)
        ];

        return new TableFunctionExpression(
            "VECTOR_SEARCH",
            call,
            arguments,
            ["TABLE", "COLUMN", "SIMILAR_TO", "METRIC"],
            [0, 1, 3],
            resultType,
            topK);
    }

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
        TableFunctionSchema resultSchema,
        bool withOrdinality = false)
        => new(name, schema, withClause, callClause, verbatimArguments, call, resultType, resultSchema)
        {
            WithOrdinality = withOrdinality
        };
}
