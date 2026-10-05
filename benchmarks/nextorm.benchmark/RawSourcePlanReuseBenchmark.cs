using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Focused, non-acceptance benchmark for the issue #14 seam that #124 tightened: a raw
/// <c>FromSql(sql)</c> source is now identified by <b>reference</b> in the plan key
/// (<c>FromExpressionPlanEqualityComparer</c>: <c>ReferenceEquals(x.RawSqlSource, y.RawSqlSource)</c>).
/// <list type="bullet">
/// <item><b>cold</b> — every iteration constructs a new <c>FromSql(sql)</c> source, so the plan key is
///   distinct and the preparation always misses the cache (SQL + command rebuilt, and a fresh entry
///   stored); the pre-#124 fallback compared the two null subqueries and let independent raw sources
///   share a plan;</item>
/// <item><b>warm</b> — the very same <c>FromSql(sql)</c> builder instance is reused, so the plan key is
///   stable and the preparation hits the cache (parameter re-extraction only).</item>
/// </list>
/// Two independent sources therefore <b>no longer</b> share a cached plan. The arms come in bound
/// (<c>BindEntity</c>, so the entity binding also participates in the key) and unbound flavours. The
/// <see cref="Setup"/> guard fails the run if that semantics is broken: independent raw sources must not
/// return the same cached command, and a reused source must.
/// <para>
/// SQLite is used only to render the statement; the context is a <c>:memory:</c> connection and the
/// benchmark never opens it or touches data. The cold arm purges the thread-local plan store once per
/// invocation so the entry count stays bounded (each of the 64 fresh sources still misses on its own
/// unique key). This benchmark does not change equality semantics.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("raw-plan-reuse")]
[Config(typeof(NextormConfig))]
public class RawSourcePlanReuseBenchmark
{
    private const int Iterations = 64;
    private const string Sql = "select id from raw_plan_reuse_entity";

    private readonly IDataContext _db;
    private readonly EntityBuilder<TableAlias> _unboundSource;
    private readonly EntityBuilder<RawPlanReuseEntity> _boundSource;
    private IPreparedQueryCommand<long?>? _unboundSink;
    private IPreparedQueryCommand<long>? _boundSink;

    public RawSourcePlanReuseBenchmark()
    {
        _db = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        _unboundSource = _db.FromSql(Sql);
        _boundSource = _db.FromSql(Sql).BindEntity<RawPlanReuseEntity>(["id"]);

        // Warm both reused sources so the warm arms and the guard exercise the cache-hit path.
        _ = PrepareUnbound(_unboundSource, storeInCache: true);
        _ = PrepareBound(_boundSource, storeInCache: true);
    }

    [GlobalSetup]
    public void Setup()
    {
        // (a) Two independently-constructed FromSql("same sql") sources must be distinct cache entries.
        var freshUnbound1 = PrepareUnbound(_db.FromSql(Sql), storeInCache: true);
        var freshUnbound2 = PrepareUnbound(_db.FromSql(Sql), storeInCache: true);
        if (ReferenceEquals(freshUnbound1, freshUnbound2))
            throw new InvalidOperationException("Independent unbound raw sources shared a cached plan.");

        var freshBound1 = PrepareBound(_db.FromSql(Sql).BindEntity<RawPlanReuseEntity>(["id"]), storeInCache: true);
        var freshBound2 = PrepareBound(_db.FromSql(Sql).BindEntity<RawPlanReuseEntity>(["id"]), storeInCache: true);
        if (ReferenceEquals(freshBound1, freshBound2))
            throw new InvalidOperationException("Independent bound raw sources shared a cached plan.");

        // (b) The same source instance must hit the cache.
        var reusedUnbound1 = PrepareUnbound(_unboundSource, storeInCache: true);
        var reusedUnbound2 = PrepareUnbound(_unboundSource, storeInCache: true);
        if (!ReferenceEquals(reusedUnbound1, reusedUnbound2))
            throw new InvalidOperationException("A reused unbound raw source did not hit the plan cache.");

        var reusedBound1 = PrepareBound(_boundSource, storeInCache: true);
        var reusedBound2 = PrepareBound(_boundSource, storeInCache: true);
        if (!ReferenceEquals(reusedBound1, reusedBound2))
            throw new InvalidOperationException("A reused bound raw source did not hit the plan cache.");
    }

    // Cold: a fresh raw source per iteration => new plan key => cache miss + a stored entry.
    [Benchmark]
    public IPreparedQueryCommand<long?> Unbound_Fresh_Cold()
    {
        IPreparedQueryCommand<long?>? r = null;
        _db.PurgeQueryCache();
        for (var i = 0; i < Iterations; i++)
            r = PrepareUnbound(_db.FromSql(Sql), storeInCache: true);

        return _unboundSink = r!;
    }

    // Warm: the same raw source instance => stable plan key => cache hit.
    [Benchmark(Baseline = true)]
    public IPreparedQueryCommand<long?> Unbound_Reused_Warm()
    {
        IPreparedQueryCommand<long?>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = PrepareUnbound(_unboundSource, storeInCache: true);

        return _unboundSink = r!;
    }

    // Cold, bound: binding joins the key, but the raw fragment identity still keeps fresh sources apart.
    [Benchmark]
    public IPreparedQueryCommand<long> Bound_Fresh_Cold()
    {
        IPreparedQueryCommand<long>? r = null;
        _db.PurgeQueryCache();
        for (var i = 0; i < Iterations; i++)
        {
            var source = _db.FromSql(Sql).BindEntity<RawPlanReuseEntity>(["id"]);
            r = PrepareBound(source, storeInCache: true);
        }

        return _boundSink = r!;
    }

    // Warm, bound: reusing the bound builder hits the cache.
    [Benchmark]
    public IPreparedQueryCommand<long> Bound_Reused_Warm()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = PrepareBound(_boundSource, storeInCache: true);

        return _boundSink = r!;
    }

    private IPreparedQueryCommand<long?> PrepareUnbound(EntityBuilder<TableAlias> source, bool storeInCache)
    {
        var command = source.Select(x => x.GetNullableInt64("id"));
        return _db.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);
    }

    private IPreparedQueryCommand<long> PrepareBound(EntityBuilder<RawPlanReuseEntity> source, bool storeInCache)
    {
        var command = source.Select(x => x.Id);
        return _db.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);
    }
}

[SqlTable("raw_plan_reuse_entity")]
public sealed class RawPlanReuseEntity
{
    [Column("id")]
    public long Id { get; set; }
}
