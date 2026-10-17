using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D5 task B: the diagnostic lifecycle of a skipped global query filter on a bound raw source.
/// A capturing <see cref="ILoggerProvider"/> asserts the category, level, event name and the five
/// structured fields, that the warning fires once on the cache-miss preparation and never on a cache
/// hit, that the message leaks no SQL text/table name/parameter/captured value, and that a context
/// without a logger factory does not crash.
/// </summary>
public class RawSourceBindingDiagnosticsTests
{
    private const string TenantKey = "rsb_diag_tenant";
    private const int SecretTenant = 424242;

    [SqlTable("rsb_diag_entity")]
    public sealed class DiagEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    [SqlTable("rsb_diag_join_main")]
    public sealed class DiagJoinMain
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("rsb_diag_join_plain")]
    public sealed class DiagJoinPlain
    {
        [Column("id")]
        public int Id { get; set; }
    }

    private static void Configure(IDataContext ctx)
        => ctx.From<DiagEntity>(b => b
            .HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey])
            .HasQueryFilter(e => e.Id > 0));

    private static (IDataContext Ctx, LogSink Sink) CreateWithLogger(bool enabled = true)
    {
        var sink = new LogSink { Enabled = enabled };
        var ctx = new SqliteDataContext(
            "Data Source=:memory:",
            new DataContextBuilder().UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(sink))));
        return (ctx, sink);
    }

    private static IReadOnlyList<CapturedLog> FilterWarnings(LogSink sink)
        => [.. sink.Logs.Where(static l => l.Message.StartsWith("RawSourceFilterSkipped:", StringComparison.Ordinal))];

    [Fact]
    public void SkippedFilter_ShouldEmitOneWarningWithContractFields()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var cmd = ctx.FromSql("select id from rsb_diag_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var warning = FilterWarnings(sink).Should().ContainSingle().Subject;
        warning.Category.Should().Be("NextORM.QueryFilters");
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().StartWith("RawSourceFilterSkipped: ");
        warning.Fields["EntityType"].Should().Be(nameof(DiagEntity));
        warning.Fields["SourceOrdinal"].Should().Be(0);
        warning.Fields["FilterKey"].Should().Be("tenant");
        warning.Fields["Reason"].Should().Be("MissingColumns");
        warning.Fields["MissingColumns"].Should().Be("tenant_id");
    }

    [Fact]
    public void CacheHit_ShouldNotReemitTheWarning()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var cmd = ctx.FromSql("select id from rsb_diag_cache_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);

        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle();

        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle("a plan-cache hit must not re-emit the diagnostic");
    }

    [Fact]
    public void Message_ShouldNotLeakSqlTableNamesParametersOrValues()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var cmd = ctx.FromSql("select id from rsb_diag_secret_table")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var message = FilterWarnings(sink).Should().ContainSingle().Subject.Message;
        message.Should().NotContain("rsb_diag_secret_table");
        message.Should().NotContain("select");
        message.Should().NotContain(SecretTenant.ToString());
        message.Should().NotContain("p0");
    }

    [Fact]
    public void SourceOrdinal_ShouldCountAllJoins_MainZeroAndJoinJPlusOne()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        // The first join is unbound (still ordinal 1); the second bound join is missing the tenant
        // column and is skipped at ordinal 2. The main source's own ordinal 0 is pinned by
        // SkippedFilter_ShouldEmitOneWarningWithContractFields.
        var secondJoin = ctx.FromSql("select id from rsb_diag_join_source").BindEntity<DiagEntity>(["id"]);

        var cmd = ctx.From<DiagJoinMain>()
            .Join(ctx.From<DiagJoinPlain>(), (a, b) => a.Id == b.Id)
            .Join(secondJoin, (p, b) => p.Item2.Id == b.Id)
            .Select(p => p.Item1.Id);
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var warning = FilterWarnings(sink).Should().ContainSingle().Subject;
        warning.Fields["SourceOrdinal"].Should().Be(2, "the unbound first join still occupies ordinal 1");
    }

    [Fact]
    public void DerivedBoundRaw_EmitsSkipWarning()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        // A bound raw source nested as a derived table: its skip is collected while the inner command
        // is prepared, so it must be transferred to the owning (outer) preparation.
        var inner = ctx.FromSql("select id from rsb_diag_derived_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);
        var outer = ctx.From(inner).Select(x => x);

        _ = ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);

        var warning = FilterWarnings(sink).Should().ContainSingle().Subject;
        warning.Category.Should().Be("NextORM.QueryFilters");
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Fields["EntityType"].Should().Be(nameof(DiagEntity));
        warning.Fields["SourceOrdinal"].Should().Be(0);
        warning.Fields["Reason"].Should().Be("MissingColumns");
        warning.Fields["MissingColumns"].Should().Be("tenant_id");
    }

    [Fact]
    public void CteBoundRaw_EmitsSkipWarning()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var body = ctx.FromSql("select id from rsb_diag_cte_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);
        var cmd = ctx.With("c", body).From("c").Select(t => t["id"].AsInt);

        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        var warning = FilterWarnings(sink).Should().ContainSingle().Subject;
        warning.Category.Should().Be("NextORM.QueryFilters");
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Fields["EntityType"].Should().Be(nameof(DiagEntity));
        warning.Fields["SourceOrdinal"].Should().Be(0);
        warning.Fields["Reason"].Should().Be("MissingColumns");
        warning.Fields["MissingColumns"].Should().Be("tenant_id");
    }

    [Fact]
    public void NestedBoundRaw_CacheHit_DoesNotRepeatWarning()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var inner = ctx.FromSql("select id from rsb_diag_nested_cache_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);
        var outer = ctx.From(inner).Select(x => x);

        _ = ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle("the nested skip is emitted once on the cache miss");

        _ = ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle("a plan-cache hit must not re-emit the nested diagnostic");
    }

    [Fact]
    public void Lifecycle_EachOwningMissEmitsOnce_HitsNeverReplay()
    {
        var (ctx, sink) = CreateWithLogger();
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        // Warning-producing miss A: the owning preparation emits exactly one diagnostic.
        var a1 = ctx.From("rsb_diag_life_a").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(a1, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle().Which.Fields["MissingColumns"].Should().Be("tenant_id");

        // Hit A: a fresh command with the same plan key must not re-emit.
        var a2 = ctx.From("rsb_diag_life_a").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(a2, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().ContainSingle("a plan-cache hit never re-emits");

        // Unrelated warning-producing miss B: its own preparation emits once more.
        var b1 = ctx.From("rsb_diag_life_b").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(b1, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().HaveCount(2, "each owning miss emits exactly once");

        // Hit B: again no replay.
        var b2 = ctx.From("rsb_diag_life_b").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(b2, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().HaveCount(2, "a hit on the second source never re-emits either");
    }

    [Fact]
    public void DisabledMiss_ThenEnabledHitAndUncachedCall_DoesNotReplay()
    {
        var (ctx, sink) = CreateWithLogger(enabled: false);
        using var context = ctx;
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        // Owning miss while logging is disabled: nothing is written and the pending list is consumed.
        var a1 = ctx.From("rsb_diag_life_disabled").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(a1, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().BeEmpty("logging was disabled at the owning preparation");

        sink.Enabled = true;

        // A hit must not emit retroactively once logging is enabled...
        var a2 = ctx.From("rsb_diag_life_disabled").BindEntity<DiagEntity>(["id"]).Select(x => x.Id);
        _ = ctx.GetPreparedQueryCommand(a2, false, true, CancellationToken.None);
        FilterWarnings(sink).Should().BeEmpty("a cache hit never emits, even once logging is enabled");

        // ...and the hit-collected diagnostic must not survive to a later uncached call.
        _ = ctx.GetPreparedQueryCommand(a2, false, false, CancellationToken.None);
        FilterWarnings(sink).Should().BeEmpty("a diagnostic owned by a hit preparation is discarded, not replayed");
    }

    [Fact]
    public void NoLoggerConfigured_ShouldNotCrash()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.Properties[TenantKey] = SecretTenant;
        Configure(ctx);

        var cmd = ctx.FromSql("select id from rsb_diag_nolog_source")
            .BindEntity<DiagEntity>(["id"])
            .Select(x => x.Id);

        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        act.Should().NotThrow("a context without a logger factory must tolerate skipped filters");
    }

    private sealed class LogSink : ILoggerProvider
    {
        private readonly List<CapturedLog> _logs = [];

        public bool Enabled { get; set; } = true;

        public IReadOnlyList<CapturedLog> Logs => _logs;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(LogSink owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => owner.Enabled;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                // A single-provider factory can hand the provider's logger straight to the caller, so the
                // caller's LogWarning may not consult IsEnabled; honor the toggle here as well.
                if (!owner.Enabled)
                    return;

                var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
                if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
                {
                    foreach (var pair in values)
                        fields[pair.Key] = pair.Value;
                }

                owner._logs.Add(new CapturedLog(category, logLevel, eventId, formatter(state, exception), fields));
            }
        }
    }

    private sealed record CapturedLog(
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> Fields);
}
