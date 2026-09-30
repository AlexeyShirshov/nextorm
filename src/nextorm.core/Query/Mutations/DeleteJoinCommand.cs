namespace NextORM.Core;

using System.Linq.Expressions;

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
    /// <param name="returningColumns">The mapped columns the statement returns through <c>RETURNING</c>, or <see langword="null"/> for a plain delete.</param>
    /// <param name="returningProjection">The selector that defines the returned columns, used to render a qualified <c>RETURNING</c> list over the joined sources.</param>
    public DeleteJoinCommand(Type targetType, QueryCommand source, IReadOnlyList<IPropertyMetadata>? returningColumns = null, LambdaExpression? returningProjection = null)
        : base(SqlStatementType.DeleteJoin, targetType)
    {
        Source = source;
        ReturningColumns = returningColumns;
        ReturningProjection = returningProjection;
    }

    /// <summary>The prepared joined query: its <c>FROM</c> is the target, its joins and condition select the rows to delete.</summary>
    public QueryCommand Source { get; }

    /// <summary>
    /// The mapped columns the statement returns through <c>RETURNING</c>, or <see langword="null"/> for
    /// a plain multi-table delete that only reports the affected-row count.
    /// </summary>
    public override IReadOnlyList<IPropertyMetadata>? ReturningColumns { get; }

    /// <summary>The projection whose selected members are rendered as the <c>RETURNING</c> list, or <see langword="null"/>.</summary>
    public LambdaExpression? ReturningProjection { get; }
}
