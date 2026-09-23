namespace NextORM.Core;

/// <summary>
/// The action a provider takes on the rows of a temporary table created with <c>ON COMMIT</c> when the
/// creating transaction commits.
/// </summary>
public enum TempTableOnCommit
{
    /// <summary>Keep the rows (the default; the clause is omitted).</summary>
    PreserveRows,
    /// <summary>Delete the rows on commit (the table is kept).</summary>
    DeleteRows,
    /// <summary>Drop the table on commit.</summary>
    Drop,
}

/// <summary>
/// Options for materialising a query into a table with <see cref="TempTableExtensions"/>.
/// Providers that cannot express a given option reject it with <see cref="NotSupportedException"/> when
/// the SQL is built.
/// </summary>
public sealed record CreateTableAsOptions
{
    /// <summary>Whether the statement carries <c>IF NOT EXISTS</c>. Defaults to <see langword="false"/>.</summary>
    public bool IfNotExists { get; init; }

    /// <summary>
    /// Whether the table is populated from the query; <see langword="false"/> renders <c>WITH NO DATA</c>
    /// (PostgreSQL only). Defaults to <see langword="true"/>.
    /// </summary>
    public bool WithData { get; init; } = true;

    /// <summary>
    /// The <c>ON COMMIT</c> action of a temporary table (PostgreSQL only). Only valid for a temporary
    /// table (see <see cref="TempTableExtensions"/>); defaults to
    /// <see cref="TempTableOnCommit.PreserveRows"/>.
    /// </summary>
    public TempTableOnCommit OnCommit { get; init; } = TempTableOnCommit.PreserveRows;

    /// <summary>
    /// The column names to declare on the created table, or <see langword="null"/> to derive them from
    /// the query. Not every provider accepts a column list together with <c>AS SELECT</c> (SQLite does not).
    /// </summary>
    public IReadOnlyList<string>? Columns { get; init; }
}
