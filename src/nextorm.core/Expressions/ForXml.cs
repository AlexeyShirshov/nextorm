namespace nextorm.core;

/// <summary>How SQL Server shapes the result set when it is rendered as XML.</summary>
public enum ForXmlMode
{
    /// <summary><c>FOR XML RAW</c>: each row becomes a <c>&lt;row&gt;</c> element with the columns as attributes.</summary>
    Raw = 0,
    /// <summary><c>FOR XML AUTO</c>: the table structure drives the element nesting.</summary>
    Auto = 1,
    /// <summary><c>FOR XML EXPLICIT</c>: the query must project a tag/parent hierarchy.</summary>
    Explicit = 2,
    /// <summary><c>FOR XML PATH</c>: the projection aliases drive the structure.</summary>
    Path = 3
}

/// <summary>
/// The trailing <c>FOR XML</c> clause of a statement (SQL Server): the output mode plus the optional
/// row element name (<c>RAW('name')</c>/<c>PATH('name')</c>), <c>ROOT('name')</c> wrapper and
/// <c>ELEMENTS</c> flag.
/// </summary>
/// <param name="Mode">Which <c>FOR XML</c> shape is emitted.</param>
/// <param name="ElementName">The row element name for <c>RAW</c>/<c>PATH</c>, or <c>null</c>.</param>
/// <param name="Root">An optional <c>ROOT('name')</c> wrapper around the document.</param>
/// <param name="Elements">Whether column values become child elements (<c>ELEMENTS</c>) instead of attributes.</param>
public readonly record struct ForXmlClause(ForXmlMode Mode, string? ElementName = null, string? Root = null, bool Elements = false);
