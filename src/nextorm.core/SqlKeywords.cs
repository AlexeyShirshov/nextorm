using System.Collections.Concurrent;

namespace NextORM.Core;

/// <summary>
/// Applies the configured <see cref="KeywordCase"/> to a SQL keyword token or a fragment made only of
/// keywords and separators. Pass lower-case keyword text; do not pass identifiers, literals, or function
/// names. Provider capability renderers can use this helper to apply the same casing to their clauses.
/// </summary>
public static class SqlKeywords
{
    private static readonly ConcurrentDictionary<string, string> UpperCache = new(StringComparer.Ordinal);

    /// <summary>Returns <paramref name="text"/> unchanged for <see cref="KeywordCase.Lower"/> and its invariant upper-case form otherwise.</summary>
    public static string Of(KeywordCase @case, string text)
        => @case == KeywordCase.Upper ? UpperCache.GetOrAdd(text, static t => t.ToUpperInvariant()) : text;
}
