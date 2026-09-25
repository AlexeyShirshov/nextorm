namespace NextORM.Core;

/// <summary>
/// Fluent terminal for an <c>UPDATE</c> that returns the updated rows through the provider's
/// <c>RETURNING</c>/<c>OUTPUT</c> form, started with
/// <see cref="UpdateBuilder{TEntity}.Returning()"/> or
/// <see cref="UpdateBuilder{TEntity}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TEntity,TResult}})"/>.
/// The returned rows are materialised with the same projection pipeline as a query; every terminal reads
/// the result through <see cref="Single"/> or <see cref="ToList"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose rows are updated.</typeparam>
/// <typeparam name="TResult">The materialized row type (the entity or a projection).</typeparam>
public sealed class UpdateReturningBuilder<TEntity, TResult> : IOutputIntoMutation
{
    private readonly UpdateBuilder<TEntity> _update;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly string? _outputIntoTable;

    internal UpdateReturningBuilder(
        UpdateBuilder<TEntity> update,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        string? outputIntoTable = null)
    {
        _update = update;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _outputIntoTable = outputIntoTable;
    }

    /// <summary>Executes the update and returns the single updated row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The update touched no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows, or the context is read-only.</exception>
    public TResult Single()
        => FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));

    /// <summary>Asynchronously executes the update and returns the single updated row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The update touched no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows, or the context is read-only.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
        => FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));

    /// <summary>Executes the update and returns every updated row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList()
        => RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);

    /// <summary>Asynchronously executes the update and returns every updated row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows, or the context is read-only.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        => await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Writes the updated rows into <paramref name="targetTable"/> through SQL Server's
    /// <c>OUTPUT ... INTO</c> clause instead of returning them to the client. The target must already
    /// exist with columns whose names match the selected output columns.
    /// </summary>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>An output-into terminal whose <c>Execute</c> writes the rows and returns the affected-row count.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetTable"/> is null or empty.</exception>
    public OutputIntoBuilder OutputInto(string targetTable)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTable);
        return new OutputIntoBuilder(this, targetTable);
    }

    /// <summary>
    /// Writes the updated rows into <paramref name="targetTable"/> and also returns them to the client
    /// through a second <c>OUTPUT</c> clause (SQL Server). Chain a row terminal such as
    /// <see cref="ToList"/> on the returned builder.
    /// </summary>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>A returning builder that writes into the target and returns the same rows to the client.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetTable"/> is null or empty.</exception>
    public UpdateReturningBuilder<TEntity, TResult> OutputIntoThenOutput(string targetTable)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTable);
        return new UpdateReturningBuilder<TEntity, TResult>(_update, _returningColumns, _selectList, _oneColumn, targetTable);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider), or the provider cannot return updated rows.</exception>
    public string ToSql()
    {
        if (_update.DataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_update.DataContext.GetType().Name} cannot render SQL or return updated rows: it is not a database-backed context.");
    }

    private UpdateCommand BuildCommand()
    {
        var outputInto = _outputIntoTable is null ? null : new OutputIntoClause(_outputIntoTable, _returningColumns);
        return _update.BuildReturningCommand(_returningColumns, outputInto);
    }

    MutationCommand IOutputIntoMutation.BuildOutputIntoCommand(string targetTable)
        => _update.BuildOutputIntoCommand(_returningColumns, targetTable);

    IDataContext IOutputIntoMutation.DataContext => _update.DataContext;

    private static TResult FirstOrThrow(IReadOnlyList<TResult> rows)
        => rows.Count switch
        {
            0 => throw new InvalidOperationException("The update touched no row."),
            1 => rows[0],
            _ => throw new InvalidOperationException("The update touched more than one row; use ToList instead."),
        };

    private IMutationExecutor RequireExecutor()
    {
        if (_update.DataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_update.DataContext.GetType().Name} does not support returning updated rows. Use a database-backed context with RETURNING/OUTPUT (SQLite, PostgreSQL or SQL Server).");
    }
}
