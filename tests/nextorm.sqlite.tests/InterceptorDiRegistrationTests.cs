using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for DI-registered interceptors (#221): a service-key registration is resolved
/// from the builder's own provider scope and observes the real SQLite command/connection lifecycle,
/// preserving registration order, exact-key deduplication and DI lifetimes.
/// </summary>
[Trait("Category", "T221")]
public class InterceptorDiRegistrationTests
{
    public sealed class Registry
    {
        public List<object> Query { get; } = [];
        public List<object> Connection { get; } = [];
    }

    public sealed class Log
    {
        public List<string> Entries { get; } = [];
    }

    public sealed class RecordingQueryInterceptor : IQueryInterceptor
    {
        public RecordingQueryInterceptor(Registry registry) => registry.Query.Add(this);

        public List<string> Events { get; } = [];

        public void CommandInitialized(CommandEventData eventData, DbCommand command) => Events.Add("initialized");

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Events.Add("executing");

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) => Events.Add("executed");

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception) => Events.Add("failed");
    }

    public sealed class RecordingConnectionInterceptor : IConnectionInterceptor
    {
        public RecordingConnectionInterceptor(Registry registry) => registry.Connection.Add(this);

        public List<string> Events { get; } = [];

        public void ConnectionOpening(ConnectionEventData eventData) => Events.Add("opening");

        public void ConnectionOpened(ConnectionEventData eventData) => Events.Add("opened");
    }

    public sealed class DualInterceptor : IQueryInterceptor, IConnectionInterceptor
    {
        public DualInterceptor(Log log) => Log = log;

        public Log Log { get; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Log.Entries.Add("executing");

        public void ConnectionOpening(ConnectionEventData eventData) => Log.Entries.Add("opening");
    }

    public sealed class OrderProbeFirst : IQueryInterceptor
    {
        private readonly Log _log;

        public OrderProbeFirst(Log log) => _log = log;

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => _log.Entries.Add("first");
    }

    public sealed class OrderProbeSecond : IQueryInterceptor
    {
        private readonly Log _log;

        public OrderProbeSecond(Log log) => _log = log;

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => _log.Entries.Add("second");
    }

    private static string CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t221-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table simple_entity (id integer primary key);" +
                              "insert into simple_entity (id) values (42);" +
                              "insert into simple_entity (id) values (43);";
            cmd.ExecuteNonQuery();
        }

        return path;
    }

    private static ServiceProvider CreateProvider(
        string path,
        Action<IServiceCollection> register,
        Action<DataContextBuilder> configure)
    {
        _ = path;
        var services = new ServiceCollection();
        services.AddSingleton<Registry>();
        register(services);
        services.AddNextOrmContext(builder => configure(builder));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DiTypeQueryInterceptor_ObservesCommandLifecycle()
    {
        var path = CreateDb();
        try
        {
            using var provider = CreateProvider(
                path,
                services => services.AddScoped<RecordingQueryInterceptor>(),
                builder => builder.UseSqlite(path).AddInterceptor<RecordingQueryInterceptor>());
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var interceptor = scope.ServiceProvider.GetRequiredService<RecordingQueryInterceptor>();

            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42, 43);

            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DiTypeConnectionInterceptor_ObservesConnectionOpen()
    {
        var path = CreateDb();
        try
        {
            using var provider = CreateProvider(
                path,
                services => services.AddScoped<RecordingConnectionInterceptor>(),
                builder => builder.UseSqlite(path).AddInterceptor<RecordingConnectionInterceptor>());
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var interceptor = scope.ServiceProvider.GetRequiredService<RecordingConnectionInterceptor>();

            ((DataContext)context).EnsureConnectionOpen();

            interceptor.Events.Should().Equal("opening", "opened");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MixedInstanceAndType_AreInvokedInRegistrationOrder()
    {
        var path = CreateDb();
        try
        {
            var log = new Log();
            using var provider = CreateProvider(
                path,
                services =>
                {
                    services.AddSingleton(log);
                    services.AddScoped<OrderProbeSecond>();
                },
                builder => builder.UseSqlite(path)
                    .AddInterceptor(new OrderProbeFirst(log))
                    .AddInterceptor<OrderProbeSecond>());
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();

            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList();

            log.Entries.Should().Equal("first", "second");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DuplicateTypeEntry_IsResolvedAndInvokedOnce()
    {
        var path = CreateDb();
        try
        {
            using var provider = CreateProvider(
                path,
                services => services.AddScoped<RecordingQueryInterceptor>(),
                builder => builder.UseSqlite(path)
                    .AddInterceptor<RecordingQueryInterceptor>()
                    .AddInterceptor<RecordingQueryInterceptor>());
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var interceptor = scope.ServiceProvider.GetRequiredService<RecordingQueryInterceptor>();

            scope.ServiceProvider.GetRequiredService<Registry>().Query.Should().ContainSingle();

            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList();

            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DualInterfaceInstanceRegisteredAsQuery_ServesBothPipelines()
    {
        var path = CreateDb();
        try
        {
            var log = new Log();
            var dual = new DualInterceptor(log);
            using var provider = CreateProvider(
                path,
                _ => { },
                builder => builder.UseSqlite(path).AddInterceptor((IQueryInterceptor)dual));
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();

            ((DataContext)context).EnsureConnectionOpen();
            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList();

            log.Entries.Should().Contain("opening");
            log.Entries.Should().Contain("executing");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DualInterfaceInstanceRegisteredAsConnection_ServesBothPipelines()
    {
        var path = CreateDb();
        try
        {
            var log = new Log();
            var dual = new DualInterceptor(log);
            using var provider = CreateProvider(
                path,
                _ => { },
                builder => builder.UseSqlite(path).AddInterceptor((IConnectionInterceptor)dual));
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();

            ((DataContext)context).EnsureConnectionOpen();
            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList();

            log.Entries.Should().Contain("opening");
            log.Entries.Should().Contain("executing");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TransientInterceptor_IsResolvedPerCreation()
    {
        var path = CreateDb();
        try
        {
            using var provider = CreateProvider(
                path,
                services => services.AddTransient<RecordingQueryInterceptor>(),
                builder => builder.UseSqlite(path).AddInterceptor<RecordingQueryInterceptor>());
            using var scope = provider.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<Registry>();
            var builder = scope.ServiceProvider.GetRequiredService<DataContextBuilder>();

            using (builder.CreateDataContext())
            using (builder.CreateDataContext())
            {
                registry.Query.Should().HaveCount(2);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DiTypeInterceptor_ObservesJsonStream()
    {
        var path = CreateDb();
        try
        {
            using var provider = CreateProvider(
                path,
                services => services.AddScoped<RecordingQueryInterceptor>(),
                builder => builder.UseSqlite(path).AddInterceptor<RecordingQueryInterceptor>());
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
            var interceptor = scope.ServiceProvider.GetRequiredService<RecordingQueryInterceptor>();

            context.PurgeQueryCache();
            using var stream = new MemoryStream();
            context.From<ISimpleEntity>().Select(x => new { x.Id }).WriteJson(stream);

            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StandaloneInstanceOnlyBuilder_StillObservesLifecycle()
    {
        var path = CreateDb();
        try
        {
            var registry = new Registry();
            var interceptor = new RecordingQueryInterceptor(registry);
            var builder = new DataContextBuilder().UseSqlite(path).AddInterceptor(interceptor);
            using var context = builder.CreateDataContext();

            context.PurgeQueryCache();
            context.From<ISimpleEntity>().Select(x => x.Id).ToList().Should().Equal(42, 43);

            interceptor.Events.Should().Equal("initialized", "executing", "executed");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
