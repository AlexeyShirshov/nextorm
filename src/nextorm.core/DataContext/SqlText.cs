namespace NextORM.Core;

/// <summary>
/// The canonical line separator of generated SQL text: a single LF (<c>'\n'</c>).
/// </summary>
/// <remarks>
/// Library-generated SQL must not depend on the host platform. <see cref="System.Text.StringBuilder.AppendLine()"/>
/// expands to <see cref="System.Environment.NewLine"/>, which turns every generated statement (and every
/// byte-exact baseline) into <c>CRLF</c> on Windows and <c>LF</c> elsewhere. SQL emission therefore appends
/// this one character explicitly. Only SQL text this library assembles is affected: opaque/raw SQL, literal
/// contents and parameter values are never reflowed.
/// </remarks>
internal static class SqlText
{
    /// <summary>The single line separator emitted into generated SQL.</summary>
    internal const char NewLine = '\n';
}
