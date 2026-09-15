using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using nextorm.core;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net10_0)]
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCachedPlan
{
    const int Iterations = 100;
    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;
    private readonly IPreparedQueryCommand<int> _prepared;

    public SqliteBenchmarkCachedPlan()
    {
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        var builder = new DbContextBuilder();
        builder.UseSqlite(filepath);
        _db = builder.CreateDbContext();
        _repo = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _prepared = _repo.SimpleEntity
            .Where(it => it.Id == NORM.Param<int>(0))
            .Select(it => it.Id)
            .Prepare();

        // Warm both plan caches so the measured loops only exercise the cached path.
        _ = _repo.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => it.Id).ToList(0);
        _ = _repo.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id).ToList();
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
}
