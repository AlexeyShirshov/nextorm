using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// A table-valued function call used as a FROM source. The SQL name/schema are resolved once from
/// <see cref="SqlTableFunctionAttribute"/> (on the method or its declaring type); the arguments are
/// kept as expressions and rendered/parameterised by <see cref="SqlBuilder"/>.
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

    /// <summary>SQL function name (after applying <see cref="SqlTableFunctionAttribute.Name"/>).</summary>
    public string Name { get; }

    /// <summary>Optional schema/owner prefix, rendered as <c>schema.name</c>.</summary>
    public string? Schema { get; }

    /// <summary>
    /// Optional trailing <c>WITH (...)</c> clause body (from
    /// <see cref="SqlTableFunctionAttribute.WithClause"/>), appended verbatim after the call.
    /// </summary>
    public string? WithClause { get; }

    /// <summary>
    /// Optional verbatim SQL appended inside the call parentheses after the arguments (from
    /// <see cref="SqlTableFunctionAttribute.CallClause"/>).
    /// </summary>
    public string? CallClause { get; }

    /// <summary>
    /// Indices of the arguments emitted verbatim as SQL identifiers (from
    /// <see cref="SqlTableFunctionAttribute.VerbatimArguments"/>); <c>null</c> when every argument is a value.
    /// </summary>
    public IReadOnlyList<int>? VerbatimArguments { get; }

    /// <summary>The CLR method call the FROM source was created from; its arguments are the TVF arguments.</summary>
    public MethodCallExpression Call { get; }

    /// <summary>
    /// The mapped row type (the <c>T</c> of the placeholder method's <c>IQueryable&lt;T&gt;</c> return
    /// type) whose metadata supplies the result schema when
    /// <see cref="ResultSchema"/> is not <see cref="TableFunctionSchema.None"/>.
    /// </summary>
    public Type? ResultType { get; }

    /// <summary>
    /// Where the caller-declared result schema is rendered, or <see cref="TableFunctionSchema.None"/>
    /// when the function supplies its own result shape. See <see cref="SqlTableFunctionAttribute.ResultSchema"/>.
    /// </summary>
    public TableFunctionSchema ResultSchema { get; }

    /// <summary>The TVF argument expressions, in declaration order.</summary>
    public IReadOnlyList<Expression> Arguments => Call.Arguments;

    /// <summary>
    /// Resolves <paramref name="call"/> into a table function, throwing when the method (or its
    /// declaring type) is not annotated with <see cref="SqlTableFunctionAttribute"/>.
    /// </summary>
    public static TableFunctionExpression Create(MethodCallExpression call)
    {
        ArgumentNullException.ThrowIfNull(call);

        var method = call.Method;
        var attribute = method.GetCustomAttribute<SqlTableFunctionAttribute>(inherit: false)
            ?? method.DeclaringType?.GetCustomAttribute<SqlTableFunctionAttribute>(inherit: false)
            ?? throw new NotSupportedException(
                $"Method '{method.DeclaringType?.Name}.{method.Name}' is not mapped as a table-valued function. Apply [SqlTableFunction] to the method or its declaring type.");

        var name = string.IsNullOrEmpty(attribute.Name) ? method.Name : attribute.Name;
        var resultType = attribute.ResultSchema == TableFunctionSchema.None ? null : ResolveResultType(method);

        return new TableFunctionExpression(name, attribute.Schema, attribute.WithClause, attribute.CallClause, attribute.VerbatimArguments, call, resultType, attribute.ResultSchema);
    }

    // The row type is the sole generic argument of the IQueryable<T> return type. A closed generic
    // method call and a plain IQueryable<T> return both expose it; anything else is a programming
    // error in the placeholder declaration.
    private static Type ResolveResultType(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            return returnType.GetGenericArguments()[0];

        throw new NotSupportedException(
            $"The table function '{method.DeclaringType?.Name}.{method.Name}' declares a result schema but does not return IQueryable<T>.");
    }
}
