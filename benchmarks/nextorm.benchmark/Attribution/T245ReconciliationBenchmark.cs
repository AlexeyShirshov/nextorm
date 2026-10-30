using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) Step 1 evidence completion — <b>measurement-only</b> side-by-side BDN class. It exists
/// only to run, in <b>one</b> BenchmarkDotNet process and against the same pinned database, the exact
/// bodies whose in-process counter values disagree with the acceptance arms, for <b>both</b> set
/// operations (<c>Except</c> and <c>Intersect</c> — the <c>_Intersect</c> arms are the counterparts of
/// the original five <c>Except</c> arms and exist so the attribution can be judged per operation):
/// <list type="bullet">
/// <item><c>Construct_LadderBody</c> — the body of
/// <see cref="SqliteBenchmarkWarmDecompose.Except_Construct"/> (plain method, no ternary).</item>
/// <item><c>Construct_ProbeBody</c> — the body of
/// <see cref="T245ReconciliationProbe"/>'s <c>construct_ladder_100loop</c> (ternary +
/// <see cref="GC.KeepAlive(object)"/>).</item>
/// <item><c>Warm_PlanOnly_LadderBody</c> — the body of
/// <see cref="SqliteBenchmarkWarmDecompose.Except_Warm_PlanOnly"/>.</item>
/// <item><c>Prepared_Reuse</c> — the body of
/// <see cref="SqliteBenchmarkFeaturesFair.A_Nextorm_Prepared_Except"/>.</item>
/// <item><c>Full_Terminal</c> — the body of
/// <see cref="SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except"/>.</item>
/// </list>
/// The <c>_Intersect</c> twins are the same five bodies against
/// <see cref="SqliteBenchmarkWarmDecompose.Intersect_Construct"/> /
/// <see cref="SqliteBenchmarkWarmDecompose.Intersect_Warm_PlanOnly"/> /
/// <see cref="SqliteBenchmarkFeaturesFair.A_Nextorm_Prepared_Intersect"/> /
/// <see cref="SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Intersect"/>.
/// It changes no existing arm, no recipe and no threshold; it is never part of an acceptance filter run.
/// </summary>
[HideColumns(BenchmarkDotNet.Columns.Column.Job, BenchmarkDotNet.Columns.Column.RatioSD, BenchmarkDotNet.Columns.Column.Error, BenchmarkDotNet.Columns.Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T245ReconciliationBenchmark
{
    private const int Iterations = 100;
    private const int FairIterations = 10;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<int> _preparedExcept;
    private readonly IPreparedQueryCommand<int> _preparedIntersect;

    private long _sink;

    public T245ReconciliationBenchmark()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _ = _db.GetPreparedQueryCommand(BuildContextExcept(_ctx), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildContextIntersect(_ctx), false, true, CancellationToken.None);
        _preparedExcept = BuildExcept(_ctx).Prepare();
        _preparedIntersect = BuildIntersect(_ctx).Prepare();
    }

    private static QueryCommand<int> BuildExcept(TestDataRepository ctx)
        => ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id));

    private static QueryCommand<int> BuildIntersect(TestDataRepository ctx)
        => ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));

    private static QueryCommand<int> BuildContextExcept(TestDataRepository ctx)
    {
        var cmd = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id));
        GC.KeepAlive(cmd);
        return cmd;
    }

    private static QueryCommand<int> BuildContextIntersect(TestDataRepository ctx)
    {
        var cmd = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
        GC.KeepAlive(cmd);
        return cmd;
    }

    [Benchmark]
    public QueryCommand<int> Construct_LadderBody()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildExcept(_ctx);
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> Construct_LadderBody_Intersect()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildIntersect(_ctx);
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> Construct_ProbeBody()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = BuildContextExcept(_ctx);
            GC.KeepAlive(cmd);
            r = cmd;
        }

        return r!;
    }

    [Benchmark]
    public QueryCommand<int> Construct_ProbeBody_Intersect()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = BuildContextIntersect(_ctx);
            GC.KeepAlive(cmd);
            r = cmd;
        }

        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Warm_PlanOnly_LadderBody()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildExcept(_ctx), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Warm_PlanOnly_LadderBody_Intersect()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildIntersect(_ctx), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public async Task Prepared_Reuse()
    {
        for (var i = 0; i < FairIterations; i++)
            foreach (var row in await _preparedExcept.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    public async Task Prepared_Reuse_Intersect()
    {
        for (var i = 0; i < FairIterations; i++)
            foreach (var row in await _preparedIntersect.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    public async Task Full_Terminal()
    {
        for (var i = 0; i < FairIterations; i++)
            foreach (var row in await _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync()) _sink++;
    }

    [Benchmark]
    public async Task Full_Terminal_Intersect()
    {
        for (var i = 0; i < FairIterations; i++)
            foreach (var row in await _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync()) _sink++;
    }
}
