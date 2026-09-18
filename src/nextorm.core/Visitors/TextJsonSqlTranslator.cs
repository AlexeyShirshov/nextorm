using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Translates the JSON-as-text surface of <see cref="NORM.NORM_SQL"/> (<c>json_value</c>,
/// <c>json_query</c>, <c>json_modify</c>), where JSON is stored in a plain text column rather than a
/// native JSON type. Only a dialect that opts in with <see cref="ISqlDialect.SupportsTextJson"/>
/// (SQL Server) may use these constructs; every other provider rejects them with a clear message.
/// </summary>
internal static class TextJsonSqlTranslator
{
    /// <summary>
    /// Translates a text-JSON function call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;

        switch (node.Method.Name)
        {
            case nameof(NORM.NORM_SQL.json_value) when args.Count == 2:
                EmitFunction(visitor, "json_value", args);
                return true;
            case nameof(NORM.NORM_SQL.json_query) when args.Count == 2:
                EmitFunction(visitor, "json_query", args);
                return true;
            case nameof(NORM.NORM_SQL.json_modify) when args.Count == 3:
                EmitFunction(visitor, "json_modify", args);
                return true;
            default:
                return false;
        }
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsTextJson)
            throw new NotSupportedException(
                "The text JSON functions (json_value/json_query/json_modify) are not supported by this provider.");

        SqlOperandTranslator.EmitFunction(visitor, sqlName, args);
    }
}
