using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Fluent terminal for a bulk insert that returns a projection (or the generated keys) of every written
/// row through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form, started with
/// <see cref="BulkInsertBuilder{TEntity}.ReturningKey{TKey}"/> or
/// <see cref="BulkInsertBuilder{TEntity}.Returning{TResult}(System.Linq.Expressions.Expression{System.Func{TEntity, TResult}})"/>.
/// <para>
/// This is the portable, batch path: the native bulk API cannot return rows, so the set is written as a
/// sequence of returning <c>INSERT ... VALUES</c> statements. It requires a provider with
/// <c>RETURNING</c> or <c>OUTPUT</c> (PostgreSQL, SQLite 3.35+, MariaDB 10.5+, SQL Server); MySQL and
/// ClickHouse reject it with a clear <see cref="NotSupportedException"/>. The result order is not
/// guaranteed to match the source order.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type written.</typeparam>
/// <typeparam name="TResult">The materialized row type.</typeparam>
public sealed class BulkInsertReturningBuilder<TEntity, TResult>
{
    private readonly BulkInsertBuilder<TEntity> _bulk;
    private readonly IReadOnlyList<IPropertyMetadata> _returningColumns;
    private readonly SelectExpression[] _selectList;
    private readonly bool _oneColumn;

    internal BulkInsertReturningBuilder(
        BulkInsertBuilder<TEntity> bulk,
        IReadOnlyList<IPropertyMetadata> returningColumns,
        SelectExpression[] selectList,
        bool oneColumn)
    {
        _bulk = bulk;
        _returningColumns = returningColumns;
        _selectList = selectList;
        _oneColumn = oneColumn;
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute for the first batch, without executing
    /// it. Useful for verifying the <c>RETURNING</c>/<c>OUTPUT</c> SQL without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied, the source is empty, or it is async.</exception>
    /// <exception cref="NotSupportedException">The provider cannot express <c>RETURNING</c>/<c>OUTPUT</c>, or the context is read-only.</exception>
    public string ToSql()
    {
        _bulk.ValidateSyncSource();

        var mutation = _bulk.RequireMutationExecutor();
        var command = _bulk.BuildCommand(_bulk.ProjectSyncRows(), null);
        PortableBulkInsertExecutor.EnsureTimeoutSupported(command);

        var rowsPerBatch = PortableBulkInsertExecutor.RowsPerBatch(command);
        var batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);

        foreach (var row in command.SyncRows!)
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
                break;
        }

        if (batch.Count == 0)
            throw new InvalidOperationException("Cannot render SQL for an empty bulk insert.");

        return mutation.Render(PortableBulkInsertExecutor.BuildBatch(command, batch, _returningColumns));
    }

    /// <summary>Writes every row and returns the materialized projection of each written row.</summary>
    /// <returns>The returned rows, in result-set order.</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied, or the source is async.</exception>
    /// <exception cref="NotSupportedException">The provider cannot express <c>RETURNING</c>/<c>OUTPUT</c>, or the context is read-only.</exception>
    public IReadOnlyList<TResult> ToList()
    {
        _bulk.ValidateSyncSource();

        var mutation = _bulk.RequireMutationExecutor();
        var command = _bulk.BuildCommand(_bulk.ProjectSyncRows(), null);
        PortableBulkInsertExecutor.EnsureTimeoutSupported(command);

        var rowsPerBatch = PortableBulkInsertExecutor.RowsPerBatch(command);
        var result = new List<TResult>();
        var batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
        var written = 0;

        foreach (var row in command.SyncRows!)
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                written += Append(mutation, command, batch, result);
                command.Progress?.Invoke(written);
                batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            written += Append(mutation, command, batch, result);
            command.Progress?.Invoke(written);
        }

        return result;
    }

    /// <summary>Asynchronously writes every row and returns the materialized projection of each written row.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the returned rows, in result-set order.</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied.</exception>
    /// <exception cref="NotSupportedException">The provider cannot express <c>RETURNING</c>/<c>OUTPUT</c>, or the context is read-only.</exception>
    public Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        _bulk.ValidateSource();

        return _bulk.AsyncSource is not null
            ? ToListAsyncRowsAsync(_bulk.ProjectAsyncRows(cancellationToken), cancellationToken)
            : ToListAsyncSyncAsync(_bulk.ProjectSyncRows(), cancellationToken);
    }

    private Task<IReadOnlyList<TResult>> ToListAsyncSyncAsync(IEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        return RunAsync(rows, cancellationToken);
    }

    private Task<IReadOnlyList<TResult>> ToListAsyncRowsAsync(IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        return RunAsync(rows, cancellationToken);
    }

    private async Task<IReadOnlyList<TResult>> RunAsync(IEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        var mutation = _bulk.RequireMutationExecutor();
        var command = _bulk.BuildCommand(rows, null);
        PortableBulkInsertExecutor.EnsureTimeoutSupported(command);

        var rowsPerBatch = PortableBulkInsertExecutor.RowsPerBatch(command);
        var result = new List<TResult>();
        var batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
        var written = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                written += await AppendAsync(mutation, command, batch, result, cancellationToken).ConfigureAwait(false);
                command.Progress?.Invoke(written);
                batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            written += await AppendAsync(mutation, command, batch, result, cancellationToken).ConfigureAwait(false);
            command.Progress?.Invoke(written);
        }

        return result;
    }

    private async Task<IReadOnlyList<TResult>> RunAsync(IAsyncEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        var mutation = _bulk.RequireMutationExecutor();
        var command = _bulk.BuildCommand(asyncRows: rows, syncRows: null);
        PortableBulkInsertExecutor.EnsureTimeoutSupported(command);

        var rowsPerBatch = PortableBulkInsertExecutor.RowsPerBatch(command);
        var result = new List<TResult>();
        var batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
        var written = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
            {
                written += await AppendAsync(mutation, command, batch, result, cancellationToken).ConfigureAwait(false);
                command.Progress?.Invoke(written);
                batch = PortableBulkInsertExecutor.NewBatch(rowsPerBatch);
            }
        }

        if (batch.Count > 0)
        {
            written += await AppendAsync(mutation, command, batch, result, cancellationToken).ConfigureAwait(false);
            command.Progress?.Invoke(written);
        }

        return result;
    }

    private int Append(IMutationExecutor mutation, BulkInsertCommand command, List<object?[]> batch, List<TResult> result)
    {
        var insert = PortableBulkInsertExecutor.BuildBatch(command, batch, _returningColumns);
        result.AddRange(mutation.ExecuteReturning<TResult>(insert, _selectList, _oneColumn));
        return batch.Count;
    }

    private async Task<int> AppendAsync(IMutationExecutor mutation, BulkInsertCommand command, List<object?[]> batch, List<TResult> result, CancellationToken cancellationToken)
    {
        var insert = PortableBulkInsertExecutor.BuildBatch(command, batch, _returningColumns);
        result.AddRange(await mutation.ExecuteReturning<TResult>(insert, _selectList, _oneColumn, cancellationToken).ConfigureAwait(false));
        return batch.Count;
    }
}
