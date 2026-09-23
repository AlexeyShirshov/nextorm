namespace NextORM.Core;

/// <summary>
/// A multi-table <c>DELETE</c> command: the target entity's rows are removed when they match a
/// prepared joined query. The target is the first table of the join chain; the join conditions and the
/// optional <c>WHERE</c> are carried by <see cref="Source"/>. Rendered natively per provider:
/// PostgreSQL <c>DELETE ... USING</c>, SQL Server/MySQL/MariaDB <c>DELETE &lt;alias&gt; FROM ... JOIN</c>.
/// </summary>
internal sealed class DeleteJoinCommand : MutationCommand
{
    /// <summary>Creates a delete-by-join command.</summary>
    /// <param name="targetType">The CLR type of the target table whose rows are deleted (the first join source).</param>
    /// <param name="source">The prepared joined query whose source, joins and condition drive the delete.</param>
    public DeleteJoinCommand(Type targetType, QueryCommand source)
        : base(SqlStatementType.DeleteJoin, targetType)
    {
        Source = source;
    }

    /// <summary>The prepared joined query: its <c>FROM</c> is the target, its joins and condition select the rows to delete.</summary>
    public QueryCommand Source { get; }
}
