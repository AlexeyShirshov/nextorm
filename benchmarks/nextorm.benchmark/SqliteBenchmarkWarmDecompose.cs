using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Decomposes the warm (non-prepared) path for the four features that are still behind Dapper
/// (CTE / recursive CTE / Join4 / IN-list). Every arm rebuilds the fluent command from scratch and
/// stops at a different stage, so the per-stage cost is a difference of two arms:
/// <list type="bullet">
/// <item><c>*_Construct</c> - build the <see cref="QueryCommand"/> only (expression tree + clone).</item>
/// <item><c>*_Prepare_NoHash</c> - construct + <c>PrepareCommand(dontCalculateHash: true)</c> (visitors).</item>
/// <item><c>*_Prepare_Hash</c> - construct + <c>PrepareCommand(false)</c> (visitors + all <c>*PlanHash</c>).</item>
/// <item><c>*_Warm_PlanOnly</c> - full implicit-cache hit path: construct + prepare + hash + lookup + ExtractParams.</item>
/// </list>
/// The SQL build + <c>CreateCommand</c> cost (only on a miss) is measured separately by
/// <see cref="SqliteBenchmarkFeaturePlanBuild"/>.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkWarmDecompose
{
    private const int Iterations = 100;

    private static readonly long[] CapturedInValues = { 1, 3, 10 };

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly QueryCommand<CteJoinRow> _reusedCte;
    private readonly QueryCommand<int> _reusedRecursiveCte;
    private readonly QueryCommand<FourJoinRow> _reusedJoin4;
    private readonly QueryCommand<long> _reusedInAtInInline;

    public SqliteBenchmarkWarmDecompose()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Prime the plan caches so the *_Warm_PlanOnly arms see hits.
        _ = _db.GetPreparedQueryCommand(BuildCte(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildRecursiveCte(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildJoin4(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildInAtInInline(), false, true, CancellationToken.None);

        // Already-prepared commands for the *_Warm_Reused arms: the loop only pays the cache lookup.
        _reusedCte = BuildCte();
        _ = _db.GetPreparedQueryCommand(_reusedCte, false, true, CancellationToken.None);
        _reusedRecursiveCte = BuildRecursiveCte();
        _ = _db.GetPreparedQueryCommand(_reusedRecursiveCte, false, true, CancellationToken.None);
        _reusedJoin4 = BuildJoin4();
        _ = _db.GetPreparedQueryCommand(_reusedJoin4, false, true, CancellationToken.None);
        _reusedInAtInInline = BuildInAtInInline();
        _ = _db.GetPreparedQueryCommand(_reusedInAtInInline, false, true, CancellationToken.None);
    }

    private QueryCommand<CteJoinRow> BuildCte()
    {
        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return _db.With("recent", recent)
            .From("recent")
            .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id });
    }

    private QueryCommand<int> BuildRecursiveCte()
    {
        var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        return _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
    }

    private QueryCommand<FourJoinRow> BuildJoin4()
    {
        return _ctx.SimpleEntity
            .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Join(_ctx.SimpleEntity, (p, s) => p.Item2.Id == (long)s.Id)
            .Join(_ctx.ComplexEntity, (p, c) => (long)p.Item3.Id == c.Id)
            .Select(p => new FourJoinRow { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString });
    }

    private QueryCommand<long> BuildInAtInInline()
        => _ctx.ComplexEntity.Where(c => SqlFunctions.Sql.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id);

    private QueryCommand<long> BuildInAtInCaptured()
        => _ctx.ComplexEntity.Where(c => SqlFunctions.Sql.@in(c.Id, CapturedInValues)).Select(c => c.Id);

    private static int _verified;

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 1) return;

        Console.WriteLine("SqliteBenchmarkWarmDecompose verification passed: "
            + BuildCte().ToList().Count + "," + BuildRecursiveCte().ToList().Count + ","
            + BuildJoin4().ToList().Count + "," + BuildInAtInInline().ToList().Count);
    }

    private static CancellationToken Ct => CancellationToken.None;

    [Benchmark(Baseline = true)]
    public QueryCommand<CteJoinRow> Cte_Construct()
    {
        QueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildCte();
        return r!;
    }

    [Benchmark]
    public QueryCommand<CteJoinRow> Cte_Prepare_NoHash()
    {
        QueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildCte(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<CteJoinRow> Cte_Prepare_Hash()
    {
        QueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildCte(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<CteJoinRow> Cte_Warm_PlanOnly()
    {
        IPreparedQueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildCte(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> RecursiveCte_Construct()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildRecursiveCte();
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> RecursiveCte_Prepare_NoHash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildRecursiveCte(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> RecursiveCte_Prepare_Hash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildRecursiveCte(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> RecursiveCte_Warm_PlanOnly()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildRecursiveCte(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public QueryCommand<FourJoinRow> Join4_Construct()
    {
        QueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildJoin4();
        return r!;
    }

    [Benchmark]
    public QueryCommand<FourJoinRow> Join4_Prepare_NoHash()
    {
        QueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildJoin4(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<FourJoinRow> Join4_Prepare_Hash()
    {
        QueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildJoin4(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<FourJoinRow> Join4_Warm_PlanOnly()
    {
        IPreparedQueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildJoin4(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Inline_Construct()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildInAtInInline();
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Inline_Prepare_NoHash()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildInAtInInline(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Inline_Prepare_Hash()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildInAtInInline(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> InAtIn_Inline_Warm_PlanOnly()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildInAtInInline(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Captured_Construct()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildInAtInCaptured();
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Captured_Prepare_NoHash()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildInAtInCaptured(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<long> InAtIn_Captured_Prepare_Hash()
    {
        QueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildInAtInCaptured(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    // Reuse an already-prepared command: no construction, no prepare, no hashing. The only work left
    // is the plan-key construction + dictionary lookup (+ ExtractParams). Delta vs *_Warm_PlanOnly
    // is exactly "construct + prepare + hash".
    [Benchmark]
    public IPreparedQueryCommand<CteJoinRow> Cte_Warm_Reused()
    {
        IPreparedQueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedCte, false, true, Ct);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> RecursiveCte_Warm_Reused()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedRecursiveCte, false, true, Ct);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<FourJoinRow> Join4_Warm_Reused()
    {
        IPreparedQueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedJoin4, false, true, Ct);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> InAtIn_Inline_Warm_Reused()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedInAtInInline, false, true, Ct);
        return r!;
    }
}
