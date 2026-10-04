using Microsoft.Extensions.Logging;
using System.Collections;
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
    private readonly Func<DbCommand, string, object?, DbParameter> _createParamAware;
    private readonly Func<string, object?, DbParameter> _createParam;
    private readonly Func<DbTransaction?> _currentTransaction;
    private readonly Func<bool> _isDisposed;
    private readonly ILogger? _logger;
    private readonly bool _logParams;
    private readonly bool _logSensitiveData;
    private readonly int? _commandTimeout;

    internal BatchRunner(
        IConnectionManager connectionManager,
        Func<DbCommand, string, object?, DbParameter> createParamAware,
        Func<string, object?, DbParameter> createParam,
        Func<DbTransaction?> currentTransaction,
        Func<bool> isDisposed,
        LoggingOptions logging,
        int? commandTimeout)
    {
        _connectionManager = connectionManager;
        _createParamAware = createParamAware;
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

    /// <summary>Executes a rendered batch in one round trip and eagerly materialises every result set.</summary>
    /// <param name="plan">The rendered batch.</param>
    /// <param name="materializers">One materialiser per result set, in result-set order.</param>
    /// <returns>The eagerly materialised result sets.</returns>
    public BatchResult RunBatchMultiple(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
    {
        CheckDisposed();
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            using var batch = CreateBatch(conn, plan);
            using var reader = batch.ExecuteReader();
            return ReadMultiple(reader, plan, materializers);
        }

        using var command = CreateJoinedCommand(conn, plan);
        using var joinedReader = command.ExecuteReader();
        return ReadMultiple(joinedReader, plan, materializers);
    }

    /// <summary>Asynchronously executes a rendered batch in one round trip and eagerly materialises every result set.</summary>
    /// <param name="plan">The rendered batch.</param>
    /// <param name="materializers">One materialiser per result set, in result-set order.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the eagerly materialised result sets.</returns>
    public async Task<BatchResult> RunBatchMultipleAsync(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers, CancellationToken cancellationToken)
    {
        CheckDisposed();
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        if (!plan.UseJoinedCommand && conn.CanCreateBatch)
        {
            await using var batch = CreateBatch(conn, plan);
            await using var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await ReadMultipleAsync(reader, plan, materializers, cancellationToken).ConfigureAwait(false);
        }

        await using var command = CreateJoinedCommand(conn, plan);
        await using var joinedReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await ReadMultipleAsync(joinedReader, plan, materializers, cancellationToken).ConfigureAwait(false);
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
            await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield return mapper(reader);

            yield break;
        }

        await using var command = CreateJoinedCommand(conn, plan);
        await using var joinedReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await ResultSetNavigator.AdvanceToResultSetAsync(joinedReader, cancellationToken).ConfigureAwait(false);

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
                cmd.Parameters.Add(_createParamAware(cmd, parameters[p].Name, parameters[p].Value));
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
        ResultSetNavigator.AdvanceToResultSet(reader);

        var list = new List<TResult>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    private static async Task<List<TResult>> ReadAsync<TResult>(DbDataReader reader, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
    {
        await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

        var list = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(mapper(reader));

        return list;
    }

    private static BatchResult ReadMultiple(DbDataReader reader, BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
    {
        ValidateMaterializerCount(plan, materializers);

        ResultSetNavigator.AdvanceToResultSet(reader);

        var sets = new List<IList>(materializers.Count);
        var resultTypes = new List<Type>(materializers.Count);
        var columnNames = new List<string[]>(materializers.Count);
        for (var i = 0; i < materializers.Count; i++)
        {
            if (reader.FieldCount == 0)
                throw MissingResultSet(plan, i);

            columnNames.Add(SnapshotColumns(reader));
            sets.Add(materializers[i].Read(reader));
            resultTypes.Add(materializers[i].ResultType);

            if (i < materializers.Count - 1 && !ResultSetNavigator.MoveToNextResultSet(reader))
                throw MissingResultSet(plan, i + 1);
        }

        return new BatchResult(sets, resultTypes, columnNames);
    }

    private static async Task<BatchResult> ReadMultipleAsync(DbDataReader reader, BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers, CancellationToken cancellationToken)
    {
        ValidateMaterializerCount(plan, materializers);

        await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

        var sets = new List<IList>(materializers.Count);
        var resultTypes = new List<Type>(materializers.Count);
        var columnNames = new List<string[]>(materializers.Count);
        for (var i = 0; i < materializers.Count; i++)
        {
            if (reader.FieldCount == 0)
                throw MissingResultSet(plan, i);

            columnNames.Add(SnapshotColumns(reader));
            sets.Add(await materializers[i].ReadAsync(reader, cancellationToken).ConfigureAwait(false));
            resultTypes.Add(materializers[i].ResultType);

            if (i < materializers.Count - 1
                && !await ResultSetNavigator.MoveToNextResultSetAsync(reader, cancellationToken).ConfigureAwait(false))
            {
                throw MissingResultSet(plan, i + 1);
            }
        }

        return new BatchResult(sets, resultTypes, columnNames);
    }

    private static string[] SnapshotColumns(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        for (var i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return names;
    }

    /// <summary>
    /// Defensive internal-contract guard: the public API always supplies exactly one materialiser per
    /// declared result set, so a mismatch indicates an executor bug rather than caller input.
    /// </summary>
    private static void ValidateMaterializerCount(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> materializers)
    {
        if (materializers.Count != plan.Results.Count)
            throw new InvalidOperationException(
                $"The batch declares {plan.Results.Count} result set(s) but {materializers.Count} materialiser(s) were supplied; one materialiser per declared result set is required.");
    }

    /// <summary>
    /// Defensive internal-contract guard: SQL always returns every declared result set, so a provider
    /// returning fewer means a broken internal contract rather than caller input.
    /// </summary>
    private static InvalidOperationException MissingResultSet(BatchPlan plan, int returnedCount)
        => new($"The batch declares {plan.Results.Count} result set(s) but the provider returned only {returnedCount}; a declared result set is missing from the batch output.");

    private static object? ReadScalar(DbDataReader reader)
    {
        ResultSetNavigator.AdvanceToResultSet(reader);

        if (!reader.Read())
            return null;

        var value = reader.GetValue(0);
        return value is DBNull ? null : value;
    }

    private static async Task<object?> ReadScalarAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        await ResultSetNavigator.AdvanceToResultSetAsync(reader, cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        var value = reader.GetValue(0);
        return value is DBNull ? null : value;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
}
