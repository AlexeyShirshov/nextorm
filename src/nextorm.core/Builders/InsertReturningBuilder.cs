using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for an <c>INSERT</c> that returns the written rows or generated key through the
/// provider's <c>RETURNING</c>/<c>OUTPUT</c> form or its scalar identity function, started with
/// <see cref="InsertBuilder{TEntity}.Returning()"/>, <see cref="InsertBuilder{TEntity}.Returning{TResult}(Expression{Func{TEntity, TResult}})"/>,
/// <see cref="InsertBuilder{TEntity}.ReturningIdentity{TKey}(Expression{Func{TEntity, TKey}})"/>,
/// <see cref="InsertBuilder{TEntity}.ReturningIdentity{TKey}()"/> or
/// <see cref="InsertBuilder{TEntity}.ReturningKey{TKey}()"/>.
/// <para>
/// The returned rows are materialised with the same projection pipeline as a query: the entity form
/// returns <typeparamref name="TEntity"/>, the projection form returns the projected shape. The
/// builder is single-use, like the <see cref="InsertBuilder{TEntity}"/> it is created from, and every
/// terminal reads the result through <see cref="Single"/> or <see cref="ToList"/>.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type inserted.</typeparam>
/// <typeparam name="TResult">The materialized row type (the entity, a scalar member or a projection).</typeparam>
public sealed class InsertReturningBuilder<TEntity, TResult>
{
    private readonly InsertBuilder<TEntity> _insert;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;
    private readonly IPropertyMetadata? _identityColumn;
    private readonly bool _identityFunction;

    internal InsertReturningBuilder(
        InsertBuilder<TEntity> insert,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn,
        IPropertyMetadata? identityColumn = null,
        bool identityFunction = false,
        LambdaExpression? projection = null)
    {
        _insert = insert;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
        _identityColumn = identityColumn;
        _identityFunction = identityFunction;
        Projection = projection;
    }

    /// <summary>
    /// The selector that defines the returned columns, or <c>null</c> for the identity-function form
    /// (which names no column). Used to shape a data-modifying CTE read so its returned columns are
    /// typed like the <c>RETURNING</c> projection.
    /// </summary>
    internal LambdaExpression? Projection { get; }

    /// <summary>The mapped columns the CTE body returns through <c>RETURNING</c>.</summary>
    internal IReadOnlyList<IPropertyMetadata> ReturningColumns => _returningColumns;
    /// <summary>Builds the <c>INSERT</c> command that becomes the body of a data-modifying CTE.</summary>
    internal InsertCommand BuildMutationCommand() => BuildCommand();

    /// <summary>
    /// Executes the insert and returns the single returned row (or generated key).
    /// </summary>
    /// <returns>The materialized row or key.</returns>
    /// <exception cref="InvalidOperationException">The insert writes more than one row; use <see cref="ToList"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return the requested value, or the context is read-only.</exception>
    public TResult Single()
    {
        EnsureSingleRow();
        return SingleCore();
    }

    /// <summary>
    /// Asynchronously executes the insert and returns the single returned row (or generated key).
    /// </summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized row or key.</returns>
    /// <exception cref="InvalidOperationException">The insert writes more than one row; use <see cref="ToListAsync"/>.</exception>
    /// <exception cref="NotSupportedException">The provider cannot return the requested value, or the context is read-only.</exception>
    public async Task<TResult> SingleAsync(CancellationToken cancellationToken = default)
    {
        EnsureSingleRow();
        var executor = RequireExecutor();

        if (_identityFunction)
            return InsertBuilder<TEntity>.ConvertIdentity<TResult>(await executor.ExecuteIdentityFunction(BuildIdentityFunctionCommand(), cancellationToken).ConfigureAwait(false));

        if (_identityColumn is not null)
            return InsertBuilder<TEntity>.ConvertIdentity<TResult>(await executor.ExecuteIdentity(BuildIdentityCommand(), cancellationToken).ConfigureAwait(false));

        var rows = await executor.ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);
        return FirstOrThrow(rows);
    }

    /// <summary>
    /// Executes the insert and returns every returned row (or key). A multi-row insert returns one entry
    /// per row where the provider supports <c>RETURNING</c>/<c>OUTPUT</c>; the scalar identity-function
    /// fallback yields a single entry.
    /// </summary>
    /// <returns>The materialized rows or keys, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return the requested value, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList()
    {
        var executor = RequireExecutor();

        if (_identityFunction)
            return [InsertBuilder<TEntity>.ConvertIdentity<TResult>(executor.ExecuteIdentityFunction(BuildIdentityFunctionCommand()))];

        if (_identityColumn is not null && !executor.SupportsGeneratedColumns)
            return [InsertBuilder<TEntity>.ConvertIdentity<TResult>(executor.ExecuteIdentity(BuildIdentityCommand()))];

        return executor.ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn);
    }

    /// <summary>
    /// Asynchronously executes the insert and returns every returned row (or key). A multi-row insert
    /// returns one entry per row where the provider supports <c>RETURNING</c>/<c>OUTPUT</c>; the scalar
    /// identity-function fallback yields a single entry.
    /// </summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialized rows or keys, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return the requested value, or the context is read-only.</exception>
    public async Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var executor = RequireExecutor();

        if (_identityFunction)
            return [InsertBuilder<TEntity>.ConvertIdentity<TResult>(await executor.ExecuteIdentityFunction(BuildIdentityFunctionCommand(), cancellationToken).ConfigureAwait(false))];

        if (_identityColumn is not null && !executor.SupportsGeneratedColumns)
            return [InsertBuilder<TEntity>.ConvertIdentity<TResult>(await executor.ExecuteIdentity(BuildIdentityCommand(), cancellationToken).ConfigureAwait(false))];

        return await executor.ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    public string ToSql()
    {
        if (_insert.DataContext is IMutationExecutor executor)
        {
            if (_identityFunction)
                return executor.RenderIdentityFunction(BuildIdentityFunctionCommand());

            if (_identityColumn is not null && !executor.SupportsGeneratedColumns)
                return executor.RenderIdentityFunction(BuildIdentityCommand());

            return executor.Render(BuildCommand());
        }

        throw new NotSupportedException($"{_insert.DataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    private TResult SingleCore()
    {
        var executor = RequireExecutor();

        if (_identityFunction)
            return InsertBuilder<TEntity>.ConvertIdentity<TResult>(executor.ExecuteIdentityFunction(BuildIdentityFunctionCommand()));

        if (_identityColumn is not null)
            return InsertBuilder<TEntity>.ConvertIdentity<TResult>(executor.ExecuteIdentity(BuildIdentityCommand()));

        return FirstOrThrow(executor.ExecuteReturning<TResult>(BuildCommand(), _selectList, _oneColumn));
    }

    private InsertCommand BuildCommand() => _insert.BuildReturningCommand(_returningColumns);

    private InsertCommand BuildIdentityCommand() => _insert.BuildIdentityCommand(_identityColumn!);

    private InsertCommand BuildIdentityFunctionCommand() => _insert.BuildReturningCommand([]);

    private void EnsureSingleRow()
    {
        if (_insert.RowCount > 1)
            throw new InvalidOperationException("The insert writes more than one row; use ToList instead.");
    }

    private static TResult FirstOrThrow(IReadOnlyList<TResult> rows)
        => rows.Count switch
        {
            0 => throw new InvalidOperationException("The insert returned no row."),
            1 => rows[0],
            _ => throw new InvalidOperationException("The insert returned more than one row; use ToList instead."),
        };

    private IMutationExecutor RequireExecutor()
    {
        if (_insert.DataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_insert.DataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL, MariaDB or ClickHouse); the in-memory provider is read-only.");
    }

    internal static (IReadOnlyList<IPropertyMetadata> Columns, SelectExpression[] SelectList, bool OneColumn) ParseProjection(
        LambdaExpression projection,
        IReadOnlyList<IPropertyMetadata> allProperties,
        Func<PropertyInfo, IPropertyMetadata?> find)
        => ReturningProjection.Parse(projection, allProperties, find);

    internal static SelectExpression[] BuildSelectList(IReadOnlyList<IPropertyMetadata> columns, IReadOnlyList<PropertyInfo> targets)
        => ReturningProjection.BuildSelectList(columns, targets);
}
