namespace NextORM.Core;

/// <summary>
/// A <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c> command: the raw target table name, the query whose
/// rows materialise it, and the statement options. Unlike the other mutations it targets no mapped
/// entity, so <see cref="MutationCommand.EntityType"/> only carries the query's result type for
/// diagnostics and the naming convention is never applied to <see cref="TargetName"/>.
/// </summary>
internal sealed class CreateTableAsCommand : MutationCommand
{
    /// <summary>Creates a materialisation command.</summary>
    /// <param name="resultType">The CLR row type the source query produces.</param>
    /// <param name="targetName">The raw (unquoted, unconventioned) target table name.</param>
    /// <param name="temporary">Whether the target is a temporary table.</param>
    /// <param name="source">The query whose rows materialise the table.</param>
    /// <param name="options">The statement options.</param>
    public CreateTableAsCommand(Type resultType, string targetName, bool temporary, QueryCommand source, CreateTableAsOptions options)
        : base(SqlStatementType.CreateTableAsSelect, resultType)
    {
        TargetName = targetName;
        Temporary = temporary;
        Source = source;
        Options = options;
    }

    /// <summary>The raw target table name, before identifier quoting is applied.</summary>
    public string TargetName { get; }

    /// <summary>Whether the target is a temporary (session-scoped) table.</summary>
    public bool Temporary { get; }

    /// <summary>The query whose rows materialise the table.</summary>
    public QueryCommand Source { get; }

    /// <summary>The statement options.</summary>
    public CreateTableAsOptions Options { get; }
}
