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
        "acos", "acosh", "asin", "asinh", "atan", "atan2", "atanh", "cosh", "degrees",
        "log10", "log2", "mod", "pi", "radians", "sinh", "tanh",
        "timediff", "unixepoch", "julianday",
        "json", "jsonb", "json_extract", "json_get", "json_get_text", "json_array",
        "json_array_insert", "json_insert", "json_replace", "json_set", "json_object",
        "json_patch", "json_pretty", "json_quote", "json_remove", "json_type", "json_valid",
        "json_group_array", "json_group_object"
    };

    /// <inheritdoc/>
    public bool Supports(string name) => SupportedNames.Contains(name);

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) => name switch
    {
        "json_get" => $"({args[0]} -> {args[1]})",
        "json_get_text" => $"({args[0]} ->> {args[1]})",
        _ when SupportedNames.Contains(name) => $"{name}({string.Join(", ", args)})",
        _ => throw new NotSupportedException($"The {name} function is not supported by SQLite.")
    };
}
