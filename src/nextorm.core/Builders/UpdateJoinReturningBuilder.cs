namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a multi-table <c>UPDATE</c> that returns the updated rows through PostgreSQL's
/// <c>UPDATE ... FROM ... RETURNING</c> form, started with
/// <see cref="UpdateJoinBuilder{TProjection}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TProjection,TResult}})"/>.
/// The returned rows are materialised with the same projection pipeline as a query; every terminal reads
/// the result through <see cref="Single"/> or <see cref="ToList"/>. The same builder is also accepted as a
/// data-modifying CTE body by <see cref="DataContextExtensions.With{TProjection,TResult}(IDataContext, string, UpdateJoinReturningBuilder{TProjection,TResult})"/>.
/// </summary>
/// <typeparam name="TProjection">The positional join projection (<c>Projection&lt;T1, ...&gt;</c>).</typeparam>
/// <typeparam name="TResult">The materialized row type (the projection or a projection of it).</typeparam>
public sealed class UpdateJoinReturningBuilder<TProjection, TResult>
{
    private readonly UpdateJoinBuilder<TProjection> _update;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly System.Linq.Expressions.LambdaExpression _projection;

    internal UpdateJoinReturningBuilder(
        UpdateJoinBuilder<TProjection> update,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        System.Linq.Expressions.LambdaExpression projection)
    {
        _update = update;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _projection = projection;
    }

    /// <summary>The context the update executes on.</summary>
    internal IDataContext DataContext => _update.DataContext;

    /// <summary>The selector that defines the returned columns, used to shape a data-modifying CTE read.</summary>
    internal System.Linq.Expressions.LambdaExpression Projection => _projection;

    /// <summary>The mapped columns the CTE body returns through <c>RETURNING</c>.</summary>
    internal IReadOnlyList<IPropertyMetadata> ReturningColumns => _returningColumns;

    /// <summary>Builds the multi-table <c>UPDATE ... RETURNING</c> command that becomes the body of a data-modifying CTE.</summary>
    internal MutationCommand BuildMutationCommand() => _update.BuildCommand(_returningColumns, _projection);

    /// <summary>Executes the update and returns the single updated row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The update touched no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows from a multi-table update.</exception>
    public TResult Single()
        => FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));

    /// <summary>Asynchronously executes the update and returns the single updated row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The update touched no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows from a multi-table update.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
        => FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));

    /// <summary>Executes the update and returns every updated row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows from a multi-table update.</exception>
    public IReadOnlyList<TResult> ToList()
        => RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);

    /// <summary>Asynchronously executes the update and returns every updated row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return updated rows from a multi-table update.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        => await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed, or the provider cannot return rows from a multi-table update.</exception>
    public string ToSql()
    {
        if (_update.DataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_update.DataContext.GetType().Name} cannot render SQL or return updated rows: it is not a database-backed context.");
    }

    private MutationCommand BuildCommand() => _update.BuildCommand(_returningColumns, _projection);

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
            $"{_update.DataContext.GetType().Name} does not support returning updated rows from a multi-table update. Use a database-backed context with RETURNING (PostgreSQL).");
    }
}
