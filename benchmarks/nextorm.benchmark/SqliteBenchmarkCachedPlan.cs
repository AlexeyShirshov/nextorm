using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using nextorm.core;

namespace nextorm.benchmark;

[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCachedPlan
{
    const int Iterations = 100;
    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;
    private readonly IPreparedQueryCommand<int> _prepared;
    private readonly QueryCommand<int> _rePrepareCmd;

    public SqliteBenchmarkCachedPlan()
    {
        var filepath = BenchDb.FilePath;
        var builder = new DbContextBuilder();
        builder.UseSqlite(filepath);
        _db = builder.CreateDbContext();
        _repo = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _prepared = _repo.SimpleEntity
            .Where(it => it.Id == NORM.Param<int>(0))
            .Select(it => it.Id)
            .Prepare();

        _rePrepareCmd = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);

        // Warm both plan caches so the measured loops only exercise the cached path.
        _ = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id).ToList(0);
        _ = _repo.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id).ToList();
        _ = _db.GetPreparedQueryCommand(_rePrepareCmd, false, true, CancellationToken.None);
    }

    // DB-bound baseline: prepared command, no plan lookup and no parameter re-extraction.
    [Benchmark(Baseline = true)]
    public int Prepared_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _prepared.ToList(_db, i))
                sum += row;
        }
        return sum;
    }

    // DB-bound cached query: identical SQL execution, but pays plan lookup + ExtractParams each run.
    // Delta vs Prepared_ToList is the per-execution cached-query overhead (DB cost cancels out).
    [Benchmark]
    public int Cached_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id).ToList(i))
                sum += row;
        }
        return sum;
    }

    // No DB: isolates plan-cache lookup + parameter extraction (ExtractParams).
    [Benchmark]
    public IPreparedQueryCommand<int> Cached_PlanOnly_Param()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);
            r = _db.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        }
        return r!;
    }

    // No DB: plan-cache lookup only (constant condition => NoParams, so ExtractParams is skipped).
    // Delta vs Cached_PlanOnly_Param isolates the cost of ExtractParams.
    [Benchmark]
    public IPreparedQueryCommand<int> Cached_PlanOnly_NoParam()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id);
            r = _db.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        }
        return r!;
    }

    // No plan cache (storeInCache:false) => SQL is regenerated every iteration, exercising
    // visitor + alias resolution. Isolates the cost of SQL generation (including #2).
    [Benchmark]
    public IPreparedQueryCommand<int> Build_Sql()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);
            r = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        }
        return r!;
    }

    // Same as Build_Sql but with a join, so alias resolution (DefaultAliasProvider) is exercised.
    [Benchmark]
    public IPreparedQueryCommand<LargeEntity> Build_Sql_Join()
    {
        IPreparedQueryCommand<LargeEntity>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.LargeEntity
                .Join(_repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
                .Where(p => p.t2.Id == NORM.Param<int>(0))
                .Select(p => new LargeEntity { Id = p.t1.Id, Dt = p.t1.Dt, Str = p.t1.Str });
            r = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        }
        return r!;
    }

    // No DB: only constructs the QueryCommand (EntityBuilder.Clone + Where + Select), no prepare/lookup.
    // Use it to split the cached-path overhead into construction vs preparation.
    [Benchmark]
    public QueryCommand<int> Construct_Only()
    {
        QueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);
        return r!;
    }

    // No DB: forces PrepareCommand + plan lookup on every iteration (ResetPreparation), reusing one
    // already-built QueryCommand. This is the ceiling that #5 could remove (prepare + lookup),
    // with query construction excluded.
    [Benchmark]
    public IPreparedQueryCommand<int> RePrepare_PlanOnly_Param()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            _rePrepareCmd.ResetPreparation();
            r = _db.GetPreparedQueryCommand(_rePrepareCmd, false, true, CancellationToken.None);
        }
        return r!;
    }

    // M12: same shape as Cached_PlanOnly_Param, but with the plan cache disabled on the command.
    // Cache = false makes PrepareCommand skip every '*PlanHash' computation (QueryCommand.cs: `!_dontCache`)
    // and skips the plan lookup, so this arm is [construct + prepare-without-hash + cache-miss work].
    // Delta vs Cached_PlanOnly_Param = (hashing + lookup) - (SQL build + CreateCommand + params).
    [Benchmark]
    public IPreparedQueryCommand<int> M12_NoCache_PlanOnly_Param()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);
            cmd.Cache = false;
            r = _db.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        }
        return r!;
    }

    // M12: DB-bound end-to-end with the plan cache disabled for the command (the "no cache" product path).
    [Benchmark]
    public int M12_NoCache_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id);
            cmd.Cache = false;
            foreach (var row in cmd.ToList(i))
                sum += row;
        }
        return sum;
    }
}
