using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D5 task C (MariaDB): the raw-source <c>BindEntity</c> semantics on the MariaDB container. MariaDB
/// does not derive the shared <see cref="CommonTestSuite"/> and has no shared query-filter fixture
/// table, so the test owns its schema and seed. Runs under <c>DOCKER_HOST</c> (or an external server
/// named by <c>NEXTORM_MARIADB_CONNECTION</c>).
/// </summary>
public sealed class MariaDbRawSourceBindingTests : IDisposable
{
    private const string TenantKey = "mariadb_rsb_tenant";

    private static int _seed = -740_000_000;

    private static int NextBase() => Interlocked.Add(ref _seed, -10);

    private readonly MariaDbDataContext _ctx;

    public MariaDbRawSourceBindingTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
        _ctx.From<MariaDbRawBindEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter(e => !e.IsDeleted));
    }

    public void Dispose() => _ctx.Dispose();

    private void Seed()
    {
        Execute("drop table if exists maria_rsb_entity");
        Execute("create table maria_rsb_entity (id int not null primary key, tenant_id int not null, is_deleted tinyint(1) not null, name varchar(64))");
    }

    private void Execute(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var cmd = _ctx.CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private void SeedRows(int b)
        => _ctx.CreateInsertBuilder<MariaDbRawBindEntity>().IgnoreFilters().Values([
            new MariaDbRawBindEntity { Id = b, TenantId = 1, IsDeleted = false, Name = "maria-active" },
            new MariaDbRawBindEntity { Id = b - 1, TenantId = 1, IsDeleted = true, Name = "maria-deleted" },
            new MariaDbRawBindEntity { Id = b - 2, TenantId = 2, IsDeleted = false, Name = "maria-foreign" },
        ]).Insert();

    [Fact]
    public void BoundCompatible_ShouldReturnFilteredRows()
    {
        _ctx.Properties[TenantKey] = 1;
        var b = NextBase();
        SeedRows(b);

        var rows = _ctx.FromSql("select id, tenant_id, is_deleted, name from maria_rsb_entity")
            .BindEntity<MariaDbRawBindEntity>(["id", "tenant_id", "is_deleted", "name"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b]);
    }

    [Fact]
    public void PartiallyIncompatible_ShouldApplyCompatibleAndSkipRest()
    {
        _ctx.Properties[TenantKey] = 1;
        var b = NextBase();
        SeedRows(b);

        var rows = _ctx.FromSql("select id, is_deleted from maria_rsb_entity")
            .BindEntity<MariaDbRawBindEntity>(["id", "is_deleted"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 2], "the skipped tenant filter leaves the active foreign row");
    }

    [Fact]
    public void Unbound_ShouldReturnEveryRow()
    {
        _ctx.Properties[TenantKey] = 1;
        var b = NextBase();
        SeedRows(b);

        var rows = _ctx.FromSql("select id from maria_rsb_entity")
            .Where(t => t["id"].AsInt >= b - 2 && t["id"].AsInt <= b)
            .Select(t => t["id"].AsInt)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 1, b - 2]);
    }

    [Fact]
    public void SkippedFilter_ShouldBeObservableThroughTheLogger()
    {
        _ctx.Properties[TenantKey] = 1;
        var b = NextBase();
        SeedRows(b);

        var sink = new LogSink();
        using var logged = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseLoggerFactory(LoggerFactory.Create(builder => builder.AddProvider(sink))));
        logged.Properties[TenantKey] = 1;

        var rows = logged.FromSql("select id, is_deleted from maria_rsb_entity")
            .BindEntity<MariaDbRawBindEntity>(["id", "is_deleted"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 2]);
        sink.Messages.Should().Contain(m => m.StartsWith("RawSourceFilterSkipped:", StringComparison.Ordinal));
    }

    private sealed class LogSink : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(LogSink owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    owner._messages.Add(formatter(state, exception));
            }
        }
    }
}

/// <summary>The self-owned MariaDB table used by the raw-source binding integration tests.</summary>
[SqlTable("maria_rsb_entity")]
public sealed class MariaDbRawBindEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
