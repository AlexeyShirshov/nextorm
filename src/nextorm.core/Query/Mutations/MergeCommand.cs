namespace NextORM.Core;

using System.Linq.Expressions;

/// <summary>Which class of candidate row a <see cref="MergeBranch"/> handles.</summary>
internal enum MergeMatchKind
{
    /// <summary>A target row matched by the source (<c>WHEN MATCHED</c>).</summary>
    Matched,
    /// <summary>A source row with no matching target row (<c>WHEN NOT MATCHED [BY TARGET]</c>).</summary>
    NotMatchedByTarget,
    /// <summary>A target row with no matching source row (<c>WHEN NOT MATCHED BY SOURCE</c>).</summary>
    NotMatchedBySource,
}

/// <summary>The action a <see cref="MergeBranch"/> performs.</summary>
internal enum MergeActionKind
{
    /// <summary><c>UPDATE SET ...</c>.</summary>
    Update,
    /// <summary><c>INSERT (...) VALUES (...)</c>.</summary>
    Insert,
    /// <summary><c>DELETE</c>.</summary>
    Delete,
    /// <summary><c>DO NOTHING</c> (PostgreSQL only).</summary>
    Nothing,
}

/// <summary>
/// One conditional branch of a full <c>MERGE</c>: the match class it handles and the action it performs.
/// The written columns of an update/insert branch are assigned from the source row by name.
/// </summary>
internal sealed class MergeBranch
{
    /// <summary>Creates a merge branch.</summary>
    /// <param name="match">The class of candidate row this branch handles.</param>
    /// <param name="action">The action performed on the row.</param>
    /// <param name="columns">The written columns (empty for a delete branch).</param>
    /// <param name="condition">An extra <c>AND</c> search condition over <c>(target, source)</c>, or <see langword="null"/>.</param>
    public MergeBranch(MergeMatchKind match, MergeActionKind action, IReadOnlyList<IPropertyMetadata> columns, LambdaExpression? condition = null)
    {
        Match = match;
        Action = action;
        Columns = columns;
        Condition = condition;
    }

    /// <summary>The class of candidate row this branch handles.</summary>
    public MergeMatchKind Match { get; }

    /// <summary>The action performed on the row.</summary>
    public MergeActionKind Action { get; }

    /// <summary>The written columns (empty for a delete branch).</summary>
    public IReadOnlyList<IPropertyMetadata> Columns { get; }

    /// <summary>
    /// An extra search condition rendered as <c>WHEN ... AND &lt;condition&gt;</c> over the
    /// <c>(target, source)</c> row, or <see langword="null"/> for an unconditional branch.
    /// </summary>
    public LambdaExpression? Condition { get; }
}

/// <summary>
/// A merge command: either a key upsert (<c>INSERT ... ON CONFLICT ... DO UPDATE</c>,
/// <c>INSERT ... ON DUPLICATE KEY UPDATE</c> or a two-branch <c>MERGE</c>) or a full <c>MERGE</c> with an
/// arbitrary set of <see cref="Branches"/>. The rows are the would-be inserted rows (the same shape as an
/// <see cref="InsertCommand"/>); the matched branch assigns the non-key columns from the incoming row.
/// </summary>
internal sealed class MergeCommand : MutationCommand
{
    /// <summary>Creates a key-upsert command.</summary>
    /// <param name="entityType">The CLR entity type being upserted.</param>
    /// <param name="tableName">The mapped (still unconventioned, unquoted) table name.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    /// <param name="columns">The written columns (key and non-key), in insert order.</param>
    /// <param name="rowCount">The number of source rows.</param>
    /// <param name="keys">The match-key columns.</param>
    /// <param name="updateColumns">The non-key columns assigned from the source on a match.</param>
    /// <param name="branches">The full-<c>MERGE</c> branches, or <see langword="null"/> for a key upsert.</param>
    /// <param name="returningColumns">The columns to return through <c>OUTPUT</c>/<c>RETURNING</c>, or <see langword="null"/>.</param>
    /// <param name="source">The server-side <c>SELECT</c> supplying the rows (<c>USING (&lt;select&gt;) AS source</c>), or <see langword="null"/> for a <c>VALUES</c> source.</param>
    /// <param name="matchCondition">An explicit <c>ON &lt;condition&gt;</c> over <c>(target, source)</c>, or <see langword="null"/> to match on <see cref="Keys"/>.</param>
    /// <param name="registry">A <see cref="QueryCommand"/> carrier used to render condition expressions (the target entity's command), or <see langword="null"/>.</param>
    public MergeCommand(
        Type entityType,
        string tableName,
        bool isTableNameAuto,
        IReadOnlyList<InsertColumn> columns,
        int rowCount,
        IReadOnlyList<IPropertyMetadata> keys,
        IReadOnlyList<IPropertyMetadata> updateColumns,
        IReadOnlyList<MergeBranch>? branches = null,
        IReadOnlyList<IPropertyMetadata>? returningColumns = null,
        QueryCommand? source = null,
        LambdaExpression? matchCondition = null,
        QueryCommand? registry = null)
        : base(SqlStatementType.Merge, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Columns = columns;
        RowCount = rowCount;
        Keys = keys;
        UpdateColumns = updateColumns;
        Branches = branches;
        ReturningColumns = returningColumns;
        Source = source;
        MatchCondition = matchCondition;
        Registry = registry;
    }

    /// <summary>
    /// The full-<c>MERGE</c> branches, or <see langword="null"/> for a key upsert. When set, the dialect
    /// renders a general <c>MERGE</c> instead of a native upsert.
    /// </summary>
    public IReadOnlyList<MergeBranch>? Branches { get; }

    /// <summary>The mapped columns returned through <c>OUTPUT</c>/<c>RETURNING</c>, or <see langword="null"/>.</summary>
    public override IReadOnlyList<IPropertyMetadata>? ReturningColumns { get; }

    /// <summary>
    /// The server-side <c>SELECT</c> that supplies the source rows
    /// (<c>MERGE ... USING (&lt;select&gt;) AS source</c>), or <see langword="null"/> for a <c>VALUES</c>
    /// derived source built from <see cref="Columns"/>.
    /// </summary>
    public QueryCommand? Source { get; }

    /// <summary>
    /// An explicit <c>ON &lt;condition&gt;</c> match over the <c>(target, source)</c> row, or
    /// <see langword="null"/> to match on <see cref="Keys"/> (<c>target.k = source.k</c>).
    /// </summary>
    public LambdaExpression? MatchCondition { get; }

    /// <summary>
    /// A <see cref="QueryCommand"/> carrier the condition renderer uses as its query registry (the
    /// target entity's command). Present even for a <c>VALUES</c> source, which has no query of its own.
    /// </summary>
    public QueryCommand? Registry { get; }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }

    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }

    /// <summary>The written columns (both key and non-key), in insert order, with their per-row values.</summary>
    public IReadOnlyList<InsertColumn> Columns { get; }

    /// <summary>The number of source rows.</summary>
    public int RowCount { get; }

    /// <summary>The match-key columns used to locate an existing row.</summary>
    public IReadOnlyList<IPropertyMetadata> Keys { get; }

    /// <summary>The non-key columns assigned from the source on a match, in insert order.</summary>
    public IReadOnlyList<IPropertyMetadata> UpdateColumns { get; }
}
