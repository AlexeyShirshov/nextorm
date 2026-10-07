using NextORM.Core;

namespace NextORM.Sqlite;

/// <summary>
/// Renders the SQLite-only surface (<see cref="SqliteFunctions"/>) on SQLite. Every name matches its
/// native SQLite function spelling; the two access variants render the <c>-&gt;</c>/<c>-&gt;&gt;</c>
/// operators, and the <c>params</c> members are flattened by the translator before they arrive here.
/// </summary>
internal sealed class SqliteFunctionRenderer : ISqliteFunctions
{
    internal static readonly SqliteFunctionRenderer Instance = new();

    private static readonly HashSet<string> SupportedNames = new(StringComparer.Ordinal)
    {
        "printf", "format", "hex", "unhex", "random", "randomblob", "quote", "typeof", "glob",
        "unicode", "char", "soundex", "octet_length", "ifnull", "if",
        "acos", "acosh", "asin", "asinh", "atan", "atan2", "atanh", "cosh",
        "log10", "log2", "mod", "sinh", "tanh",
        "timediff", "unixepoch", "julianday",
        "json", "jsonb", "json_extract", "json_get", "json_get_text", "json_array",
        "json_array_insert", "json_insert", "json_replace", "json_set", "json_object",
        "json_patch", "json_pretty", "json_quote", "json_remove", "json_type", "json_valid",
        "json_group_array", "json_group_object",
        // FTS3/4/5 (SQL-only; the query surface renders the hidden columns and the auxiliary functions).
        "Match", "Rank", "RowId", "FTS5bm25", "Highlight", "Snippet",
        "FTS3Offsets", "FTS3MatchInfo", "FTS3Snippet"
    };

    // Internal render key for the FTS3/4 rank form; the translator selects it for the byte[] overload so
    // the same CLR name is not confused with the FTS5 hidden-column form.
    internal const string Fts3RankName = "fts3_rank";

    /// <inheritdoc/>
    public bool Supports(string name) => SupportedNames.Contains(name);

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "json_get" => $"({args[0]} -> {args[1]})",
        "json_get_text" => $"({args[0]} ->> {args[1]})",
        // FTS: MATCH is an infix operator, the FTS5 rank is a hidden column and the FTS3/4 rank is a
        // connection-registered UDF over matchinfo; the remaining names use the native call spelling.
        "Match" => $"{args[0]} MATCH {args[1]}",
        "Rank" => $"{args[0]}.rank",
        Fts3RankName => $"rank({args[0]})",
        "RowId" => $"{args[0]}.rowid",
        "FTS5bm25" => $"bm25({string.Join(", ", args)})",
        "Highlight" => $"highlight({string.Join(", ", args)})",
        "Snippet" => $"snippet({string.Join(", ", args)})",
        "FTS3Offsets" => $"offsets({args[0]})",
        "FTS3MatchInfo" => $"matchinfo({string.Join(", ", args)})",
        "FTS3Snippet" => $"snippet({string.Join(", ", args)})",
        _ when SupportedNames.Contains(name) => $"{name}({string.Join(", ", args)})",
        _ => throw new NotSupportedException($"The {name} function is not supported by SQLite.")
    };
}
