namespace nextorm.core;

/// <summary>How SQL Server shapes the result set when it is rendered as JSON.</summary>
public enum ForJsonMode
{
    /// <summary><c>FOR JSON PATH</c>: the projection's column aliases drive the JSON structure.</summary>
    Path = 0,
    /// <summary><c>FOR JSON AUTO</c>: the queried table structure drives the nesting.</summary>
    Auto = 1
}

/// <summary>
/// The trailing <c>FOR JSON</c> clause of a statement (SQL Server): the output mode plus the optional
/// <c>ROOT</c> wrapper name and the <c>INCLUDE_NULL_VALUES</c> flag.
/// </summary>
/// <param name="Mode">Whether the output is shaped by the projection aliases (<c>PATH</c>) or the table structure (<c>AUTO</c>).</param>
/// <param name="Root">An optional <c>ROOT('name')</c> wrapper around the JSON document.</param>
/// <param name="IncludeNullValues">Whether null-valued properties are emitted (<c>INCLUDE_NULL_VALUES</c>).</param>
public readonly record struct ForJsonClause(ForJsonMode Mode, string? Root = null, bool IncludeNullValues = false);
