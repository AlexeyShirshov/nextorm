using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for the Phase 1 interceptors against a real SQLite database: the command
/// lifecycle events, the bound parameters visible to an interceptor, the failure path and the
/// connection lifecycle.
/// </summary>
public class InterceptorTests
{
    private sealed class RecordingQueryInterceptor : IQueryInterceptor
    {
        public List<string> Events { get; } = [];
        public CommandEventData LastEventData { get; private set; }
        public DbCommand? LastCommand { get; private set; }
        public TimeSpan LastElapsed { get; private set; }
        public Exception? Failure { get; private set; }

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
        {
            Events.Add("initialized");
            LastEventData = eventData;
            LastCommand = command;
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            Events.Add("executing");
            LastEventData = eventData;
            LastCommand = command;
        }

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        {
            Events.Add("executed");
            LastEventData = eventData;
            LastCommand = command;
            LastElapsed = elapsed;
        }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        {
            Events.Add("failed");
            LastEventData = eventData;
            LastCommand = command;
            Failure = exception;
        }
    }

    private sealed class RecordingConnectionInterceptor : IConnectionInterceptor
    {
        public List<string> Events { get; } = [];
        public ConnectionEventData LastEventData { get; private set; }

        public void ConnectionOpening(ConnectionEventData eventData)
        {
            Events.Add("opening");
            LastEventData = eventData;
        }

        public void ConnectionOpened(ConnectionEventData eventData)
        {
            Events.Add("opened");
            LastEventData = eventData;
        }
    }

    private sealed class OrderProbe : IQueryInterceptor
    {
        private readonly string _name;
        private readonly List<string> _log;

        public OrderProbe(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => _log.Add(_name);
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb(DataContextBuilder? builder = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-interceptors-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table simple_entity (id integer primary key);" +
                              "insert into simple_entity (id) values (42);" +
                              "insert into simple_entity (id) values (43);";
            cmd.ExecuteNonQuery();
        }

        return (new SqliteDataContext($"Data Source={path}", builder ?? new DataContextBuilder()), path);
    }

    [Fact]
    public void BuilderInterceptor_RecordsCommandLifecycleInOrder()
    {
        var interceptor = new RecordingQueryInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            ctx.PurgeQueryCache();

            var ids = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();

            ids.Should().Equal(42, 43);
            interceptor.Events.Should().Equal("initialized", "executing", "executed");
            interceptor.LastEventData.DataContext.Should().BeSameAs(ctx);
            interceptor.LastEventData.Sql.Should().NotBeNullOrEmpty();
            interceptor.LastCommand.Should().NotBeNull();
            interceptor.LastElapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Interceptor_SeesBoundParameters()
    {
        var interceptor = new RecordingQueryInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            ctx.PurgeQueryCache();
            var id = 43;

            var ids = ctx.From<ISimpleEntity>().Where(x => x.Id == id).Select(x => x.Id).ToList();

            ids.Should().Equal(43);
            interceptor.LastCommand!.Parameters.Count.Should().Be(1);
            Convert.ToInt32(interceptor.LastCommand!.Parameters[0].Value).Should().Be(43);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Interceptors_AreInvokedInRegistrationOrder()
    {
        var log = new List<string>();
        var builder = new DataContextBuilder()
            .AddInterceptor(new OrderProbe("first", log))
            .AddInterceptor(new OrderProbe("second", log));
        var (ctx, path) = CreateDb(builder);
        try
        {
            ctx.PurgeQueryCache();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();

            log.Should().Equal("first", "second");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PerInstanceAddInterceptor_RecordsCommandLifecycle()
    {
        var interceptor = new RecordingQueryInterceptor();
        var (ctx, path) = CreateDb();
        try
        {
            ctx.AddInterceptor(interceptor);
            ctx.PurgeQueryCache();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();

            interceptor.Events.Should().ContainInOrder("initialized", "executing", "executed");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CommandFailed_IsRaisedOnProviderError()
    {
        var interceptor = new RecordingQueryInterceptor();
        using var supplied = new SqliteConnection("Data Source=:memory:");
        supplied.Open();
        using var ctx = new SqliteDataContext(supplied, new DataContextBuilder().AddInterceptor(interceptor));
        ctx.PurgeQueryCache();

        // The entity is mapped, but the database has no such table: planning succeeds, execution throws.
        var act = () => ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();

        act.Should().Throw<SqliteException>();
        interceptor.Events.Should().Equal("initialized", "executing", "failed");
        interceptor.Failure.Should().BeOfType<SqliteException>();
    }

    [Fact]
    public async Task Streaming_RaisesCommandLifecycleEvents()
    {
        var interceptor = new RecordingQueryInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            ctx.PurgeQueryCache();

            var ids = new List<int>();
            await foreach (var id in ctx.From<ISimpleEntity>().Select(x => x.Id).ToAsyncEnumerable(TestContext.Current.CancellationToken))
                ids.Add(id);

            ids.Should().Equal(42, 43);
            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AsyncTerminal_RaisesCommandLifecycleEvents()
    {
        var interceptor = new RecordingQueryInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            ctx.PurgeQueryCache();

            var ids = await ctx.From<ISimpleEntity>().Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken);

            ids.Should().Equal(42, 43);
            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ConnectionInterceptor_RaisesOpeningAndOpenedOnce()
    {
        var interceptor = new RecordingConnectionInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            ctx.EnsureConnectionOpen();

            interceptor.Events.Should().Equal("opening", "opened");
            interceptor.LastEventData.DataContext.Should().BeSameAs(ctx);
            interceptor.LastEventData.Connection.Should().BeSameAs(ctx.GetConnection());

            ctx.EnsureConnectionOpen();

            interceptor.Events.Should().Equal("opening", "opened");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConnectionInterceptor_RaisesOpeningAndOpenedOnAsyncOpen()
    {
        var interceptor = new RecordingConnectionInterceptor();
        var (ctx, path) = CreateDb(new DataContextBuilder().AddInterceptor(interceptor));
        try
        {
            await ctx.EnsureConnectionOpenAsync(TestContext.Current.CancellationToken);

            interceptor.Events.Should().Equal("opening", "opened");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NoInterceptors_ContextStillExecutes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            ctx.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42, 43);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
