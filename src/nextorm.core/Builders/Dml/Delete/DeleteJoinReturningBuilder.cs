namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a multi-table <c>DELETE</c> that returns the removed rows through PostgreSQL's
/// <c>DELETE ... USING ... RETURNING</c> form, started with
/// <see cref="DataContextExtensions.CreateDeleteJoinBuilder{T1, T2}(JoinedEntityBuilder{T1, T2})"/> and
/// switched to a returning selector with
/// <see cref="DeleteJoinBuilder{TProjection}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TProjection, TResult}})"/>.
/// The whole-projection (identity) form returns every returnable mapped property of every
/// item slot, in slot order, so a self-join of one type keeps its <c>Item1</c>/<c>Item2</c> values
/// distinct (a repeated CLR type stays separated by slot); explicit projections are unchanged and no
/// call adds a <c>RETURNING</c> list implicitly. The returned rows are materialised with the same
/// projection pipeline as a query; every terminal reads the result through <see cref="Single"/> or
/// <see cref="ToList"/>. Identity is PostgreSQL only, on INNER joins and for arities 2–8; every other
/// provider and every outer join rejects. The same builder is also accepted as a data-modifying CTE
/// body by
/// <see cref="DataContextExtensions.With{TProjection,TResult}(IDataContext, string, DeleteJoinReturningBuilder{TProjection,TResult})"/>.
/// </summary>
/// <typeparam name="TProjection">The positional join projection (<c>Projection&lt;T1, ...&gt;</c>).</typeparam>
/// <typeparam name="TResult">The materialized row type (the projection or a projection of it).</typeparam>
public sealed class DeleteJoinReturningBuilder<TProjection, TResult>
{
    private readonly EntityBuilder<TProjection> _query;
    private readonly Type _targetType;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly System.Linq.Expressions.LambdaExpression _projection;

    internal DeleteJoinReturningBuilder(
        EntityBuilder<TProjection> query,
        Type targetType,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        System.Linq.Expressions.LambdaExpression projection)
    {
        _query = query;
        _targetType = targetType;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _projection = projection;
    }

    /// <summary>The context the delete executes on.</summary>
    internal IDataContext DataContext => _query.DataProvider;

    /// <summary>The selector that defines the returned columns, used to shape a data-modifying CTE read.</summary>
    internal System.Linq.Expressions.LambdaExpression Projection => _projection;

    /// <summary>The mapped columns the CTE body returns through <c>RETURNING</c>.</summary>
    internal IReadOnlyList<IPropertyMetadata> ReturningColumns => _returningColumns;

    /// <summary>Builds the multi-table <c>DELETE ... RETURNING</c> command that becomes the body of a data-modifying CTE.</summary>
    internal MutationCommand BuildMutationCommand()
    {
        var source = JoinedMutationSource.Prepare(_query, "DELETE");
        return new DeleteJoinCommand(_targetType, source, _returningColumns, _projection);
    }

    /// <summary>Executes the delete and returns the single removed row.</summary>
    /// <returns>The materialized row.</returns>
    /// <exception cref="InvalidOperationException">The delete removed no row, or more than one; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows from a multi-table delete.</exception>
    public TResult Single()
        => FirstOrThrow(RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));

    /// <summary>Asynchronously executes the delete and returns the single removed row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row.</returns>
    /// <exception cref="InvalidOperationException">The delete removed no row, or more than one; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows from a multi-table delete.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
        => FirstOrThrow(await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));

    /// <summary>Executes the delete and returns every removed row.</summary>
    /// <returns>The materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows from a multi-table delete.</exception>
    public IReadOnlyList<TResult> ToList()
        => RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);

    /// <summary>Asynchronously executes the delete and returns every removed row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return removed rows from a multi-table delete.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        => await RequireExecutor().ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context is not database-backed, or the provider cannot return rows from a multi-table delete.</exception>
    public string ToSql()
    {
        if (_query.DataProvider is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_query.DataProvider.GetType().Name} cannot render SQL or return removed rows: it is not a database-backed context.");
    }

    private MutationCommand BuildCommand() => BuildMutationCommand();

    private static TResult FirstOrThrow(IReadOnlyList<TResult> rows)
        => rows.Count switch
        {
            0 => throw new InvalidOperationException("The delete removed no row."),
            1 => rows[0],
            _ => throw new InvalidOperationException("The delete removed more than one row; use ToList instead."),
        };

    private IMutationExecutor RequireExecutor()
    {
        if (_query.DataProvider is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_query.DataProvider.GetType().Name} does not support returning removed rows from a multi-table delete. Use a database-backed context with RETURNING (PostgreSQL).");
    }
}
