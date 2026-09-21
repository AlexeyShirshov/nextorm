using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the text-JSON surface of <see cref="SqlServerFunctions"/> (<c>json_value</c>,
/// <c>json_query</c>, <c>json_modify</c>, <c>isjson</c>), where JSON is stored in a plain text column
/// rather than a native JSON type. Only a dialect that opts in with
/// <see cref="ISqlDialect.SupportsTextJson"/> (SQL Server, MySQL/MariaDB) may use these constructs;
/// every other provider rejects them with a clear message.
/// </summary>
internal static class TextJsonSqlTranslator
{
    /// <summary>
    /// Translates a text-JSON function call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqlServerFunctions))
            return false;

        var args = node.Arguments;

        switch (node.Method.Name)
        {
            case nameof(SqlServerFunctions.json_value) when args.Count == 2:
                EmitFunction(visitor, "json_value", args);
                return true;
            case nameof(SqlServerFunctions.json_query) when args.Count == 2:
                EmitFunction(visitor, "json_query", args);
                return true;
            case nameof(SqlServerFunctions.json_modify) when args.Count == 3:
                EmitFunction(visitor, "json_modify", args);
                return true;
            case nameof(SqlServerFunctions.isjson) when args.Count == 1:
                EmitIsJson(visitor, args);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// <c>isjson(value)</c>. T-SQL's <c>ISJSON</c> returns an <c>int</c>, so a predicate context compares
    /// it with 1 and a value context casts it to <c>bit</c> (the only provider that opts into
    /// <see cref="ISqlDialect.SupportsTextJson"/> is SQL Server or MySQL/MariaDB).
    /// </summary>
    private static void EmitIsJson(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsTextJson)
            throw new NotSupportedException(
                "The text JSON functions (json_value/json_query/json_modify/isjson) are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(args[0]);

        visitor.Builder!.Append(visitor.Dialect.MakeIsJson(value, visitor.IsPredicateContext));
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsTextJson)
            throw new NotSupportedException(
                "The text JSON functions (json_value/json_query/json_modify/isjson) are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeTextJsonFunction(sqlName, rendered));
    }
}
