using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Execution primitive of a multi-statement batch. Kept out of <see cref="QueryExecutor"/> so that
/// class stays focused on single-command execution: a batch uses either the driver's
/// <see cref="DbBatch"/> (PostgreSQL, MySQL/MariaDB) or one <c>;</c>-joined <see cref="DbCommand"/>
/// (SQLite always, SQL Server because its <c>SqlBatch</c> scopes each command separately).
/// <para>
/// Batch execution is deliberately not routed through the query interceptors: their events carry a
/// <see cref="DbCommand"/>, and a <see cref="DbBatch"/> command is a <see cref="DbBatchCommand"/>.
/// </para>
/// </summary>
internal sealed class BatchRunner
{
    private readonly IConnectionManager _connectionManager;
    private readonly Func<string, object?, DbParameter> _createParam;
    private readonly Func<DbTransaction?> _currentTransaction;
    private readonly Func<bool> _isDisposed;
    private readonly ILogger? _logger;
    private readonly bool _logParams;
    private readonly bool _logSensitiveData;
    private readonly int? _commandTimeout;

    internal BatchRunner(
        IConnectionManager connectionManager,
        Func<string, object?, DbParameter> createParam,
        Func<DbTransaction?> currentTransaction,
        Func<bool> isDisposed,
        LoggingOptions logging,
        int? commandTimeout)
    {
        _connectionManager = connectionManager;
        _createParam = createParam;
        _currentTransaction = currentTransaction;
        _isDisposed = isDisposed;
        _logger = logging.Logger;
        _logParams = logging.LogParams;
        _logSensitiveData = logging.LogSensitiveData;
        _commandTimeout = commandTimeout;
    }

    /// <summary>Executes a rendered batch in one round trip and materialises its result rows.</summary>
    public List<TResult> Run<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper)
    {
        CheckDisposed();
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            using var batch = CreateBatch(conn, plan);
            using var reader = batch.ExecuteReader();
            return Read(reader, mapper);
        }

        using var command = CreateJoinedCommand(conn, plan);
        using var joinedReader = command.ExecuteReader();
        return Read(joinedReader, mapper);
    }

    /// <summary>Asynchronously executes a rendered batch in one round trip and materialises its result rows.</summary>
    public async Task<List<TResult>> RunAsync<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
    {
        CheckDisposed();
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            await using var batch = CreateBatch(conn, plan);
            await using var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await ReadAsync(reader, mapper, cancellationToken).ConfigureAwait(false);
        }

        await using var command = CreateJoinedCommand(conn, plan);
        await using var joinedReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(joinedReader, mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes a rendered batch in one round trip and returns the first column of the result-bearing query's first row.</summary>
    /// <param name="plan">The rendered batch.</param>
    /// <returns>The scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>, or <see langword="null"/> when the query returns no rows.</returns>
    public object? RunScalar(BatchPlan plan)
    {
        CheckDisposed();
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            using var batch = CreateBatch(conn, plan);
            using var reader = batch.ExecuteReader();
            return ReadScalar(reader);
        }

        using var command = CreateJoinedCommand(conn, plan);
        using var joinedReader = command.ExecuteReader();
        return ReadScalar(joinedReader);
    }

    /// <summary>Asynchronously executes a rendered batch in one round trip and returns the first column of the result-bearing query's first row.</summary>
    /// <param name="plan">The rendered batch.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>The scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>, or <see langword="null"/> when the query returns no rows.</returns>
    public async Task<object?> RunScalarAsync(BatchPlan plan, CancellationToken cancellationToken)
    {
        CheckDisposed();
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            await using var batch = CreateBatch(conn, plan);
            await using var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await ReadScalarAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        await using var command = CreateJoinedCommand(conn, plan);
        await using var joinedReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await ReadScalarAsync(joinedReader, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Streams a rendered batch's result rows; the reader stays open for the whole batch.</summary>
    public async IAsyncEnumerable<TResult> RunStream<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CheckDisposed();
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            await using var batch = CreateBatch(conn, plan);
            await using var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield return mapper(reader);

            yield break;
        }

        await using var command = CreateJoinedCommand(conn, plan);
        await using var joinedReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await AdvanceToResultSetAsync(joinedReader, cancellationToken).ConfigureAwait(false);

        while (await joinedReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(joinedReader);
    }

    private DbBatch CreateBatch(DbConnection conn, BatchPlan plan)
    {
        var batch = conn.CreateBatch();
        if (_currentTransaction() is { } transaction)
            batch.Transaction = transaction;

        if (_commandTimeout is int timeout)
            batch.Timeout = timeout;

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            var statement = plan.Statements[i];
            var command = batch.CreateBatchCommand();
            command.CommandText = statement.Sql;

            for (var p = 0; p < statement.Parameters.Count; p++)
            {
                var parameter = statement.Parameters[p];
                command.Parameters.Add(_createParam(parameter.Name, parameter.Value));
            }

            batch.BatchCommands.Add(command);
        }

        if (_logParams) LogBatch(plan);

        return batch;
    }

    private DbCommand CreateJoinedCommand(DbConnection conn, BatchPlan plan)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = plan.ToSql();

        if (_currentTransaction() is { } transaction)
            cmd.Transaction = transaction;

        if (_commandTimeout is int timeout)
            cmd.CommandTimeout = timeout;

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            var parameters = plan.Statements[i].Parameters;
            for (var p = 0; p < parameters.Count; p++)
                cmd.Parameters.Add(_createParam(parameters[p].Name, parameters[p].Value));
        }

        if (_logParams) LogBatch(plan);

        return cmd;
    }

    private void LogBatch(BatchPlan plan)
    {
        _logger!.LogDebug("Executing batch: {sql}", plan.ToSql());

        if (_logSensitiveData)
        {
            foreach (var statement in plan.Statements)
            {
                foreach (var parameter in statement.Parameters)
                    _logger!.LogDebug("param {name} is {value}", parameter.Name, parameter.Value);
            }
        }
        else if (plan.Statements.Count > 0 && plan.Statements[0].Parameters.Count > 0)
        {
            _logger!.LogDebug("Use {method} to see param values", nameof(_logSensitiveData));
        }
    }

    private static List<TResult> Read<TResult>(DbDataReader reader, Func<IDataRecord, TResult> mapper)
    {
        AdvanceToResultSet(reader);

        var list = new List<TResult>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    private static async Task<List<TResult>> ReadAsync<TResult>(DbDataReader reader, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
    {
        await AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

        var list = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(mapper(reader));

        return list;
    }

    private static object? ReadScalar(DbDataReader reader)
    {
        AdvanceToResultSet(reader);

        if (!reader.Read())
            return null;

        var value = reader.GetValue(0);
        return value is DBNull ? null : value;
    }

    private static async Task<object?> ReadScalarAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        await AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        var value = reader.GetValue(0);
        return value is DBNull ? null : value;
    }

    // The result-bearing query is the last statement and only it returns columns; the preceding
    // side-effecting statements (materialisations and DML) surface as result sets without columns.
    // Stopping on the first set that has columns deliberately avoids calling NextResult on an
    // already-last set: Microsoft.Data.Sqlite empties the current result set when NextResult returns false.
    private static void AdvanceToResultSet(DbDataReader reader)
    {
        while (reader.FieldCount == 0)
        {
            if (!reader.NextResult())
                break;
        }
    }

    private static async Task AdvanceToResultSetAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        while (reader.FieldCount == 0)
        {
            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                break;
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
}
