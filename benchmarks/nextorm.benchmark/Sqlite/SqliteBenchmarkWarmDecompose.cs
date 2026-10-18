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
    private readonly QueryCommand<int> _reusedExcept;
    private readonly QueryCommand<int> _reusedIntersect;

    // T244 (#244) D244-01 diagnostics. The raw-recipe store is keyed by the concrete context type
    // (QueryPlanner passes GetType()), so the probes need exactly that type. The counters below are
    // written outside every measured region (GlobalSetup verification / GlobalCleanup reporting).
    private readonly Type _dbType;
    private long _sink;
    private int _cteRawHitVerified;
    private int _recursiveRawHitVerified;
    private string _cteRawHitNote = "not verified";
    private string _recursiveRawHitNote = "not verified";

    public SqliteBenchmarkWarmDecompose()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _dbType = _db.GetType();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Prime the plan caches so the *_Warm_PlanOnly arms see hits.
        _ = _db.GetPreparedQueryCommand(BuildCte(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildRecursiveCte(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildJoin4(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildInAtInInline(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildExcept(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(BuildIntersect(), false, true, CancellationToken.None);

        // Already-prepared commands for the *_Warm_Reused arms: the loop only pays the cache lookup.
        _reusedCte = BuildCte();
        _ = _db.GetPreparedQueryCommand(_reusedCte, false, true, CancellationToken.None);
        _reusedRecursiveCte = BuildRecursiveCte();
        _ = _db.GetPreparedQueryCommand(_reusedRecursiveCte, false, true, CancellationToken.None);
        _reusedJoin4 = BuildJoin4();
        _ = _db.GetPreparedQueryCommand(_reusedJoin4, false, true, CancellationToken.None);
        _reusedInAtInInline = BuildInAtInInline();
        _ = _db.GetPreparedQueryCommand(_reusedInAtInInline, false, true, CancellationToken.None);
        _reusedExcept = BuildExcept();
        _ = _db.GetPreparedQueryCommand(_reusedExcept, false, true, CancellationToken.None);
        _reusedIntersect = BuildIntersect();
        _ = _db.GetPreparedQueryCommand(_reusedIntersect, false, true, CancellationToken.None);
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

    // Exact fluent shape of SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except / _Intersect.
    private QueryCommand<int> BuildExcept()
        => _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id));

    private QueryCommand<int> BuildIntersect()
        => _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id));

    private static int _verified;

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 0)
        {
            Console.WriteLine("SqliteBenchmarkWarmDecompose verification passed: "
                + BuildCte().ToList().Count + "," + BuildRecursiveCte().ToList().Count + ","
                + BuildJoin4().ToList().Count + "," + BuildInAtInInline().ToList().Count
                + "," + BuildExcept().ToList().Count + "," + BuildIntersect().ToList().Count);
        }

        // BDN rejects a second un-targeted [GlobalSetup] method in the same class, so both
        // verifications are performed by this single effective setup: the D244-01 probes run
        // per instance, after the verification warm-up above has published their recipes.
        VerifyDiagnosticProbes();
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

    // =============================================================================================
    // T245 (#245) D245-1 set-operation decomposition — measurement only, no product change.
    //
    // The same stage ladder as the CTE / recursive CTE / Join4 / IN-list families above, applied to
    // the exact fluent shape SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except /
    // B_Nextorm_Cached_Intersect executes (same tables, same projection, same terminal), so the
    // warm totals are comparable with the FairCached B arms:
    //   *_Construct      = build the QueryCommand only (expression tree + clone).
    //   *_Prepare_NoHash = construct + PrepareCommand(dontCalculateHash: true) (visitors).
    //   *_Prepare_Hash   = construct + PrepareCommand(false) (visitors + all *PlanHash).
    //   *_Warm_PlanOnly  = full implicit-cache hit path on a fresh command.
    //   *_Warm_Reused    = the same already-prepared command every iteration: plan-key + lookup only
    //                      (expected 0 B per operation). Delta vs *_Warm_PlanOnly is exactly
    //                      "construct + prepare + hash".
    // =============================================================================================

    [Benchmark]
    [BenchmarkCategory("Warm_Except")]
    public QueryCommand<int> Except_Construct()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildExcept();
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Except")]
    public QueryCommand<int> Except_Prepare_NoHash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildExcept(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Except")]
    public QueryCommand<int> Except_Prepare_Hash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildExcept(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Except")]
    public IPreparedQueryCommand<int> Except_Warm_PlanOnly()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildExcept(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Except")]
    public IPreparedQueryCommand<int> Except_Warm_Reused()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedExcept, false, true, Ct);
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Intersect")]
    public QueryCommand<int> Intersect_Construct()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildIntersect();
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Intersect")]
    public QueryCommand<int> Intersect_Prepare_NoHash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildIntersect(); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Intersect")]
    public QueryCommand<int> Intersect_Prepare_Hash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildIntersect(); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Intersect")]
    public IPreparedQueryCommand<int> Intersect_Warm_PlanOnly()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildIntersect(), false, true, Ct);
        return r!;
    }

    [Benchmark]
    [BenchmarkCategory("Warm_Intersect")]
    public IPreparedQueryCommand<int> Intersect_Warm_Reused()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(_reusedIntersect, false, true, Ct);
        return r!;
    }

    // =============================================================================================
    // T244 (#244) D244-01 diagnostic phases — measurement only, no product change.
    //
    // Resolved symbols (Roslyn, before editing; internal to nextorm.core and reachable from this
    // assembly through InternalsVisibleTo("nextorm.benchmark")):
    //   QueryCommand.CaptureRawShape(QueryCommand)                             Query/QueryCommand.QueryPreparer.cs:3889
    //   QueryCommand.RawShape.Fingerprint                                      Query/QueryCommand.QueryPreparer.cs:3685
    //   QueryPlanStore.TryGetRaw(Type, RawShape, QueryCommand, out QueryPlan?)  DataContext/Cache/QueryPlanStore.cs:118
    //   QueryCommand.CaptureRawGraph() / ApplyPreparationFrom(QueryCommand)    Query/QueryCommand.QueryPreparer.cs:4382/:4363
    //   QueryCommand.GetOrCreatePlanKey(string?)                               Query/QueryCommand.Plan.cs:30
    //   QueryCommand.IsPrepared / HasEverPrepared                              Query/QueryCommand.cs:521/:96
    //   QueryPlanner.RecipeReuses / RecipeApplyMismatches                      DataContext/QueryPlanner.cs:56/:62
    //   QueryCommand<TResult>.ToListAsync()                                    Query/QueryCommand.TResult.cs:310
    //   IDataContext.GetPreparedQueryCommand(cmd, false, true, ct)              DataContext/DataContext.cs:319
    //
    // INCLUSIVE phases, deliberately nested so each isolated cost is the difference of two adjacent
    // arms (no graph is ever counted twice; every arm builds its own fresh graph per iteration):
    //   *_RawHit        = construct + CaptureRawShape + Fingerprint                    (phase 2)
    //   *_RawHit_Lookup = previous + TryGetRaw + CaptureRawGraph + ApplyPreparationFrom   (phase 3)
    //   *_PlanKey       = previous + GetOrCreatePlanKey + staged-key equivalence         (phase 4)
    //   *_Warm_EndToEnd = the unmodified production warm call: the same fluent graph and the same
    //                     ToListAsync terminal as SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Cte
    //                     (GetPreparedQueryCommand(..., storeInCache: true) + execution)  (phase 5)
    // _Warm_EndToEnd is the reference the isolated probes are validated against. The reuse probes
    // never mutate product state: every staged command is per-iteration and thrown away.
    // =============================================================================================

    [Benchmark]
    public int Cte_RawHit()
    {
        var r = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var shape = QueryCommand.CaptureRawShape(BuildCte());
            if (shape is not null)
            {
                r = shape.Fingerprint;
                _sink++;
            }
        }
        return r;
    }

    [Benchmark]
    public QueryPlan? Cte_RawHit_Lookup()
    {
        QueryPlan? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            if (TryApplyCachedRecipe(BuildCte(), _dbType, out var recipe)) r = recipe;
        }
        return r;
    }

    [Benchmark]
    public QueryPlan? Cte_PlanKey()
    {
        QueryPlan? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var command = BuildCte();
            if (TryApplyCachedRecipe(command, _dbType, out var recipe))
            {
                var stagedKey = command.GetOrCreatePlanKey(null);
                // The product validates the staged normalized key against the recipe owner's key;
                // that comparison is where the plan comparers are created on a fresh command.
                _sink += recipe!.Equals(stagedKey) ? 1 : 0;
                r = stagedKey;
            }
        }
        return r;
    }

    [Benchmark]
    public async Task Cte_Warm_EndToEnd()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await BuildCte().ToListAsync()) _sink++;
        }
    }

    [Benchmark]
    public int RecursiveCte_RawHit()
    {
        var r = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var shape = QueryCommand.CaptureRawShape(BuildRecursiveCte());
            if (shape is not null)
            {
                r = shape.Fingerprint;
                _sink++;
            }
        }
        return r;
    }

    [Benchmark]
    public QueryPlan? RecursiveCte_RawHit_Lookup()
    {
        QueryPlan? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            if (TryApplyCachedRecipe(BuildRecursiveCte(), _dbType, out var recipe)) r = recipe;
        }
        return r;
    }

    [Benchmark]
    public QueryPlan? RecursiveCte_PlanKey()
    {
        QueryPlan? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var command = BuildRecursiveCte();
            if (TryApplyCachedRecipe(command, _dbType, out var recipe))
            {
                var stagedKey = command.GetOrCreatePlanKey(null);
                _sink += recipe!.Equals(stagedKey) ? 1 : 0;
                r = stagedKey;
            }
        }
        return r;
    }

    [Benchmark]
    public async Task RecursiveCte_Warm_EndToEnd()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await BuildRecursiveCte().ToListAsync()) _sink += row;
        }
    }

    // =============================================================================================
    // T244 (#244) D244-05 targeted re-gather ladder — measurement only, no product change.
    //
    // Every rung is a genuine PREFIX of the same graph, rebuilt fresh on every iteration (no rung
    // shares state with another and nothing is cached between iterations). The signed increment
    // between two consecutive rungs is therefore the isolated cost of the single step that
    // separates them:
    //   Cte:          body -> +With -> +From -> +Join -> +Select(full)     (existing Cte_Construct   = closure target)
    //   RecursiveCte: anchor -> step -> operands -> UnionAll -> full      (existing RecursiveCte_Construct = closure target)
    //   Gen:          From("nums") -> +Where -> +Select                   (control ladder, no residual/eligibility claim)
    // The full rungs (Cte_P4_Select / RecursiveCte_P4_Declared) build exactly what BuildCte() /
    // BuildRecursiveCte() build, so the ladder closes on the original arms.
    //
    // Raw-hit ladder on one-shot fresh never-prepared commands (the only capture entry point is
    // QueryCommand.CaptureRawShape, QueryCommand.QueryPreparer.cs:3889):
    //   RawHit_P0_Capture   = construct + CaptureRawShape (no field read).
    //   RawHit_P2_Comparers = P0 + the four expression comparers the full raw lookup creates while
    //                         RawShape.Matches verifies a candidate (QueryCommand.QueryPreparer.cs:3745-3753:
    //                         Expression, Sorting, Join and From plan comparers; a fresh command
    //                         creates them once per iteration).
    //   {family}_RawHit_Lookup = the pre-existing full raw seam rung; {family}_RawHit = the
    //                         pre-existing capture+fingerprint arm and the closure target of P0.
    // NOT SEPARABLE: a distinct "RawHit_P1_Fingerprint" rung cannot exist — RawShape.Fingerprint is a
    // plain FIELD assigned inside CaptureRawShape (QueryCommand.QueryPreparer.cs:4028), so reading it
    // adds a field load and no measurable work. It is therefore skipped, not faked.
    // =============================================================================================

    [Benchmark]
    public object Cte_P0_Body()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return result!;
    }

    [Benchmark]
    public object Cte_P1_With()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            result = _db.With("recent", recent);
        }
        return result!;
    }

    [Benchmark]
    public object Cte_P2_From()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            result = _db.With("recent", recent).From("recent");
        }
        return result!;
    }

    [Benchmark]
    public object Cte_P3_Join()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            result = _db.With("recent", recent)
                .From("recent")
                .Join(_ctx.SimpleEntity, (row, s) => row["id"].AsInt == s.Id);
        }
        return result!;
    }

    [Benchmark]
    public QueryCommand<CteJoinRow> Cte_P4_Select()
    {
        QueryCommand<CteJoinRow>? result = null;
        for (var i = 0; i < Iterations; i++) result = BuildCte();
        return result!;
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> RecursiveCte_P0_Anchor()
    {
        QueryCommand<CteNumberRow>? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        return result!;
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> RecursiveCte_P1_Step()
    {
        QueryCommand<CteNumberRow>? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        return result!;
    }

    [Benchmark]
    public object RecursiveCte_P2_Operands()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            result = anchor;
            if (step is not null) _sink++;
        }
        return result!;
    }

    [Benchmark]
    public QueryCommand<CteNumberRow> RecursiveCte_P3_UnionAll()
    {
        QueryCommand<CteNumberRow>? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            result = anchor.UnionAll(step);
        }
        return result!;
    }

    [Benchmark]
    public QueryCommand<int> RecursiveCte_P4_Declared()
    {
        QueryCommand<int>? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            result = _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
        }
        return result!;
    }

    [Benchmark]
    public object Gen_P0_From()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++) result = _db.From("nums");
        return result!;
    }

    [Benchmark]
    public object Gen_P1_Where()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++) result = _db.From("nums").Where(t => t["n"].AsInt < 5);
        return result!;
    }

    [Benchmark]
    public QueryCommand<int> Gen_P2_Select()
    {
        QueryCommand<int>? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => t["n"].AsInt);
        return result!;
    }

    [Benchmark]
    public object? Cte_RawHit_P0_Capture()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++) result = QueryCommand.CaptureRawShape(BuildCte());
        return result;
    }

    [Benchmark]
    public object? RecursiveCte_RawHit_P0_Capture()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++) result = QueryCommand.CaptureRawShape(BuildRecursiveCte());
        return result;
    }

    [Benchmark]
    public object? Cte_RawHit_P2_Comparers()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var command = BuildCte();
            var shape = QueryCommand.CaptureRawShape(command);
            if (shape is not null)
            {
                if (command.GetExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetSortingExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetJoinExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetFromExpressionPlanEqualityComparer() is not null) _sink++;
                result = shape;
            }
        }
        return result;
    }

    [Benchmark]
    public object? RecursiveCte_RawHit_P2_Comparers()
    {
        object? result = null;
        for (var i = 0; i < Iterations; i++)
        {
            var command = BuildRecursiveCte();
            var shape = QueryCommand.CaptureRawShape(command);
            if (shape is not null)
            {
                if (command.GetExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetSortingExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetJoinExpressionPlanEqualityComparer() is not null) _sink++;
                if (command.GetFromExpressionPlanEqualityComparer() is not null) _sink++;
                result = shape;
            }
        }
        return result;
    }

    /// <summary>
    /// The exact raw-recipe seam the product takes for a fresh cacheable command: capture, index
    /// lookup, replay. It deliberately does not replay the product's reject-and-discard branch — the
    /// probe must measure the hit path only, and the final command is discarded after each iteration.
    /// </summary>
    private static bool TryApplyCachedRecipe(QueryCommand command, Type dbType, out QueryPlan? recipe)
    {
        recipe = null;
        if (command.IsPrepared || command.HasEverPrepared) return false;

        var shape = QueryCommand.CaptureRawShape(command);
        if (shape is null) return false;
        if (!QueryPlanStore.TryGetRaw(dbType, shape, command, out var plan)) return false;

        _ = command.CaptureRawGraph();
        command.ApplyPreparationFrom(plan!.QueryCommand);
        recipe = plan;
        return true;
    }

    /// <summary>
    /// Non-timed D244-01 probe verification. The shipped product counter
    /// <see cref="QueryPlanner.RecipeReuses"/> is the authority: it advances only when a fresh command
    /// really took the raw-recipe reuse branch (index hit AND the staged-key/owner validation passed).
    /// Per family it runs the unmodified end-to-end warm call once, then the same isolated
    /// lookup/replay/plan-key seam the new arms measure on a second fresh command. Results are
    /// reported by <see cref="ReportDiagnosticProbes"/>, so a miss is visible in the output instead of
    /// being silently measured. Invoked by <see cref="Verify"/> (single effective
    /// <c>[GlobalSetup]</c>).
    /// </summary>
    public void VerifyDiagnosticProbes()
    {
        _cteRawHitVerified = VerifyRawHit(BuildCte, out _cteRawHitNote);
        _recursiveRawHitVerified = VerifyRawHit(BuildRecursiveCte, out _recursiveRawHitNote);
    }

    private int VerifyRawHit<TResult>(Func<QueryCommand<TResult>> build, out string note)
    {
        var reusesBefore = QueryPlanner.RecipeReuses;
        var mismatchesBefore = QueryPlanner.RecipeApplyMismatches;

        // 1. The unmodified end-to-end warm call (same call the end-to-end arm and the production
        //    benchmark make): a fresh command must take the raw-recipe reuse branch exactly once.
        _ = _db.GetPreparedQueryCommand(build(), false, true, Ct);
        var reuses = QueryPlanner.RecipeReuses - reusesBefore;
        var mismatches = QueryPlanner.RecipeApplyMismatches - mismatchesBefore;

        // 2. The isolated seam on a second fresh command must resolve a recipe whose staged plan key
        //    validates against it — i.e. the probe measures the very same path the end-to-end call used.
        var probe = build();
        var probeHit = TryApplyCachedRecipe(probe, _dbType, out var recipe);
        var stagedValidated = probeHit && recipe!.Equals(probe.GetOrCreatePlanKey(null));

        note = "endToEndReuses=" + reuses
            + ", applyMismatches=" + mismatches
            + ", lookup=" + (probeHit ? "HIT" : "MISS")
            + ", stagedKey=" + (stagedValidated ? "validated" : "MISS");

        return reuses == 1 && mismatches == 0 && probeHit && stagedValidated ? 1 : 0;
    }

    [GlobalCleanup]
    public void ReportDiagnosticProbes()
    {
        Console.WriteLine(
            "SqliteBenchmarkWarmDecompose diagnostics: cteRawHit=" + _cteRawHitVerified + " [" + _cteRawHitNote + "]; "
            + "recursiveRawHit=" + _recursiveRawHitVerified + " [" + _recursiveRawHitNote + "]; "
            + "recipe publishes/reuses/rejections/mismatches=" + QueryPlanner.RecipePublishes + "/"
            + QueryPlanner.RecipeReuses + "/" + QueryPlanner.RecipeRejections + "/" + QueryPlanner.RecipeApplyMismatches
            + "; sink=" + _sink);
    }
}
