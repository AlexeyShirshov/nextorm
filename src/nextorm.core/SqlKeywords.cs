using System.Collections.Concurrent;

namespace NextORM.Core;

/// <summary>
/// Central construction-time helper that resolves a keyword token (or a fragment that is pure
/// keywords and separators) to the configured <see cref="KeywordCase"/>. Emitters must pass the
/// exact lower-case token they would have emitted; identifiers, literals and function names are never
/// routed through here, so the case never touches user data. Exposed so provider capability renderers
/// (which live in their own assembly) can case the clauses they emit.
/// </summary>
public static class SqlKeywords
{
    private static readonly ConcurrentDictionary<string, string> UpperCache = new(StringComparer.Ordinal);

    /// <summary>Returns <paramref name="text"/> unchanged for <see cref="KeywordCase.Lower"/> and its invariant upper-case form otherwise.</summary>
    public static string Of(KeywordCase @case, string text)
        => @case == KeywordCase.Upper ? UpperCache.GetOrAdd(text, static t => t.ToUpperInvariant()) : text;
}
