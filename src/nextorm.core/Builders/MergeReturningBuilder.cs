namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a full <c>MERGE</c> that returns the merged rows through the provider's
/// <c>OUTPUT</c>/<c>RETURNING</c> form, started with
/// <see cref="MergeBuilder{TEntity}.Returning()"/> or
/// <see cref="MergeBuilder{TEntity}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TEntity,TResult}})"/>.
/// The returned rows are materialised with the same projection pipeline as a query; every terminal reads
/// the result through <see cref="Single"/> or <see cref="ToList"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
/// <typeparam name="TResult">The materialized row type (the entity or a projection).</typeparam>
public sealed class MergeReturningBuilder<TEntity, TResult>
{
    private readonly MergeBuilder<TEntity> _merge;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;

    internal MergeReturningBuilder(
        MergeBuilder<TEntity> merge,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn)
    {
        _merge = merge;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
    }

    /// <summary>Executes the merge and returns the single merged row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The merge touched no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public TResult Single() => FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));

    /// <summary>Asynchronously executes the merge and returns the single merged row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The merge touched no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
        => FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));

    /// <summary>Executes the merge and returns every merged row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList() => RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);

    /// <summary>Asynchronously executes the merge and returns every merged row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return merged rows, or the context is read-only.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        => await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);

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

    private MergeCommand BuildCommand() => _merge.BuildReturningCommand(_returningColumns);

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
