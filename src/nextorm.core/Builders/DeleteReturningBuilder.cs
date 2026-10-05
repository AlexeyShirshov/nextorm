using NextORM.Core;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a <c>DELETE</c> that returns the removed rows through the provider's
/// <c>RETURNING</c>/<c>OUTPUT</c> form, started with <see cref="DeleteBuilder{TEntity}.Returning()"/> or
/// <see cref="DeleteBuilder{TEntity}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TEntity,TResult}})"/>.
/// The returned rows are materialised with the same projection pipeline as a query; every terminal reads
/// the result through <see cref="Single"/> or <see cref="ToList"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose rows are deleted.</typeparam>
/// <typeparam name="TResult">The materialized row type (the entity or a projection).</typeparam>
public sealed class DeleteReturningBuilder<TEntity, TResult> : IOutputIntoMutation
{
    private readonly DeleteBuilder<TEntity> _delete;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly System.Linq.Expressions.LambdaExpression? _projection;
    private readonly string? _outputIntoTable;

    internal DeleteReturningBuilder(
        DeleteBuilder<TEntity> delete,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        string? outputIntoTable = null,
        System.Linq.Expressions.LambdaExpression? projection = null)
    {
        _delete = delete;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _outputIntoTable = outputIntoTable;
        _projection = projection;
    }

    /// <summary>The context the delete executes on.</summary>
    internal IDataContext DataContext => _delete.DataContext;

    /// <summary>
    /// The selector that defines the returned columns, or <c>null</c> when none was captured. Used to
    /// shape a data-modifying CTE read so its returned columns are typed like the <c>RETURNING</c>
    /// projection.
    /// </summary>
    internal System.Linq.Expressions.LambdaExpression? Projection => _projection;

    /// <summary>The mapped columns the CTE body returns through <c>RETURNING</c>.</summary>
    internal IReadOnlyList<IPropertyMetadata> ReturningColumns => _returningColumns;

    /// <summary>Builds the <c>DELETE ... RETURNING</c> command that becomes the body of a data-modifying CTE.</summary>
    internal MutationCommand BuildMutationCommand() => BuildCommand();

    /// <summary>Executes the delete and returns the single removed row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The delete removed no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows, or the context is read-only.</exception>
    public TResult Single()
        => FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));

    /// <summary>Asynchronously executes the delete and returns the single removed row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The delete removed no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows, or the context is read-only.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
        => FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));

    /// <summary>Executes the delete and returns every removed row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList()
        => RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);

    /// <summary>Asynchronously executes the delete and returns every removed row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows, or the context is read-only.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        => await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Writes the removed rows into <paramref name="targetTable"/> through SQL Server's
    /// <c>OUTPUT ... INTO</c> clause (the removed row is read through the <c>deleted</c> alias) instead
    /// of returning it to the client. The target must already exist with columns whose names match the
    /// selected output columns.
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
    /// Writes the removed rows into <paramref name="targetTable"/> and also returns them to the client
    /// through a second <c>OUTPUT</c> clause (SQL Server). Chain a row terminal such as
    /// <see cref="ToList"/> on the returned builder.
    /// </summary>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>A returning builder that writes into the target and returns the same rows to the client.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetTable"/> is null or empty.</exception>
    public DeleteReturningBuilder<TEntity, TResult> OutputIntoThenOutput(string targetTable)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTable);
        return new DeleteReturningBuilder<TEntity, TResult>(_delete, _returningColumns, _selectList, _oneColumn, targetTable, _projection);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider), or the provider cannot return removed rows.</exception>
    public string ToSql()
    {
        if (_delete.DataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_delete.DataContext.GetType().Name} cannot render SQL or return removed rows: it is not a database-backed context.");
    }

    private DeleteCommand BuildCommand()
    {
        var outputInto = _outputIntoTable is null ? null : new OutputIntoClause(_outputIntoTable, _returningColumns);
        return _delete.BuildReturningCommand(_returningColumns, outputInto);
    }

    MutationCommand IOutputIntoMutation.BuildOutputIntoCommand(string targetTable)
        => _delete.BuildOutputIntoCommand(_returningColumns, targetTable);

    IDataContext IOutputIntoMutation.DataContext => _delete.DataContext;

    private static TResult FirstOrThrow(IReadOnlyList<TResult> rows)
        => rows.Count switch
        {
            0 => throw new InvalidOperationException("The delete removed no row."),
            1 => rows[0],
            _ => throw new InvalidOperationException("The delete removed more than one row; use ToList instead."),
        };

    private IMutationExecutor RequireExecutor()
    {
        if (_delete.DataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_delete.DataContext.GetType().Name} does not support returning removed rows. Use a database-backed context with RETURNING/OUTPUT (SQLite, PostgreSQL or SQL Server).");
    }
}
