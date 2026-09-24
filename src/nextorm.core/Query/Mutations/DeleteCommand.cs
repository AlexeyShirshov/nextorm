namespace NextORM.Core;

/// <summary>
/// A <c>DELETE</c> command: the target table plus exactly one of the two row filters — a prepared
/// <see cref="Condition"/> (the predicate form <c>Where(...)</c>) or the declared key values
/// (<see cref="Keys"/>, the <c>Delete(entity)</c> form). A command with neither deletes every row
/// (<c>All()</c>).
/// </summary>
internal sealed class DeleteCommand : MutationCommand
{
    /// <summary>Creates a delete command.</summary>
    /// <param name="entityType">The CLR entity type being deleted.</param>
    /// <param name="tableName">The mapped (still unconventioned, unquoted) table name.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    /// <param name="condition">The predicate command whose condition is rendered as the <c>WHERE</c>, or <see langword="null"/> for the key/all-rows forms.</param>
    /// <param name="keys">The declared key values of the key form, or <see langword="null"/> for the predicate/all-rows forms.</param>
    /// <param name="returningColumns">The mapped columns to return through <c>RETURNING</c>/<c>OUTPUT</c>, or <see langword="null"/> for a plain delete.</param>
    public DeleteCommand(Type entityType, string tableName, bool isTableNameAuto, QueryCommand? condition, IReadOnlyList<KeyValue>? keys, IReadOnlyList<IPropertyMetadata>? returningColumns = null)
        : base(SqlStatementType.Delete, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Condition = condition;
        Keys = keys;
        ReturningColumns = returningColumns;
    }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }

    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }

    /// <summary>The predicate command whose prepared condition forms the <c>WHERE</c>, or <see langword="null"/>.</summary>
    public QueryCommand? Condition { get; }

    /// <summary>The declared key values rendered as equality predicates, or <see langword="null"/>.</summary>
    public IReadOnlyList<KeyValue>? Keys { get; }

    /// <summary>
    /// The mapped columns the statement returns through <c>RETURNING</c>/<c>OUTPUT</c>, or
    /// <see langword="null"/> for a plain delete that only reports the affected-row count.
    /// </summary>
    public override IReadOnlyList<IPropertyMetadata>? ReturningColumns { get; }
}
