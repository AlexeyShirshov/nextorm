using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Terminal-level coverage for <c>ToDataReader</c>/<c>ToDataReaderAsync</c> on SQLite (issue #134): the
/// locator-free result path emits the projection unchanged, the per-call command never mutates the
/// shared command's cache state, and the lazy temp-table guard names the reader terminal while CSV
/// keeps its own wording.
/// </summary>
public class ToDataReaderSqliteTests
{
    [SqlTable("reader_probe")]
    private sealed class ReaderProbeEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("data")]
        public byte[]? Data { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    /// <summary>Prepares through the same buffered route the reader terminal uses (no connection opened).</summary>
    private static string BufferedSql<T>(IDataContext ctx, QueryCommand<T> command)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None)).DbCommand.CommandText);

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        /// <summary>The most recently executed command, captured for ownership/disposal assertions.</summary>
        public DbCommand? LastCommand { get; private set; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            Statements.Add(eventData.Sql ?? string.Empty);
            LastCommand = command;
        }
    }

    /// <summary>
    /// Test-only connection hook: the provider mints every command through <see cref="CreateCommand"/>,
    /// so returning a <see cref="TrackingSqliteCommand"/> makes per-call disposal directly observable
    /// (the stock <c>SqliteCommand.Dispose</c> keeps <c>Connection</c> set, so it cannot be probed).
    /// </summary>
    private sealed class TrackingSqliteConnection(string connectionString) : SqliteConnection(connectionString)
    {
        public List<TrackingSqliteCommand> CreatedCommands { get; } = [];

        /// <summary>When set, every command minted by this connection fails on both execution paths.</summary>
        public bool FailExecute { get; set; }

        /// <summary>When set, opening the connection fails instead of opening it.</summary>
        public bool FailOpen { get; set; }

        public override SqliteCommand CreateCommand()
        {
            var command = new TrackingSqliteCommand
            {
                Connection = this,
                CommandTimeout = DefaultTimeout,
                Transaction = Transaction,
                FailExecute = FailExecute,
            };
            CreatedCommands.Add(command);
            return command;
        }

        public override void Open()
        {
            if (FailOpen)
                throw new InvalidOperationException("Simulated connection-open failure.");

            base.Open();
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
            => FailOpen
                ? Task.FromException(new InvalidOperationException("Simulated connection-open failure."))
                : base.OpenAsync(cancellationToken);
    }

    private sealed class TrackingSqliteCommand : SqliteCommand
    {
        public bool WasDisposed { get; private set; }

        /// <summary>When set, the command throws on the sync and the async execution path.</summary>
        public bool FailExecute { get; set; }

        /// <summary>Whether the provider reader is still attached (set by <c>ExecuteReader</c>).</summary>
        public bool HasOpenReader => DataReader is not null;

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (FailExecute)
                throw new InvalidOperationException("Simulated command-execution failure.");

            return base.ExecuteDbDataReader(behavior);
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
            => FailExecute
                ? Task.FromException<DbDataReader>(new InvalidOperationException("Simulated command-execution failure."))
                : base.ExecuteDbDataReaderAsync(behavior, cancellationToken);
    }

    private static SqliteDataContext CreateDb(out string path, SqlRecordingInterceptor? interceptor = null)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-todatareader-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "create table reader_probe (id integer primary key, name text, data blob);" +
                "insert into reader_probe (id, name, data) values (1, 'alpha', x'0102'), (2, null, null);";
            command.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        if (interceptor is not null)
            builder = builder.AddInterceptor(interceptor);

        return new SqliteDataContext($"Data Source={path}", builder);
    }

    [Fact]
    public void ToDataReaderProjection_Sqlite_ShouldNotAppendRowid()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<ReaderProbeEntity>().Select(x => new { x.Id, x.Name });

        var sql = BufferedSql(ctx, command);

        sql.Should().Be("select id, name from reader_probe");
        sql.Should().NotContain("rowid");
    }

    [Fact]
    public void ToDataReader_Sqlite_ReadsMultiColumnProjectionWithoutLocator()
    {
        var interceptor = new SqlRecordingInterceptor();
        var ctx = CreateDb(out var path, interceptor);
        try
        {
            using var reader = ctx.From<ReaderProbeEntity>()
                .Select(x => new { x.Id, x.Name })
                .ToDataReader();

            Normalize(interceptor.Statements.Single()).Should().Be("select id, name from reader_probe");
            reader.FieldCount.Should().Be(2, "the locator-free path must not append a rowid column");
            reader.GetName(0).Should().Be("id");
            reader.GetName(1).Should().Be("name");
            reader.GetOrdinal("name").Should().Be(1);

            var rows = new Dictionary<int, string?>();
            while (reader.Read())
                rows[reader.GetInt32(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);

            rows.Should().Equal(new Dictionary<int, string?> { [1] = "alpha", [2] = null });
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_ReadsMultiColumnProjectionWithoutLocator()
    {
        var ctx = CreateDb(out var path);
        try
        {
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .Select(x => new { x.Id, x.Name })
                .ToDataReaderAsync(TestContext.Current.CancellationToken);

            reader.FieldCount.Should().Be(2);
            reader.GetName(0).Should().Be("id");
            reader.GetName(1).Should().Be("name");

            var rows = new Dictionary<int, string?>();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                rows[reader.GetInt32(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);

            rows.Should().Equal(new Dictionary<int, string?> { [1] = "alpha", [2] = null });
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_ShouldNotMutateSharedCommandCache()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
            command.Cache.Should().BeTrue("a new command caches by default");

            using (var reader = command.ToDataReader())
            {
                reader.FieldCount.Should().Be(3);
                reader.Read().Should().BeTrue();
            }

            command.Cache.Should().BeTrue("the reader terminal must not mutate the shared command");

            // The per-call plan uses storeInCache: false, so a cached query still works afterwards and
            // the shared command can be read again.
            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
            using (var second = command.ToDataReader())
                second.Read().Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_SingleColumn_ShouldReadEveryRow()
    {
        var ctx = CreateDb(out var path);
        try
        {
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToDataReaderAsync(TestContext.Current.CancellationToken);

            reader.FieldCount.Should().Be(1, "a single-column projection stays single-column on the locator-free path");
            reader.GetName(0).Should().Be("id");

            var ids = new List<int>();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                ids.Add(reader.GetInt32(0));

            ids.Should().Equal(1, 2);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_SingleBlobProjection_ShouldReadWholeBufferedValue()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var reader = ctx.From<ReaderProbeEntity>()
                .OrderBy(x => x.Id)
                .Select(x => x.Data!)
                .ToDataReader();

            reader.FieldCount.Should().Be(1, "the locator-free path must not append a rowid column");
            reader.GetName(0).Should().Be("data");

            reader.Read().Should().BeTrue();
            ((byte[])reader.GetValue(0)).Should().Equal(new byte[] { 1, 2 }, "a SQLite blob is read whole (buffered, not chunked)");
            reader.Read().Should().BeTrue();
            reader.IsDBNull(0).Should().BeTrue("a null blob must round-trip as DBNull");
            reader.Read().Should().BeFalse();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_SingleBlobProjection_ShouldReadWholeBufferedValue()
    {
        var ctx = CreateDb(out var path);
        try
        {
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .OrderBy(x => x.Id)
                .Select(x => x.Data!)
                .ToDataReaderAsync(TestContext.Current.CancellationToken);

            reader.FieldCount.Should().Be(1, "the locator-free path must not append a rowid column");
            reader.GetName(0).Should().Be("data");

            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            ((byte[])reader.GetValue(0)).Should().Equal(new byte[] { 1, 2 }, "a SQLite blob is read whole (buffered, not chunked)");
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            reader.IsDBNull(0).Should().BeTrue("a null blob must round-trip as DBNull");
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_DefaultToken_WithParameters_ShouldBindArrayValues()
    {
        var ctx = CreateDb(out var path);
        try
        {
            // Default-token overload plus the object[] (array) parameter form: several values and a null.
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    P0 = SqlFunctions.Parameter<int>(0),
                    P1 = SqlFunctions.Parameter<string?>(1),
                    P2 = SqlFunctions.Parameter<int?>(2),
                    P3 = SqlFunctions.Parameter<string?>(3)
                })
                .ToDataReaderAsync(42, "beta", null, null);

            reader.FieldCount.Should().Be(4);
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(42);
            reader.GetString(1).Should().Be("beta");
            reader.IsDBNull(2).Should().BeTrue("a null parameter must round-trip as DBNull, not throw");
            reader.IsDBNull(3).Should().BeTrue();
            reader.Read().Should().BeFalse();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_WithParameters_ShouldBindSpanValues()
    {
        var ctx = CreateDb(out var path);
        try
        {
            // Span (params ReadOnlySpan) parameter form, sync: several values and a null.
            using var reader = ctx.From<ReaderProbeEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new
                {
                    P0 = SqlFunctions.Parameter<int>(0),
                    P1 = SqlFunctions.Parameter<string?>(1),
                    P2 = SqlFunctions.Parameter<int?>(2),
                    P3 = SqlFunctions.Parameter<string?>(3)
                })
                .ToDataReader(7, "gamma", null, null);

            reader.FieldCount.Should().Be(4);
            reader.Read().Should().BeTrue();
            reader.GetInt32(0).Should().Be(7);
            reader.GetString(1).Should().Be("gamma");
            reader.IsDBNull(2).Should().BeTrue();
            reader.IsDBNull(3).Should().BeTrue();
            reader.Read().Should().BeFalse();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_EmptyResult_ShouldReturnNoRows()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var reader = ctx.From<ReaderProbeEntity>()
                .Where(x => x.Id == 999)
                .Select(x => new { x.Id, x.Name })
                .ToDataReader();

            reader.FieldCount.Should().Be(2);
            reader.HasRows.Should().BeFalse("a zero-row projection must not report rows");
            reader.Read().Should().BeFalse("the first Read on an empty result returns false");
        }
        finally
        {
            // Disposal of the reader happened at the end of the using block; the context stays usable.
            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_EmptyResult_ShouldReturnNoRows()
    {
        var ctx = CreateDb(out var path);
        try
        {
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .Where(x => x.Id == 999)
                .Select(x => new { x.Id, x.Name })
                .ToDataReaderAsync(TestContext.Current.CancellationToken);

            reader.FieldCount.Should().Be(2);
            reader.HasRows.Should().BeFalse();
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }
        finally
        {
            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_ShouldNotMutateSharedCommandCache()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
            command.Cache.Should().BeTrue("a new command caches by default");

            await using (var reader = await command.ToDataReaderAsync())
            {
                reader.FieldCount.Should().Be(3);
                (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            }

            command.Cache.Should().BeTrue("the async reader terminal must not mutate the shared command");
            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
            await using (var second = await command.ToDataReaderAsync(TestContext.Current.CancellationToken))
                (await second.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_DisposesPerCallCommandAndReader()
    {
        using var connection = CreateTrackingDb();
        var interceptor = new SqlRecordingInterceptor();
        using var ctx = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(interceptor));

        TrackingSqliteCommand? executed = null;
        using (var reader = ctx.From<ReaderProbeEntity>()
                   .OrderBy(x => x.Id)
                   .Select(x => new { x.Id, x.Name })
                   .ToDataReader())
        {
            reader.Read().Should().BeTrue();
            executed = interceptor.LastCommand.Should().BeOfType<TrackingSqliteCommand>().Which;
            executed.HasOpenReader.Should().BeTrue("the provider reader is attached while the outer reader is alive");
            executed.WasDisposed.Should().BeFalse();
        }

        var disposed = executed!;
        disposed.HasOpenReader.Should().BeFalse("disposing the returned reader must release the provider reader");
        disposed.WasDisposed.Should().BeTrue("disposing the returned reader must dispose the per-call command");

        // The connection and context stay usable after the per-call command and reader are disposed.
        ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
    }

    [Fact]
    public void ToDataReader_Sqlite_ExecuteReaderFails_DisposesCommandAndContextRemainsUsable()
    {
        using var connection = CreateTrackingDb();
        using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
        command.Cache.Should().BeTrue("a new command caches by default");

        connection.FailExecute = true;
        var act = () => command.ToDataReader();

        act.Should().Throw<InvalidOperationException>("the provider execution failure must surface");

        var failed = connection.CreatedCommands.Last();
        failed.WasDisposed.Should().BeTrue("a reader that fails to execute must still release its per-call command");
        failed.HasOpenReader.Should().BeFalse("no provider reader was opened");
        command.Cache.Should().BeTrue("the failure path must not mutate the shared command's cache state");

        connection.FailExecute = false;
        using (var reader = command.ToDataReader())
        {
            reader.Read().Should().BeTrue("the same context is usable once the failure is cleared");
            reader.GetString(1).Should().Be("alpha");
        }

        ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_ExecuteReaderFails_DisposesCommandAndContextRemainsUsable()
    {
        using var connection = CreateTrackingDb();
        using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
        command.Cache.Should().BeTrue("a new command caches by default");

        connection.FailExecute = true;
        var act = async () => await command.ToDataReaderAsync();

        await act.Should().ThrowAsync<InvalidOperationException>("the provider execution failure must surface");

        var failed = connection.CreatedCommands.Last();
        failed.WasDisposed.Should().BeTrue("a reader that fails to execute must still release its per-call command");
        failed.HasOpenReader.Should().BeFalse("no provider reader was opened");
        command.Cache.Should().BeTrue("the failure path must not mutate the shared command's cache state");

        connection.FailExecute = false;
        await using (var reader = await command.ToDataReaderAsync(TestContext.Current.CancellationToken))
        {
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue("the same context is usable once the failure is cleared");
            reader.GetString(1).Should().Be("alpha");
        }

        ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
    }

    [Fact]
    public void ToDataReader_Sqlite_ConnectionOpenFails_DisposesCommandAndContextRemainsUsable()
    {
        var connection = CreateTrackingFileDb(out var path);
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        try
        {
            var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
            command.Cache.Should().BeTrue("a new command caches by default");

            connection.FailOpen = true;
            var act = () => command.ToDataReader();

            act.Should().Throw<InvalidOperationException>("the provider open failure must surface");

            var failed = connection.CreatedCommands.Last();
            failed.WasDisposed.Should().BeTrue("a command created before a failed connection open must be released");
            failed.HasOpenReader.Should().BeFalse("no provider reader was opened");
            command.Cache.Should().BeTrue("the failure path must not mutate the shared command's cache state");

            connection.FailOpen = false;
            using (var reader = command.ToDataReader())
            {
                reader.Read().Should().BeTrue("the same context is usable once the open failure is cleared");
                reader.GetString(1).Should().Be("alpha");
            }

            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
        }
        finally
        {
            ctx.Dispose();
            connection.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_ConnectionOpenFails_DisposesCommandAndContextRemainsUsable()
    {
        var connection = CreateTrackingFileDb(out var path);
        var ctx = new SqliteDataContext(connection, new DataContextBuilder());
        try
        {
            var command = ctx.From<ReaderProbeEntity>().OrderBy(x => x.Id).ToParentCommand();
            command.Cache.Should().BeTrue("a new command caches by default");

            connection.FailOpen = true;
            var act = async () => await command.ToDataReaderAsync();

            await act.Should().ThrowAsync<InvalidOperationException>("the provider open failure must surface");

            var failed = connection.CreatedCommands.Last();
            failed.WasDisposed.Should().BeTrue("a command created before a failed connection open must be released");
            failed.HasOpenReader.Should().BeFalse("no provider reader was opened");
            command.Cache.Should().BeTrue("the failure path must not mutate the shared command's cache state");

            connection.FailOpen = false;
            await using (var reader = await command.ToDataReaderAsync(TestContext.Current.CancellationToken))
            {
                (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue("the same context is usable once the open failure is cleared");
                reader.GetString(1).Should().Be("alpha");
            }

            ctx.From<ReaderProbeEntity>().Where(x => x.Id == 1).Select(x => x.Name).First().Should().Be("alpha");
        }
        finally
        {
            ctx.Dispose();
            connection.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ToDataReader_Sqlite_CancelAfterOpen_ShouldThrowOnNextRead()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var cts = new CancellationTokenSource();
            using var reader = ctx.From<ReaderProbeEntity>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name })
                .ToDataReader(cts.Token);

            reader.Read().Should().BeTrue("the first row is available before the token is cancelled");
            cts.Cancel();

            var act = () => reader.Read();

            act.Should().Throw<OperationCanceledException>("a token cancelled after the reader is open is honored");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ToDataReaderAsync_Sqlite_CancelAfterOpen_ShouldThrowOnNextRead()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var cts = new CancellationTokenSource();
            await using var reader = await ctx.From<ReaderProbeEntity>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name })
                .ToDataReaderAsync(cts.Token);

            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            cts.Cancel();

            var act = async () => await reader.ReadAsync(TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static TrackingSqliteConnection CreateTrackingDb()
    {
        var connection = new TrackingSqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "create table reader_probe (id integer primary key, name text, data blob);" +
            "insert into reader_probe (id, name, data) values (1, 'alpha', x'0102'), (2, null, null);";
        command.ExecuteNonQuery();
        return connection;
    }

    /// <summary>
    /// File-backed variant that leaves the connection <b>closed</b> with the probe table already created:
    /// the next terminal prepares its command on the closed connection, so a failed open happens after
    /// the planner minted the per-call command (the open-failure cleanup path).
    /// </summary>
    private static TrackingSqliteConnection CreateTrackingFileDb(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-todatareader-openfail-{Guid.NewGuid():N}.db");
        var connection = new TrackingSqliteConnection($"Data Source={path}");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "create table reader_probe (id integer primary key, name text, data blob);" +
                "insert into reader_probe (id, name, data) values (1, 'alpha', x'0102'), (2, null, null);";
            command.ExecuteNonQuery();
        }

        connection.Close();
        return connection;
    }

    [Fact]
    public void ToDataReader_TempTableSource_ShouldNameReaderTerminalWithoutCsvText()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        var query = ctx.From(source).Select(t => new { Id = t.GetInt32("id") });

        var act = () => query.ToDataReader();

        var exception = act.Should().Throw<NotSupportedException>().Which;
        exception.Message.Should().Contain("ToDataReader", "the guard names the calling terminal");
        exception.Message.Should().Contain("temporary table");
        exception.Message.Should().NotContain("WriteCsv", "the reader guard must not surface the CSV terminal");
        exception.Message.Should().NotContain("CSV", "the reader guard must not surface the CSV terminal");
    }

    [Fact]
    public async Task ToDataReaderAsync_TempTableSource_ShouldNameReaderTerminalWithoutCsvText()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        var query = ctx.From(source).Select(t => new { Id = t.GetInt32("id") });

        var act = async () => await query.ToDataReaderAsync();

        var exception = (await act.Should().ThrowAsync<NotSupportedException>()).Which;
        exception.Message.Should().Contain("ToDataReader");
        exception.Message.Should().Contain("temporary table");
        exception.Message.Should().NotContain("WriteCsv");
        exception.Message.Should().NotContain("CSV");
    }
}
