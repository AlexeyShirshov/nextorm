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
        [nameof(SqliteFunctions.json_set)] = 1,
        [nameof(SqliteFunctions.FTS5bm25)] = 1
    };

    // FTS names whose first argument is a trusted constant table/alias token, emitted as a quoted
    // identifier (never a parameter or a bare literal): the FTS auxiliary functions take the FTS table
    // as their first operand. Match accepts either such a token or a mapped column, so it is handled apart.
    private static readonly HashSet<string> FtsVerbatimFirstArgument = new(StringComparer.Ordinal)
    {
        nameof(SqliteFunctions.FTS5bm25),
        nameof(SqliteFunctions.Highlight),
        nameof(SqliteFunctions.Snippet),
        nameof(SqliteFunctions.RowId),
        nameof(SqliteFunctions.FTS3Offsets),
        nameof(SqliteFunctions.FTS3MatchInfo),
        nameof(SqliteFunctions.FTS3Snippet),
        nameof(SqliteFunctions.Rank)
    };

    // Internal render key for the FTS3/4 rank form; the renderer's switch (SqliteFunctionRenderer)
    // reserves the same spelling. Duplicated as a literal because core does not reference the provider.
    private const string Fts3RankName = "fts3_rank";

    /// <summary>Translates a SQLite-only call; returns <c>false</c> when the call is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqliteFunctions))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.SqliteFunctions is not { } functions || !functions.Supports(name))
            throw new NotSupportedException($"The SQLite function '{name}' is not supported by this provider.");

        // The FTS5 hidden-column rank (string table) and the FTS3/4 UDF rank over matchinfo (byte[])
        // share the CLR name, so the render key is selected by the parameter type, not by the name.
        var renderName = name == nameof(SqliteFunctions.Rank) && node.Method.GetParameters()[0].ParameterType == typeof(byte[])
            ? Fts3RankName
            : name;

        var args = VariadicLeading.TryGetValue(name, out var leading)
            ? ArgumentFlattener.Flatten(node.Arguments, leading)
            : node.Arguments;

        // Only the FTS query surface binds its constant string value arguments. The core scalars, the
        // JSON1 functions and their aggregates keep rendering constant strings as escaped SQL literals
        // (e.g. printf('%d', 1), json_extract(x, '$.a')); parameterising those broke exact-form SQL
        // generation. Match is the one FTS name outside the auxiliary set.
        var isFts = name == nameof(SqliteFunctions.Match) || FtsVerbatimFirstArgument.Contains(name);

        var rendered = visitor.IsParamMode ? null : new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            // The FTS auxiliary functions and the FTS5 rank take the FTS table as their first argument;
            // it is a trusted constant token, emitted as one quoted identifier (never a parameter or a
            // bare literal that could read as SQL).
            if (i == 0 && renderName != Fts3RankName && FtsVerbatimFirstArgument.Contains(name))
            {
                if (rendered is null)
                    visitor.Visit(args[0]);
                else
                    rendered[0] = QuoteTableToken(visitor, name, args[0]);

                continue;
            }

            // Match takes either a mapped column expression or such a trusted table/alias token; any
            // other first operand has no meaningful SQL rendering and is rejected at translation.
            if (i == 0 && name == nameof(SqliteFunctions.Match))
            {
                if (TryTableToken(args[0], out var token))
                {
                    if (rendered is not null)
                        rendered[0] = visitor.Dialect.QuoteIdentifier(token);
                    continue;
                }

                if (IsMappedColumn(visitor, args[0]))
                {
                    if (rendered is null)
                        visitor.Visit(args[0]);
                    else
                        rendered[0] = visitor.VisitToString(args[0]);
                    continue;
                }

                throw new NotSupportedException(
                    $"The first argument of the SQLite function '{name}' must be a mapped column or a constant non-empty table/alias token; a computed value is not accepted.");
            }

            // FTS value positions (search query, matchinfo format, snippet/highlight markers): a
            // constant string is bound as a parameter so it never becomes a raw SQL literal and never
            // needs quote escaping. Non-FTS scalar/JSON members fall through to literal rendering below.
            if (isFts && args[i] is ConstantExpression { Value: string constant })
            {
                var placeholder = visitor.EmitStableStringParameter(constant);
                if (rendered is not null)
                    rendered[i] = placeholder!;
                continue;
            }

            if (rendered is null)
                visitor.Visit(args[i]);
            else
                rendered[i] = visitor.VisitToString(args[i]);
        }

        if (rendered is not null)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(functions.Render(renderName, rendered));
        }

        return true;
    }

    private static string QuoteTableToken(BaseExpressionVisitor visitor, string name, Expression expression)
    {
        if (TryTableToken(expression, out var token))
            return visitor.Dialect.QuoteIdentifier(token);

        throw new NotSupportedException(
            $"The first argument of the SQLite function '{name}' must be a constant non-empty table/alias token; a computed value is not accepted.");
    }

    // A trusted constant table/alias token: a non-null, non-empty string literal. It is rendered as a
    // quoted identifier (with the dialect doubling an embedded delimiter), so a token carrying quotes,
    // semicolons or comment text stays one identifier instead of becoming SQL fragments.
    private static bool TryTableToken(Expression expression, out string token)
    {
        if (expression is ConstantExpression { Value: string text } && !string.IsNullOrEmpty(text))
        {
            token = text;
            return true;
        }

        token = string.Empty;
        return false;
    }

    // The Match first operand is a column when it is a member access on the query entity that maps to a
    // physical column; a captured closure value or any computed expression is not a column.
    private static bool IsMappedColumn(BaseExpressionVisitor visitor, Expression expression)
    {
        if (TypeFacts.UnwrapConvert(expression) is not MemberExpression member || member.Expression is null)
            return false;

        var root = member;
        while (root.Expression is MemberExpression inner)
            root = inner;

        if (root.Expression is ConstantExpression)
            return false;

        return member.Expression.Type == visitor.EntityType
            && !string.IsNullOrEmpty(member.Member.GetPropertyColumnName(visitor.Options.NamingConvention));
    }
}
