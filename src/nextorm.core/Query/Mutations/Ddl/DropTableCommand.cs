namespace NextORM.Core;

/// <summary>
/// An internal <c>DROP TABLE IF EXISTS</c> command, emitted only as the first half of a materialisation
/// whose <see cref="CreateTableOptions.DropExisting"/> is set. It targets no mapped entity and carries
/// no parameters; identifier quoting and keyword casing mirror the materialisation it precedes.
/// </summary>
internal sealed class DropTableCommand : MutationCommand
{
    /// <summary>Creates a drop command.</summary>
    /// <param name="targetName">The raw (unquoted, unconventioned) target table name.</param>
    /// <param name="quoteIdentifiers">The identifier-quoting override inherited from the source query, or <see langword="null"/> for the context default.</param>
    /// <param name="keywordCase">The keyword-case override inherited from the source query, or <see langword="null"/> for the context default.</param>
    public DropTableCommand(string targetName, bool? quoteIdentifiers = null, KeywordCase? keywordCase = null)
        : base(SqlStatementType.DropTable, typeof(object))
    {
        TargetName = targetName;
        QuoteIdentifiers = quoteIdentifiers;
        KeywordCase = keywordCase;
    }

    /// <summary>The raw target table name, before identifier quoting is applied.</summary>
    public string TargetName { get; }

    /// <summary>The identifier-quoting override of the source query, or <see langword="null"/> for the context default.</summary>
    public bool? QuoteIdentifiers { get; }

    /// <summary>The keyword-case override of the source query, or <see langword="null"/> for the context default.</summary>
    public KeywordCase? KeywordCase { get; }
}
