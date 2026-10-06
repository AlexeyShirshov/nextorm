namespace NextORM.Core;

/// <summary>
/// The SQLite FTS5 maintenance operation a <see cref="SqliteFts5Command"/> issues through the FTS5
/// <c>INSERT INTO &lt;table&gt;(...) VALUES(...)</c> control interface. The numeric values are part of the
/// stable internal contract; do not reorder.
/// </summary>
internal enum SqliteFts5Operation
{
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;, rank) VALUES('automerge', &lt;n&gt;)</c>.</summary>
    AutoMerge = 0,
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;, rank) VALUES('crisismerge', &lt;n&gt;)</c>.</summary>
    CrisisMerge = 1,
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;, rank) VALUES('merge', &lt;n&gt;)</c>.</summary>
    Merge = 2,
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;) VALUES('optimize')</c>.</summary>
    Optimize = 3,
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;) VALUES('rebuild')</c>.</summary>
    Rebuild = 4,
    /// <summary><c>INSERT INTO &lt;table&gt;(&lt;table&gt;[, rank]) VALUES('integrity-check'[, &lt;n&gt;])</c>.</summary>
    IntegrityCheck = 5,
}

/// <summary>
/// An FTS5 maintenance command over a raw FTS5 table name. It reuses the FTS5 control interface: the
/// operation is a row written through <c>INSERT INTO &lt;table&gt;(...) VALUES(...)</c>, so the command is
/// typed as an insert while <see cref="SqlMutationBuilder.MakeSqliteFts5"/> renders the native form and the
/// dialect capability guard rejects providers without FTS5. <see cref="TableName"/> is preserved verbatim.
/// </summary>
internal sealed class SqliteFts5Command : MutationCommand
{
    /// <summary>Creates an FTS5 maintenance command.</summary>
    /// <param name="tableName">The raw (unquoted, verbatim) FTS5 table name.</param>
    /// <param name="operation">The maintenance operation to issue.</param>
    /// <param name="value">The operation's integer argument, or <see langword="null"/> when it takes none.</param>
    public SqliteFts5Command(string tableName, SqliteFts5Operation operation, int? value)
        : base(SqlStatementType.Insert, typeof(object))
    {
        TableName = tableName;
        Operation = operation;
        Value = value;
    }

    /// <summary>The raw (unquoted, verbatim) FTS5 table name.</summary>
    public string TableName { get; }

    /// <summary>The maintenance operation to issue.</summary>
    public SqliteFts5Operation Operation { get; }

    /// <summary>The operation's integer argument, or <see langword="null"/> when it takes none.</summary>
    public int? Value { get; }
}
