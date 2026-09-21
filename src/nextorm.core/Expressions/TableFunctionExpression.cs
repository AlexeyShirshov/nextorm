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
        : this(name, schema, null, call)
    {
    }

    /// <summary>Creates a table-function source, optionally with a trailing <c>WITH (...)</c> clause body.</summary>
    public TableFunctionExpression(string name, string? schema, string? withClause, MethodCallExpression call)
    {
        Name = name;
        Schema = schema;
        WithClause = withClause;
        Call = call;
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

    /// <summary>The CLR method call the FROM source was created from; its arguments are the TVF arguments.</summary>
    public MethodCallExpression Call { get; }

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

        return new TableFunctionExpression(name, attribute.Schema, attribute.WithClause, call);
    }
}
