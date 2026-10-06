using NextORM.Core;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a SQLite FTS5 maintenance/control command, started with
/// <see cref="DataContextExtensions.CreateSqliteFts5CommandBuilder"/>. The operation is issued through the
/// FTS5 control interface (<c>INSERT INTO &lt;table&gt;(...) VALUES(...)</c>); a provider without FTS5
/// rejects it with <see cref="NotSupportedException"/>. The builder is immutable: every operation method
/// returns a new builder and leaves the receiver unchanged.
/// </summary>
public sealed class SqliteFts5CommandBuilder
{
    private readonly IDataContext _dataContext;
    private readonly string _tableName;
    private readonly SqliteFts5Operation? _operation;
    private readonly int? _value;

    internal SqliteFts5CommandBuilder(IDataContext dataContext, string tableName)
        : this(dataContext, tableName, null, null)
    {
    }

    private SqliteFts5CommandBuilder(IDataContext dataContext, string tableName, SqliteFts5Operation? operation, int? value)
    {
        _dataContext = dataContext;
        _tableName = tableName;
        _operation = operation;
        _value = value;
    }

    /// <summary>Selects the FTS5 <c>automerge</c> command, which merges up to <paramref name="value"/> b-tree segments.</summary>
    /// <param name="value">The automerge level; must be between 0 and 16 inclusive.</param>
    /// <returns>A new builder carrying the <c>automerge</c> operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside 0..16.</exception>
    public SqliteFts5CommandBuilder AutoMerge(int value)
    {
        if (value is < 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(value), value, "automerge takes a value between 0 and 16.");

        return new(_dataContext, _tableName, SqliteFts5Operation.AutoMerge, value);
    }

    /// <summary>Selects the FTS5 <c>crisismerge</c> command, which merges the <paramref name="value"/> largest segments.</summary>
    /// <param name="value">The number of largest segments to merge; must not be negative.</param>
    /// <returns>A new builder carrying the <c>crisismerge</c> operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public SqliteFts5CommandBuilder CrisisMerge(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "crisismerge takes a non-negative value.");

        return new(_dataContext, _tableName, SqliteFts5Operation.CrisisMerge, value);
    }

    /// <summary>Selects the FTS5 <c>merge</c> command, which merges <paramref name="pages"/> pages of b-tree segments.</summary>
    /// <param name="pages">The number of pages to merge; the signed value is passed through unchanged.</param>
    /// <returns>A new builder carrying the <c>merge</c> operation.</returns>
    public SqliteFts5CommandBuilder Merge(int pages)
        => new(_dataContext, _tableName, SqliteFts5Operation.Merge, pages);

    /// <summary>Selects the FTS5 <c>optimize</c> command, which merges the whole full-text index into one segment.</summary>
    /// <returns>A new builder carrying the <c>optimize</c> operation.</returns>
    public SqliteFts5CommandBuilder Optimize()
        => new(_dataContext, _tableName, SqliteFts5Operation.Optimize, null);

    /// <summary>Selects the FTS5 <c>rebuild</c> command, which rebuilds the full-text index from the content table.</summary>
    /// <returns>A new builder carrying the <c>rebuild</c> operation.</returns>
    public SqliteFts5CommandBuilder Rebuild()
        => new(_dataContext, _tableName, SqliteFts5Operation.Rebuild, null);

    /// <summary>Selects the FTS5 <c>integrity-check</c> command, which validates the full-text index.</summary>
    /// <param name="checkExternalContent">Whether to also verify the external content; <see langword="null"/> omits the rank argument, <see langword="false"/> passes 0 (internal consistency only) and <see langword="true"/> passes 1 (also verify the content table).</param>
    /// <returns>A new builder carrying the <c>integrity-check</c> operation.</returns>
    public SqliteFts5CommandBuilder IntegrityCheck(bool? checkExternalContent = null)
        => new(_dataContext, _tableName, SqliteFts5Operation.IntegrityCheck, checkExternalContent switch
        {
            null => null,
            false => 0,
            true => 1,
        });

    /// <summary>Renders the SQL this builder would execute, without executing it.</summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No FTS5 maintenance command was selected.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed, or the provider does not support SQLite FTS5.</exception>
    public string ToSql()
    {
        var command = BuildCommand();
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(command);

        throw Unsupported();
    }

    /// <summary>Executes the maintenance command and returns the affected-row count where the provider reports one.</summary>
    /// <returns>The affected-row count, or 0 where the provider reports none.</returns>
    /// <exception cref="InvalidOperationException">No FTS5 maintenance command was selected.</exception>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider does not support SQLite FTS5.</exception>
    public int Execute()
    {
        var command = BuildCommand();
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw Unsupported();
    }

    /// <summary>Asynchronously executes the maintenance command and returns the affected-row count where the provider reports one.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the affected-row count, or 0 where the provider reports none.</returns>
    /// <exception cref="InvalidOperationException">No FTS5 maintenance command was selected.</exception>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider does not support SQLite FTS5.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var command = BuildCommand();
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw Unsupported();
    }

    private SqliteFts5Command BuildCommand()
    {
        if (_operation is not { } operation)
            throw new InvalidOperationException("Select an FTS5 maintenance command before rendering or executing it.");

        return new SqliteFts5Command(_tableName, operation, _value);
    }

    private NotSupportedException Unsupported()
        => new(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
