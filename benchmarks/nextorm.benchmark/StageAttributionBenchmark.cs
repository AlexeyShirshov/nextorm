using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Iteration 15 / #183 stage-attribution harness: the fresh-fluent implicit plan-cache hit path
/// (construct + prepare + key + structural lookup + ExtractParams), with no database execution.
/// One arm performs <see cref="N"/> cached-hit invocations in a tight loop and returns a consumed
/// checksum so the loop cannot be dead-code-eliminated. The slope of the measured time against
/// <see cref="N"/> is the per-hit cached-path cost; the harness is deliberately restricted to the
/// public API so the exact same file compiles against both the old and the new library.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class StageAttributionBenchmark
{
    [Params(1, 16, 256, 4096)]
    public int N;

    private IDataContext _db = null!;
    private TestDataRepository _repo = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _repo = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Warm the implicit plan cache for the captured-value query shape so every measured
        // invocation is a cache hit with parameter refresh (PlanOnly, no DB execution).
        for (var i = 0; i < 3; i++)
            _ = _db.GetPreparedQueryCommand(Build(i), false, true, CancellationToken.None);
    }

    private QueryCommand<int> Build(int value)
        => _repo.SimpleEntity.Where(it => it.Id == value).Select(it => it.Id);

    [Benchmark]
    public int CachedHit_PlanOnly_Param()
    {
        var sum = 0;
        for (var i = 0; i < N; i++)
        {
            var r = _db.GetPreparedQueryCommand(Build(i), false, true, CancellationToken.None);
            sum += r.GetHashCode();
        }
        return sum;
    }
}
