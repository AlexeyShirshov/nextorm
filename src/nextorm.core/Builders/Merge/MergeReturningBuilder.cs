namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a full <c>MERGE</c> or key upsert that returns the affected rows through the provider's
/// <c>OUTPUT</c>/<c>RETURNING</c> form, started with
/// <see cref="MergeBuilder{TEntity}.Returning()"/> or
/// <see cref="MergeBuilder{TEntity}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TEntity,TResult}})"/>.
/// The returned rows are materialised with the same projection pipeline as a query; every terminal reads
/// the result through <see cref="Single"/> or <see cref="ToList"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
/// <typeparam name="TResult">The materialized row type (the entity or a projection).</typeparam>
public sealed class MergeReturningBuilder<TEntity, TResult> : IOutputIntoMutation
{
    private readonly MergeBuilder<TEntity> _merge;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly OutputIntoClause? _standaloneOutput;

    internal MergeReturningBuilder(
        MergeBuilder<TEntity> merge,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        System.Linq.Expressions.LambdaExpression? projection = null,
        OutputIntoClause? standaloneOutput = null)
    {
        _merge = merge;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _standaloneOutput = standaloneOutput;
        Projection = projection;
    }

    /// <summary>The context the merge executes on.</summary>
    internal IDataContext DataContext => _merge.DataContext;

    /// <summary>
    /// The selector that defines the returned columns, or <see langword="null"/> when none was captured.
    /// Used to shape a SQL Server batch capture read so its returned columns are typed like the
    /// <c>OUTPUT</c> projection.
    /// </summary>
    internal System.Linq.Expressions.LambdaExpression? Projection { get; }

    /// <summary>The mapped columns the merge returns through <c>OUTPUT</c>/<c>RETURNING</c>.</summary>
    internal IReadOnlyList<IPropertyMetadata> ReturningColumns => _returningColumns;

    /// <summary>Builds the <c>MERGE</c> command that is captured into a batch table variable.</summary>
    internal MergeCommand BuildMutationCommand()
    {
        if (_standaloneOutput is not null)
            throw new NotSupportedException("Standalone MERGE OUTPUT INTO cannot be registered as a batch table-variable capture.");

        return BuildCommand();
    }

    /// <summary>
    /// Writes SQL Server MERGE actions into the existing table's <c>action</c> column, followed by the
    /// projected <c>inserted</c> columns under their mapped names. Returns no rows to the client.
    /// </summary>
    /// <param name="targetTable">The existing table's bare name (no schema qualifier or quoting).</param>
    /// <returns>The terminal for executing the standalone write.</returns>
    internal OutputIntoBuilder CreateExistingTableOutput(string targetTable)
        => new(this, CreateStandaloneOutput(targetTable), standaloneOnly: true);

    /// <summary>
    /// Writes the same capture as <see cref="CreateExistingTableOutput"/> and separately returns the projected
    /// <c>inserted</c> values for this statement's affected rows. The client result does not include
    /// <c>$action</c> or read existing rows from the capture table.
    /// </summary>
    /// <param name="targetTable">The existing table's bare name (no schema qualifier or quoting).</param>
    /// <returns>A terminal for executing the write and reading the projected rows.</returns>
    internal MergeReturningBuilder<TEntity, TResult> WithExistingTableOutput(string targetTable)
        => new(_merge, _returningColumns, _selectList, _oneColumn, Projection, CreateStandaloneOutput(targetTable));

    private OutputIntoClause CreateStandaloneOutput(string targetTable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTable);
        if ((!char.IsAsciiLetter(targetTable[0]) && targetTable[0] != '_')
            || targetTable.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Standalone MERGE OUTPUT INTO requires a bare target table name.", nameof(targetTable));

        if (_standaloneOutput is not null)
            throw new InvalidOperationException("Standalone MERGE OUTPUT INTO is already configured.");

        _merge.ValidateStandaloneOutput();
        return new OutputIntoClause(targetTable, _returningColumns);
    }

    IDataContext IOutputIntoMutation.DataContext => DataContext;

    MutationCommand IOutputIntoMutation.BuildOutputIntoCommand(OutputIntoClause outputInto)
        => BuildStandaloneOutput(outputInto, returnsOutput: false);

    /// <summary>Executes the merge and returns the single merged row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The merge touched no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public TResult Single()
    {
        if (_standaloneOutput is null)
            _merge.ValidateFilters();
        return FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));
    }

    /// <summary>Asynchronously executes the merge and returns the single merged row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The merge touched no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
    {
        if (_standaloneOutput is null)
            await _merge.ValidateFiltersAsync(cancellationToken).ConfigureAwait(false);
        return FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Executes the merge and returns every merged row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList()
    {
        if (_standaloneOutput is null)
            _merge.ValidateFilters();
        return RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);
    }

    /// <summary>Asynchronously executes the merge and returns every merged row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_standaloneOutput is null)
            await _merge.ValidateFiltersAsync(cancellationToken).ConfigureAwait(false);
        return await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider), or the provider cannot return merged rows.</exception>
    public string ToSql()
    {
        if (_merge.DataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_merge.DataContext.GetType().Name} cannot render SQL or return merged rows: it is not a database-backed context.");
    }

    private MergeCommand BuildCommand() => _standaloneOutput is null
        ? _merge.BuildReturningCommand(_returningColumns)
        : BuildStandaloneOutput(_standaloneOutput, returnsOutput: true);

    private MergeCommand BuildStandaloneOutput(OutputIntoClause target, bool returnsOutput)
    {
        _merge.ValidateStandaloneOutput();
        var command = _merge.BuildReturningCommand(_returningColumns);
        command.StandaloneOutputInto = target;
        command.ReturnsStandaloneOutput = returnsOutput;
        return command;
    }

    private static TResult FirstOrThrow(IReadOnlyList<TResult> rows)
        => rows.Count switch
        {
            0 => throw new InvalidOperationException("The merge touched no row."),
            1 => rows[0],
            _ => throw new InvalidOperationException("The merge touched more than one row; use ToList instead."),
        };

    private IMutationExecutor RequireExecutor()
    {
        if (_merge.DataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_merge.DataContext.GetType().Name} does not support returning merged rows. Use a database-backed context with OUTPUT/RETURNING (SQL Server or PostgreSQL).");
    }
}
