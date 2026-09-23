namespace NextORM.Core;

/// <summary>
/// A key-upsert command: <c>INSERT ... ON CONFLICT ... DO UPDATE</c>, <c>INSERT ... ON DUPLICATE KEY
/// UPDATE</c> or <c>MERGE</c>, depending on the active dialect. The rows are the would-be inserted rows
/// (the same shape as an <see cref="InsertCommand"/>); the dialect renders the matched branch from
/// <see cref="Keys"/> and <see cref="UpdateColumns"/>.
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
    public MergeCommand(
        Type entityType,
        string tableName,
        bool isTableNameAuto,
        IReadOnlyList<InsertColumn> columns,
        int rowCount,
        IReadOnlyList<IPropertyMetadata> keys,
        IReadOnlyList<IPropertyMetadata> updateColumns)
        : base(SqlStatementType.Merge, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Columns = columns;
        RowCount = rowCount;
        Keys = keys;
        UpdateColumns = updateColumns;
    }

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
