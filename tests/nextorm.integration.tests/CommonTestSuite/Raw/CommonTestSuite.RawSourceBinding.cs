using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D5 task C: the database-backed semantics of global query filters injected into a raw
/// <c>FromSql</c> source explicitly bound with <c>BindEntity</c>. Runs on every provider that derives
/// <see cref="CommonTestSuite"/> (SQLite, PostgreSQL, SQL Server, MySQL) against the shared
/// <c>query_filter_entity</c> fixture table; ClickHouse and MariaDB have their own containers and
/// coverage.
/// </summary>
public abstract partial class CommonTestSuite
{
    private static int _rawBindSeed = -730_000_000;

    private static int NextRawBindBase() => Interlocked.Add(ref _rawBindSeed, -10);

    private const string AllRawBindColumnsSql = "select id, tenant_id, is_deleted, name from query_filter_entity";

    private IDataContext RawBindContext(int tenant)
    {
        var ctx = _sut.DataProvider;
        ctx.Properties[QueryFilterFixtures.TenantKey] = tenant;
        return ctx;
    }

    private void SeedRawBindRows(params QueryFilterEntity[] rows)
        => _sut.DataProvider.CreateInsertBuilder<QueryFilterEntity>().IgnoreFilters().Values(rows).Insert();

    private static QueryFilterEntity RawBindActive(int id)
        => new() { Id = id, TenantId = 1, IsDeleted = false, Name = "raw-bind-active" };

    private static QueryFilterEntity RawBindDeleted(int id)
        => new() { Id = id, TenantId = 1, IsDeleted = true, Name = "raw-bind-deleted" };

    private static QueryFilterEntity RawBindForeign(int id)
        => new() { Id = id, TenantId = 2, IsDeleted = false, Name = "raw-bind-foreign" };

    [Fact]
    public void RawSourceBinding_BoundCompatible_ShouldReturnFilteredRows()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        var rows = ctx.FromSql(AllRawBindColumnsSql)
            .BindEntity<QueryFilterEntity>(["id", "tenant_id", "is_deleted", "name"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b], "the tenant and soft-delete filters are injected into the raw source");
    }

    [Fact]
    public void RawSourceBinding_PartiallyIncompatible_ShouldApplyCompatibleAndSkipRest()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        // tenant_id is not declared, so the keyed tenant filter is skipped; the anonymous soft-delete
        // filter only needs is_deleted and stays active, so the active foreign tenant row survives.
        var rows = ctx.FromSql("select id, is_deleted from query_filter_entity")
            .BindEntity<QueryFilterEntity>(["id", "is_deleted"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 2], "the compatible filter excluded the soft-deleted row only");
    }

    [Fact]
    public void RawSourceBinding_Unbound_ShouldReturnEveryRow()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        var rows = ctx.FromSql("select id from query_filter_entity")
            .Where(t => t["id"].AsInt >= b - 2 && t["id"].AsInt <= b)
            .Select(t => t["id"].AsInt)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 1, b - 2], "an unbound raw source is never filtered");
    }

    [Fact]
    public void RawSourceBinding_JoinedBoundSource_ShouldFilterOnlyTheBoundSide()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        var bound = ctx.FromSql(AllRawBindColumnsSql)
            .BindEntity<QueryFilterEntity>(["id", "tenant_id", "is_deleted", "name"]);

        var rows = ctx.From<RawBindJoinMainEntity>()
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Join(bound, (l, r) => l.Id == r.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b], "the joined bound source's filters restrict the join, not the main side");
    }

    [Fact]
    public void RawSourceBinding_SkippedFilter_ShouldBeObservableThroughTheLogger()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        var sink = new RawBindLogSink();
        using var logged = Provider.CreateContext(LoggerFactory.Create(builder => builder.AddProvider(sink)));
        logged.Properties[QueryFilterFixtures.TenantKey] = 1;

        var rows = logged.FromSql("select id, is_deleted from query_filter_entity")
            .BindEntity<QueryFilterEntity>(["id", "is_deleted"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .Select(x => x.Id)
            .ToList();

        rows.Should().BeEquivalentTo([b, b - 2]);
        sink.Messages.Should().Contain(
            m => m.StartsWith("RawSourceFilterSkipped:", StringComparison.Ordinal),
            "the skipped tenant filter must be observable through the context's query-filter logger");
    }

    // R03 (smoke): a bound class entity materializes from a raw source and its values round-trip. This
    // process-wide smoke test cannot assert a cold cache: RawBindMaterializedEntity is registered by the
    // first provider to run, so the guard then skips registration for every later provider. The
    // authoritative cold-state proof is the isolated sqlite test
    // RawSourceBindingColdRegistrationTests.ColdClassEntity_RawSql_ShouldMaterializeNullAndNonNullValues.
    [Fact]
    public void RawSourceBinding_BoundClassEntity_ShouldMaterialize()
    {
        var ctx = RawBindContext(1);
        var b = NextRawBindBase();
        SeedRawBindRows(RawBindActive(b), RawBindDeleted(b - 1), RawBindForeign(b - 2));

        // The whole entity is materialized by the builder terminal (no identity Select: a single-source
        // `Select(x => x)` is not a supported whole-entity projection). This is the runtime smoke check:
        // the bind's registration makes typed materialization work regardless of prior From<T>() state.
        var rows = ctx.FromSql(AllRawBindColumnsSql)
            .BindEntity<RawBindMaterializedEntity>(["id", "tenant_id", "is_deleted", "name"])
            .Where(x => x.Id >= b - 2 && x.Id <= b)
            .ToList();

        rows.Should().ContainSingle(r => r.Id == b);
        var row = rows.Single(r => r.Id == b);
        row.TenantId.Should().Be(1);
        row.IsDeleted.Should().BeFalse();
        row.Name.Should().Be("raw-bind-active");
        DataContextCache.Metadata.ContainsKey(typeof(RawBindMaterializedEntity)).Should().BeTrue(
            "a bound class entity has a registered mapping (coldness is proven by the isolated sqlite test)");
    }

    private sealed class RawBindLogSink : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(RawBindLogSink owner) : ILogger
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

/// <summary>
/// A filter-free map over the shared <c>query_filter_entity</c> table, used as the unfiltered main
/// side of the joined bound-source integration test.
/// </summary>
[SqlTable("query_filter_entity")]
public sealed class RawBindJoinMainEntity
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

/// <summary>
/// A class entity unique to <c>RawSourceBinding_BoundClassEntity_ShouldMaterialize</c>. Its mapping is
/// registered by the first provider that binds it; later providers reuse it (a registration no-op), so
/// this smoke test does not assert a cold <c>Metadata</c> (#185 D185, R03).
/// </summary>
[SqlTable("query_filter_entity")]
public sealed class RawBindMaterializedEntity
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
