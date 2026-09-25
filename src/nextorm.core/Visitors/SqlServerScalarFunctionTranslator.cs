using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the SQL Server-only T-SQL scalar functions of <see cref="SqlServerFunctions"/> (the
/// string functions <c>patindex</c>/<c>quotename</c>/<c>soundex</c>/<c>difference</c>/
/// <c>string_escape</c>/<c>unicode</c>/<c>nchar</c>/<c>format</c>, the trigonometric functions, the
/// date functions <c>datename</c>/<c>date_bucket</c>, the binary/system functions
/// <c>hashbytes</c>/<c>newsequentialid</c> and the SQL/JSON constructors, aggregates and predicates)
/// through the dialect's <see cref="ISqlDialect.SqlServerFunctions"/> renderer.
/// <para>
/// Only SQL Server exposes that renderer; every other provider rejects the members with a clear
/// message instead of emitting SQL it cannot execute. The boolean-returning JSON predicates are
/// materialised through <see cref="ISqlDialect.MakeBooleanPredicate"/> because T-SQL has no boolean
/// type.
/// </para>
/// </summary>
internal static class SqlServerScalarFunctionTranslator
{
    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.patindex), nameof(SqlServerFunctions.quotename),
        nameof(SqlServerFunctions.soundex), nameof(SqlServerFunctions.difference),
        nameof(SqlServerFunctions.string_escape), nameof(SqlServerFunctions.unicode),
        nameof(SqlServerFunctions.nchar), nameof(SqlServerFunctions.format),
        nameof(SqlServerFunctions.acos), nameof(SqlServerFunctions.asin),
        nameof(SqlServerFunctions.atan), nameof(SqlServerFunctions.atn2),
        nameof(SqlServerFunctions.square), nameof(SqlServerFunctions.datename),
        nameof(SqlServerFunctions.date_bucket), nameof(SqlServerFunctions.hashbytes),
        nameof(SqlServerFunctions.newsequentialid), nameof(SqlServerFunctions.json_array),
        nameof(SqlServerFunctions.json_object), nameof(SqlServerFunctions.json_arrayagg),
        nameof(SqlServerFunctions.json_objectagg), nameof(SqlServerFunctions.json_contains),
        nameof(SqlServerFunctions.json_path_exists)
    };

    /// <summary>The functions whose T-SQL form returns an <c>int</c> that has to become a <c>bit</c> value.</summary>
    private static readonly HashSet<string> BooleanFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.json_contains), nameof(SqlServerFunctions.json_path_exists)
    };

    /// <summary>Translates a SQL Server-only scalar call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqlServerFunctions) || !FunctionNames.Contains(node.Method.Name))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.SqlServerFunctions is not { } functions || !functions.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        var args = name is nameof(SqlServerFunctions.json_array) or nameof(SqlServerFunctions.json_object)
            ? ArgumentFlattener.Flatten(node.Arguments, 0)
            : node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return true;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        var expression = functions.Render(name, rendered);

        if (BooleanFunctions.Contains(name))
            expression = visitor.Dialect.MakeBooleanPredicate($"{expression} = 1", visitor.IsPredicateContext);

        visitor.Builder!.Append(expression);
        return true;
    }
}
