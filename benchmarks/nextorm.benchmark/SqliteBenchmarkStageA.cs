using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;
using IDataContext = NextORM.Core.IDataContext;

namespace NextORM.Benchmark;

/// <summary>
/// Iteration 15 / #183 Stage A: a homogeneous decomposition of the fresh-fluent implicit-plan-cache
/// path into its stages, for two workloads (a simple single-table <c>Where</c> and the two-table
/// <c>Join</c>). Every arm rebuilds the fluent command from scratch and stops at a different stage,
/// so a stage cost is the difference of two adjacent arms:
/// <list type="bullet">
/// <item><c>Construct</c> - build the <see cref="QueryCommand{TResult}"/> only (expression tree + clone).</item>
/// <item><c>Prepare_NoHash</c> - construct + <c>PrepareCommand(dontCalculateHash: true)</c> (visitors).</item>
/// <item><c>Prepare_Hash</c> - construct + <c>PrepareCommand(false)</c> (visitors + all <c>*PlanHash</c>).</item>
/// <item><c>CachedHit_PlanOnly</c> - full hit path: construct + prepare + key + structural lookup + ExtractParams (no DB).</item>
/// <item><c>CachedHit_ToList</c> - the same, plus SQLite execution (normalized-equivalent SQL/types/rows to the prepared control).</item>
/// <item><c>Prepared_ToList</c> - prepared control: no plan lookup, no parameter re-extraction.</item>
/// </list>
/// The fresh captures (<c>i</c>) become computed parameters that must be re-extracted on every cached
/// execution (see <c>QueryPlanner.ExtractParams</c>); the diagnostic batches in
/// <see cref="StageADiagnostics"/> prove the cache hits and the parameter refresh without contaminating
/// these timed operations.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[BenchmarkCategory("stage-a")]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkStageA
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<int> _wherePrepared;
    private readonly IPreparedQueryCommand<LargeEntity> _joinPrepared;

    public SqliteBenchmarkStageA()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Prepared controls: same SQL shape/types/rows as the param arms, built once outside the
        // measurement. Placeholders differ by design: the prepared control uses the runtime parameter
        // `SqlFunctions.Parameter<int>(0)` ($norm_p0), while the captured arms render the captured local
        // as $value; the arm comparison is normalized equivalence, not byte-identical SQL text.
        _wherePrepared = _ctx.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Id)
            .Prepare();

        _joinPrepared = _ctx.LargeEntity
            .Join(_ctx.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Where(p => p.Item2.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str })
            .Prepare();

        // Prime both implicit plan caches for several captured values so every CachedHit_* arm runs
        // on the hit path (the first value would otherwise be the single cold miss).
        for (var i = 0; i < 3; i++)
        {
            _ = _db.GetPreparedQueryCommand(BuildWhere(i), false, true, CancellationToken.None);
            _ = _db.GetPreparedQueryCommand(BuildJoin(i), false, true, CancellationToken.None);
        }
    }

    private static CancellationToken Ct => CancellationToken.None;

    private QueryCommand<int> BuildWhere(int value)
        => _ctx.SimpleEntity.Where(it => it.Id == value).Select(it => it.Id);

    private QueryCommand<LargeEntity> BuildJoin(int value)
        => _ctx.LargeEntity
            .Join(_ctx.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Where(p => p.Item2.Id == value)
            .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str });

    // ---- simple Where workload -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    public QueryCommand<int> Where_Construct()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildWhere(i);
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> Where_Prepare_NoHash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildWhere(i); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<int> Where_Prepare_Hash()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildWhere(i); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Where_CachedHit_PlanOnly()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildWhere(i), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public int Where_CachedHit_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in BuildWhere(i).ToList())
                sum += row;
        }
        return sum;
    }

    [Benchmark]
    public int Where_Prepared_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _wherePrepared.ToList(_db, i))
                sum += row;
        }
        return sum;
    }

    // ---- two-table Join workload ---------------------------------------------------------------

    [Benchmark]
    public QueryCommand<LargeEntity> Join_Construct()
    {
        QueryCommand<LargeEntity>? r = null;
        for (var i = 0; i < Iterations; i++) r = BuildJoin(i);
        return r!;
    }

    [Benchmark]
    public QueryCommand<LargeEntity> Join_Prepare_NoHash()
    {
        QueryCommand<LargeEntity>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildJoin(i); r.PrepareCommand(true, Ct); }
        return r!;
    }

    [Benchmark]
    public QueryCommand<LargeEntity> Join_Prepare_Hash()
    {
        QueryCommand<LargeEntity>? r = null;
        for (var i = 0; i < Iterations; i++) { r = BuildJoin(i); r.PrepareCommand(false, Ct); }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<LargeEntity> Join_CachedHit_PlanOnly()
    {
        IPreparedQueryCommand<LargeEntity>? r = null;
        for (var i = 0; i < Iterations; i++) r = _db.GetPreparedQueryCommand(BuildJoin(i), false, true, Ct);
        return r!;
    }

    [Benchmark]
    public long Join_CachedHit_ToList()
    {
        long sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in BuildJoin(i).ToList())
                sum += row.Id;
        }
        return sum;
    }

    [Benchmark]
    public long Join_Prepared_ToList()
    {
        long sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _joinPrepared.ToList(_db, i))
                sum += row.Id;
        }
        return sum;
    }
}
