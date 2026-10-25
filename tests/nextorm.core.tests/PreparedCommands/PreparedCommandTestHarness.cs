using System.Collections;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// Shared ADO.NET harness for the T333 prepared-command tests. Everything here is a deterministic,
/// in-process SQLite member (no container, no network): a <see cref="DbConnection"/> subclass that
/// mints instrumented commands, the matching command/reader pair with on-demand fault injection, a
/// fixed connection manager and a <see cref="DataContext"/> that only provides the executor's context
/// role. The prepared commands are built through the product's own <see cref="PreparedCommandFactory"/>
/// and executed through the product's own <c>QueryExecutor</c>/<c>ResultSetEnumerator</c> paths, so the
/// assertions observe real ownership code rather than a re-implementation of it.
/// </summary>
internal static class PreparedCommandHarness
{
    /// <summary>A query result type mapped to a real SQLite table used by the planner-based tests.</summary>
    [SqlTable("pdca_t333_rows")]
    internal sealed class PreparedProbeRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>Creates and seeds a file-backed SQLite database, returning its path.</summary>
    internal static string CreateSeededFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t333-{Guid.NewGuid():N}.db");
        using var seed = new SqliteConnection($"Data Source={path}");
        seed.Open();
        using var command = seed.CreateCommand();
        command.CommandText =
            "create table pdca_t333_rows (id integer primary key, name text);"
            + "insert into pdca_t333_rows (id, name) values (1, 'alpha'), (2, 'beta');";
        command.ExecuteNonQuery();
        return path;
    }

    /// <summary>Opens an instrumented connection over an already seeded database file.</summary>
    internal static ProbeSqliteConnection OpenProbe(string path)
    {
        var connection = new ProbeSqliteConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }

    /// <summary>Builds a prepared command over the product's immutable execution factory.</summary>
    internal static DbPreparedQueryCommand<TResult> FactoryPrepared<TResult>(
        string sql,
        PreparedCommandCache cache,
        Func<IDataRecord, TResult>? map = null)
        => new(
            new PreparedCommandFactory(sql, null, []),
            [],
            map,
            new PreparedCommandOptions(Sql: sql) { Cache = cache });

    /// <summary>Builds a prepared integer command reading the first result column.</summary>
    internal static DbPreparedQueryCommand<int> IntPrepared(string sql, PreparedCommandCache cache)
        => FactoryPrepared(sql, cache, static record => record.GetInt32(0));

    /// <summary>The product's command-aware parameter factory over an instrumented SQLite command.</summary>
    internal static DbParameter CreateParam(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    /// <summary>The command-unaware factory the planner uses for connection-free plan descriptors.</summary>
    internal static DbParameter CreateParamSimple(string name, object? value)
        => new SqliteParameter { ParameterName = name, Value = value ?? DBNull.Value };

    /// <summary>Creates the production executor over a fixed connection and a mutable transaction slot.</summary>
    internal static QueryExecutor CreateExecutor(
        IDataContext context,
        IConnectionManager connectionManager,
        Func<DbTransaction?>? currentTransaction = null)
        => new(
            context,
            connectionManager,
            CreateParam,
            CreateParamSimple,
            static (_, _) => throw new NotSupportedException("No procedure parameter is used by these tests."),
            new LoggingOptions(null),
            static () => false,
            currentTransaction ?? (() => null),
            new InterceptorHooks([], []));

    /// <summary>Creates a streaming enumerator over the production enumerator type.</summary>
    internal static ResultSetEnumerator<TResult> CreateEnumerator<TResult>(
        DbPreparedQueryCommand<TResult> prepared,
        IConnectionManager connectionManager,
        object[]? @params = null,
        CancellationToken cancellationToken = default)
    {
        var enumerator = new ResultSetEnumerator<TResult>(prepared);
        enumerator.InitEnumerator(
            connectionManager,
            CreateParam,
            @params,
            cancellationToken,
            static () => null,
            new InterceptorHooks([], []),
            new ProbeHarnessContext());
        return enumerator;
    }

    /// <summary>Reads every remaining row of a command owner and disposes it.</summary>
    internal static List<object?[]> ReadAll(CommandReaderOwner owner, int fieldCount)
    {
        var rows = new List<object?[]>();
        try
        {
            while (owner.Reader.Read())
            {
                var row = new object?[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                    row[i] = owner.Reader.IsDBNull(i) ? null : owner.Reader.GetValue(i);
                rows.Add(row);
            }
        }
        finally
        {
            owner.Dispose();
        }

        return rows;
    }
}

/// <summary>
/// An instrumented <see cref="SqliteConnection"/>: mints <see cref="ProbeSqliteCommand"/> instances,
/// counts their creation and records them, and can fail the next <c>CreateCommand</c>/<c>Open</c> calls
/// on demand. It is a real SQLite connection, so the executions it drives are real ADO.NET round trips.
/// </summary>
internal sealed class ProbeSqliteConnection(string connectionString) : SqliteConnection(connectionString)
{
    private readonly List<ProbeSqliteCommand> _created = [];

    /// <summary>Every command this connection has minted, in creation order.</summary>
    internal IReadOnlyList<ProbeSqliteCommand> CreatedCommands => _created;

    /// <summary>Number of commands this connection has minted.</summary>
    internal int CreatedCommandCount => _created.Count;

    /// <summary>Number of commands this connection has minted that were disposed.</summary>
    internal int DisposedCommandCount
    {
        get
        {
            var count = 0;
            foreach (var command in _created)
                count += command.DisposeCount;
            return count;
        }
    }

    /// <summary>How many more <c>CreateCommand</c> calls fail before succeeding again.</summary>
    internal int FailCreateCommandTimes { get; set; }

    /// <summary>The failure thrown by a failing <c>CreateCommand</c> call.</summary>
    internal Exception CreateCommandFailure { get; set; } = new InvalidOperationException("Simulated CreateCommand failure.");

    /// <summary>When set, opening the connection fails.</summary>
    internal Exception? OpenFailure { get; set; }

    /// <summary>When set, every command this connection mints wraps its provider reader with this factory.</summary>
    internal Func<DbDataReader, ProbeDbDataReader>? ReaderWrapper { get; set; }

    /// <summary>How many more commands minted by this connection fail their sync reader creation.</summary>
    internal int FailExecuteReaderTimes { get; set; }

    /// <summary>How many more commands minted by this connection fail their async reader creation.</summary>
    internal int FailExecuteReaderAsyncTimes { get; set; }

    /// <summary>The failure thrown by a command's reader creation.</summary>
    internal Exception ExecuteFailure { get; set; } = new InvalidOperationException("Simulated command-execution failure.");

    internal ProbeSqliteCommand LastCommand => _created[^1];

    public override SqliteCommand CreateCommand()
    {
        if (FailCreateCommandTimes > 0)
        {
            FailCreateCommandTimes--;
            throw CreateCommandFailure;
        }

        var syncFailures = FailExecuteReaderTimes;
        var asyncFailures = FailExecuteReaderAsyncTimes;
        if (syncFailures > 0)
            FailExecuteReaderTimes--;
        if (asyncFailures > 0)
            FailExecuteReaderAsyncTimes--;

        var command = new ProbeSqliteCommand
        {
            Connection = this,
            ReaderWrapper = ReaderWrapper,
            FailExecuteReaderTimes = syncFailures,
            FailExecuteReaderAsyncTimes = asyncFailures,
            ExecuteFailure = ExecuteFailure,
        };
        _created.Add(command);
        return command;
    }

    public override void Open()
    {
        if (OpenFailure is not null)
            throw OpenFailure;

        base.Open();
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
        => OpenFailure is not null ? Task.FromException(OpenFailure) : base.OpenAsync(cancellationToken);
}

/// <summary>
/// An instrumented <see cref="SqliteCommand"/>: counts executions and disposals, can throw from
/// <c>ExecuteReader</c>/<c>ExecuteReaderAsync</c> a fixed number of times, and can wrap the provider
/// reader so a test controls row-level failures and cancellation.
/// </summary>
internal sealed class ProbeSqliteCommand : SqliteCommand
{
    private int _executeReaderCallCount;
    private int _executeReaderAsyncCallCount;
    private DbTransaction? _transaction;

    /// <summary>
    /// The transaction the prepared command attached. Kept on the probe so a simple
    /// <see cref="ProbeTransaction"/> (whose connection identity the product validates) can be bound
    /// without a provider transaction that requires real provider bookkeeping.
    /// </summary>
    protected override DbTransaction? DbTransaction
    {
        get => _transaction;
        set => _transaction = value;
    }

    /// <summary>How many times <c>ExecuteReader</c> was entered, including failing calls.</summary>
    internal int ExecuteReaderCallCount => Volatile.Read(ref _executeReaderCallCount);

    /// <summary>How many times <c>ExecuteReaderAsync</c> was entered, including failing calls.</summary>
    internal int ExecuteReaderAsyncCallCount => Volatile.Read(ref _executeReaderAsyncCallCount);

    /// <summary>How many provider disposals this command received.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>How many more sync reader creations fail before succeeding again.</summary>
    internal int FailExecuteReaderTimes { get; set; }

    /// <summary>How many more async reader creations fail before succeeding again.</summary>
    internal int FailExecuteReaderAsyncTimes { get; set; }

    /// <summary>The failure thrown by a failing reader creation.</summary>
    internal Exception ExecuteFailure { get; set; } = new InvalidOperationException("Simulated command-execution failure.");

    /// <summary>When set, every successful reader creation is wrapped by this factory.</summary>
    internal Func<DbDataReader, ProbeDbDataReader>? ReaderWrapper { get; set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeCount++;

        base.Dispose(disposing);
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        Interlocked.Increment(ref _executeReaderCallCount);
        if (FailExecuteReaderTimes > 0)
        {
            FailExecuteReaderTimes--;
            throw ExecuteFailure;
        }

        var reader = base.ExecuteDbDataReader(behavior);
        return ReaderWrapper is null ? reader : ReaderWrapper(reader);
    }

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _executeReaderAsyncCallCount);
        if (FailExecuteReaderAsyncTimes > 0)
        {
            FailExecuteReaderAsyncTimes--;
            throw ExecuteFailure;
        }

        var reader = await base.ExecuteDbDataReaderAsync(behavior, cancellationToken).ConfigureAwait(false);
        return ReaderWrapper is null ? reader : ReaderWrapper(reader);
    }
}

/// <summary>
/// A delegating <see cref="DbDataReader"/> that counts reads/disposals and can throw or cancel on the
/// n-th read, so ownership on exceptional stream paths is observable without timing.
/// </summary>
internal sealed class ProbeDbDataReader(DbDataReader inner) : DbDataReader
{
    private int _readCount;

    /// <summary>How many read attempts this reader saw.</summary>
    internal int ReadCount => Volatile.Read(ref _readCount);

    /// <summary>How many disposals this reader received.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>The 1-based read attempt that throws <see cref="ReadFailure"/>, or <see langword="null"/>.</summary>
    internal int? ThrowOnReadNumber { get; set; }

    /// <summary>The 1-based read attempt that throws <see cref="OperationCanceledException"/>, or <see langword="null"/>.</summary>
    internal int? CancelOnReadNumber { get; set; }

    /// <summary>The failure thrown on the configured read attempt.</summary>
    internal Exception ReadFailure { get; set; } = new InvalidOperationException("Simulated read failure.");

    internal Exception? DisposeFailure { get; set; }

    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;

    public override bool Read()
    {
        var attempt = Interlocked.Increment(ref _readCount);
        ThrowIfInjected(attempt);
        return inner.Read();
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _readCount);
        ThrowIfInjected(attempt);
        return await inner.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfInjected(int attempt)
    {
        if (ThrowOnReadNumber == attempt)
            throw ReadFailure;
        if (CancelOnReadNumber == attempt)
            throw new OperationCanceledException("Simulated read cancellation.");
    }

    public override bool NextResult() => inner.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => inner.NextResultAsync(cancellationToken);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
            if (DisposeFailure is not null)
                throw DisposeFailure;
        }

        inner.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        DisposeCount++;
        return inner.DisposeAsync();
    }
}

/// <summary>A fixed connection manager for an already-open instrumented connection.</summary>
internal sealed class ProbeConnectionManager(DbConnection connection) : IConnectionManager
{
    public DbConnection GetConnection() => connection;

    public void EnsureConnectionOpen()
    {
        if (connection.State == ConnectionState.Closed)
            connection.Open();
    }

    public Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
        => connection.State == ConnectionState.Closed
            ? connection.OpenAsync(cancellationToken)
            : Task.CompletedTask;
}

/// <summary>A mutable transaction slot, so one executor can change its active transaction between executions.</summary>
internal sealed class TransactionSlot
{
    internal DbTransaction? Value { get; set; }
}

/// <summary>
/// A transaction whose <see cref="DbTransaction.Connection"/> can be a foreign connection or throw
/// <see cref="ObjectDisposedException"/>, so the binding validation is testable without a provider
/// transaction that cannot be constructed for a different connection.
/// </summary>
internal sealed class ProbeTransaction(DbConnection connection) : DbTransaction
{
    private bool _completed;

    protected override DbConnection DbConnection
        => _completed
            ? throw new ObjectDisposedException(nameof(ProbeTransaction))
            : connection;

    public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;

    public void MarkDisposed() => _completed = true;

    public override void Commit() => _completed = true;

    public override void Rollback() => _completed = true;

    protected override void Dispose(bool disposing) => _completed = true;
}

/// <summary>
/// The smallest <see cref="DataContext"/> the production executor needs: it only supplies the context
/// role (command timeout and the disposed flag come from the base type). It never creates a connection
/// or a dialect; every test drives the executor explicitly.
/// </summary>
internal sealed class ProbeHarnessContext : DataContext
{
    public ProbeHarnessContext()
        : base(new DataContextBuilder())
    {
    }

    public override ISqlDialect Dialect => throw new NotSupportedException("The probe executor never renders SQL.");

    public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

    protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
}
