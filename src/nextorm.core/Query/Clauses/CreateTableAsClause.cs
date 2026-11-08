namespace NextORM.Core;

/// <summary>
/// The options of a <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c> clause, handed to
/// <see cref="ISqlDialect.MakeCreateTableAsSelect"/> so the dialect can spell the statement natively.
/// <see cref="Table"/> and <see cref="Columns"/> are already resolved and, when identifier quoting is
/// enabled, quoted by the caller — the dialect only places them.
/// </summary>
/// <param name="Table">The target table name (resolved and optionally quoted).</param>
/// <param name="Temporary">Whether the target is a temporary (session-scoped) table.</param>
/// <param name="IfNotExists">Whether the statement carries <c>IF NOT EXISTS</c>.</param>
/// <param name="Columns">The declared column names (resolved and optionally quoted), or <see langword="null"/> for none.</param>
/// <param name="OnCommit">The <c>ON COMMIT</c> action of a temporary table; <see cref="TempTableOnCommit.PreserveRows"/> omits the clause.</param>
/// <param name="WithData">Whether the table is populated from the query (<c>WITH NO DATA</c> when <see langword="false"/>).</param>
public readonly record struct CreateTableAsClause(
    string Table,
    bool Temporary,
    bool IfNotExists,
    IReadOnlyList<string>? Columns,
    TempTableOnCommit OnCommit,
    bool WithData);
