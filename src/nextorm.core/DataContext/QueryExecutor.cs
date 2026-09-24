using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Execution axis of a database-backed context: turns a prepared command into a <see cref="DbCommand"/>,
/// runs it and materialises the result. Extracted out of <c>DataContext</c> so the context no longer owns
/// the execution algorithm (SRP). Everything it needs arrives as a constructor dependency — the
/// connection role, the parameter factory, the logging configuration and the disposal state — so it
/// never sees the concrete context (DIP).
/// </summary>
internal sealed class QueryExecutor : IQueryExecutor, IRowReaderFactory
{
    private readonly IConnectionManager _connectionManager;
    private readonly Func<string, object?, DbParameter> _createParam;
    private readonly ILogger? _logger;
    private readonly bool _logParams;
    private readonly bool _logSensitiveData;
    private readonly Func<bool> _isDisposed;
    private readonly Func<DbTransaction?> _currentTransaction;

    internal QueryExecutor(
        IConnectionManager connectionManager,
        Func<string, object?, DbParameter> createParam,
        LoggingOptions logging,
        Func<bool> isDisposed,
        Func<DbTransaction?> currentTransaction)
    {
        _connectionManager = connectionManager;
        _createParam = createParam;
        _logger = logging.Logger;
        _logParams = logging.LogParams;
        _logSensitiveData = logging.LogSensitiveData;
        _isDisposed = isDisposed;
        _currentTransaction = currentTransaction;
    }

    private void LogParams(DbCommand sqlCommand)
    {
        _logger!.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

        if (_logSensitiveData)
        {
            foreach (DbParameter p in sqlCommand.Parameters)
            {
                _logger!.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
            }
        }
        else if (sqlCommand.Parameters?.Count > 0)
        {
            _logger!.LogDebug("Use {method} to see param values", nameof(_logSensitiveData));
        }
    }

    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params)
        => GetDbCommand(compiledQuery, @params is null ? ReadOnlySpan<object?>.Empty : @params);

    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn, _currentTransaction());

        if (_logParams) LogParams(cmd);

        return cmd;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "A pooled connection may be open or closed depending on prior use, so the connection-state check is reachable on both sides; the analyzer cannot assume state across the pool.")]
    private async ValueTask<DbCommand> GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        await _connectionManager.EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = _connectionManager.GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn, _currentTransaction());

        if (_logParams) LogParams(cmd);

        return cmd;
    }

    /// <summary>
    /// Creates a fresh <see cref="DbCommand"/> for a mutation, binds its parameters and attaches it to
    /// the current connection. Unlike a prepared query command, a mutation command is not reused across
    /// executions, so the parameters are added each time.
    /// </summary>
    private DbCommand CreateMutationCommand(string sql, IReadOnlyList<Parameter> parameters)
    {
        _connectionManager.EnsureConnectionOpen();
        var conn = _connectionManager.GetConnection();

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        if (_currentTransaction() is { } transaction)
            cmd.Transaction = transaction;

        for (var i = 0; i < parameters.Count; i++)
            cmd.Parameters.Add(_createParam(parameters[i].Name, parameters[i].Value));

        if (_logParams) LogParams(cmd);

        return cmd;
    }

    /// <summary>Executes a mutation and returns the number of affected rows.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The number of rows affected, as reported by the provider.</returns>
    public int ExecuteNonQuery(string sql, IReadOnlyList<Parameter> parameters)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Asynchronously executes a mutation and returns the number of affected rows.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows affected.</returns>
    public async Task<int> ExecuteNonQueryAsync(string sql, IReadOnlyList<Parameter> parameters, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes a mutation and returns the first column of its first row, or <see langword="null"/>.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>.</returns>
    public object? ExecuteScalar(string sql, IReadOnlyList<Parameter> parameters)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        var result = cmd.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    /// <summary>Asynchronously executes a mutation and returns the first column of its first row, or <see langword="null"/>.</summary>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the scalar value, with <see cref="DBNull"/> normalized to <see langword="null"/>.</returns>
    public async Task<object?> ExecuteScalarAsync(string sql, IReadOnlyList<Parameter> parameters, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    /// <summary>
    /// Executes a mutation that returns rows (<c>INSERT ... RETURNING</c>/<c>OUTPUT</c>) and materialises
    /// every returned row through <paramref name="mapper"/>.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <returns>The returned rows, materialized in result-set order.</returns>
    public List<TResult> ExecuteReader<TResult>(string sql, IReadOnlyList<Parameter> parameters, Func<IDataRecord, TResult> mapper)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        using var reader = cmd.ExecuteReader();

        var list = new List<TResult>();
        while (reader.Read())
            list.Add(mapper(reader));

        return list;
    }

    /// <summary>Asynchronously executes a mutation that returns rows and materialises every returned row.</summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="sql">The parameterised statement text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the returned rows, materialized in result-set order.</returns>
    public async Task<List<TResult>> ExecuteReaderAsync<TResult>(string sql, IReadOnlyList<Parameter> parameters, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken)
    {
        CheckDisposed();
        using var cmd = CreateMutationCommand(sql, parameters);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var list = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(mapper(reader));

        return list;
    }

    /// <summary>
    /// Single entry guard for the public execution overloads: a context only executes the command
    /// representation it produces itself (mixing storage backends is not supported). Foreign
    /// implementations are rejected here, once, instead of in every overload.
    /// </summary>
    private static DbPreparedQueryCommand<TResult> AsDbCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
        => preparedQueryCommand as DbPreparedQueryCommand<TResult>
           ?? throw new ArgumentException($"Expected {nameof(DbPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));

    /// <summary>
    /// Resolves the enumerator a streaming terminal needs. A command prepared with
    /// <c>nonStreamUsing: true</c> (the <see cref="QueryCommand{TResult}.Prepare"/> default) is optimised for
    /// buffered/scalar results and never gets one, so fail with an actionable message instead of a
    /// NullReferenceException.
    /// </summary>
    private static ResultSetEnumerator<TResult> RequireEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
        => compiledQuery.Enumerator
           ?? throw new InvalidOperationException(
               "This prepared command has no enumerator because it was created with nonStreamUsing: true, "
               + "which is optimised for buffered and scalar results. Use Prepare(nonStreamUsing: false) "
               + "to stream (ToAsyncEnumerable/ToEnumerable/CreateEnumerator).");

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(_connectionManager, _createParam, @params, cancellationToken, _currentTransaction);
        return sqlEnumerator;
    }

    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(_connectionManager, _createParam, @params, CancellationToken.None, _currentTransaction);
        sqlEnumerator.InitReader(@params);

        return sqlEnumerator;
    }

    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }

    /// <summary>
    /// Converts a raw scalar value to <typeparamref name="TResult"/>. SQLite has no bool type
    /// (comparisons/EXISTS come back as INTEGER/<see cref="long"/>), so typed fast paths are used
    /// to avoid boxing and <see cref="IConvertible"/> dispatch for the common scalar types.
    /// </summary>
    private static TResult ConvertScalar<TResult>(object value)
    {
        // Already the wanted runtime type (bool/int/long/double/decimal/string/...): unbox, no
        // IConvertible dispatch. A boxed underlying value also satisfies `is T?`.
        if (value is TResult result) return result;

        // A nullable projection (TResult = T?) is converted through its underlying type:
        // Convert.ChangeType does not understand Nullable<T>, and the typed fast paths below compare
        // against the non-nullable type. Boxing the underlying value is a valid unboxing to T?.
        // This is what lets a provider's wider/native type map onto a nullable projection, e.g.
        // MySQL timestampdiff BIGINT -> int? or SQLite datetime TEXT -> DateTime?.
        if (Nullable.GetUnderlyingType(typeof(TResult)) is { } underlyingType)
            return (TResult)Convert.ChangeType(value, underlyingType)!;

        var type = typeof(TResult);

        if (type == typeof(bool))
        {
            // SQLite has no bool type: comparisons/EXISTS come back as INTEGER (long).
            var v = value switch
            {
                long l => l != 0,
                int i => i != 0,
                short s => s != 0,
                byte b => b != 0,
                _ => Convert.ToBoolean(value),
            };
            return Unsafe.As<bool, TResult>(ref v);
        }
        if (type == typeof(int))
        {
            var v = value switch
            {
                // Convert.ToInt32(long) is checked; a narrowing cast must throw on overflow too.
                long l => checked((int)l),
                _ => Convert.ToInt32(value),
            };
            return Unsafe.As<int, TResult>(ref v);
        }
        if (type == typeof(long))
        {
            var v = value switch
            {
                int i => i,
                _ => Convert.ToInt64(value),
            };
            return Unsafe.As<long, TResult>(ref v);
        }
        if (type == typeof(double))
        {
            var v = value switch
            {
                // Widening float->double; Convert.ToDouble(double) would round-trip IConvertible.
                float f => f,
                _ => Convert.ToDouble(value),
            };
            return Unsafe.As<double, TResult>(ref v);
        }

        return (TResult)Convert.ChangeType(value, type);
    }

    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);

        var r = sqlCommand.ExecuteScalar();

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        CheckDisposed();
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);

        var r = await sqlCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }

    [Conditional("DEBUG")]
    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed(), nameof(DataContext));
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, true)!;

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return (await ExecuteScalar<TResult>(preparedQueryCommand, @params, true, cancellationToken))!;

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }

    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, false);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return await ExecuteScalar<TResult>(preparedQueryCommand, @params, false, cancellationToken);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);

        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }
}
