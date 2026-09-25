using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the SQLite-only surface of <see cref="SqliteFunctions"/> (exposed through
/// <see cref="SqlFunctions.Sqlite"/>): the core scalars, the JSON1 functions/operators/aggregates, the
/// date helpers and the math-extension functions. Rendering is delegated to
/// <see cref="ISqlDialect.SqliteFunctions"/>; a provider that does not opt in fails with a clear
/// message instead of emitting SQL it cannot execute.
/// </summary>
internal static class SqliteFunctionTranslator
{
    // The number of fixed arguments that precede a trailing params array. The C# compiler wraps the
    // variadic arguments in a single NewArrayExpression; ArgumentFlattener turns it back into items.
    private static readonly Dictionary<string, int> VariadicLeading = new(StringComparer.Ordinal)
    {
        [nameof(SqliteFunctions.printf)] = 1,
        [nameof(SqliteFunctions.format)] = 1,
        ["char"] = 0,
        [nameof(SqliteFunctions.json_array)] = 0,
        [nameof(SqliteFunctions.json_object)] = 0,
        [nameof(SqliteFunctions.json_remove)] = 1,
        [nameof(SqliteFunctions.json_array_insert)] = 1,
        [nameof(SqliteFunctions.json_insert)] = 1,
        [nameof(SqliteFunctions.json_replace)] = 1,
        [nameof(SqliteFunctions.json_set)] = 1
    };

    /// <summary>Translates a SQLite-only call; returns <c>false</c> when the call is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqliteFunctions))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.SqliteFunctions is not { } functions || !functions.Supports(name))
            throw new NotSupportedException($"The SQLite function '{name}' is not supported by this provider.");

        var args = VariadicLeading.TryGetValue(name, out var leading)
            ? ArgumentFlattener.Flatten(node.Arguments, leading)
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

        visitor.Builder!.Append(functions.Render(name, rendered));
        return true;
    }
}
