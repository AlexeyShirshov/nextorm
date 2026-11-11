using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T333 revision r=2 — the two CANDIDATE arms of the rv2 campaign (worktree Release build only).
///
/// The matching preserved arms live only in the preserved pre-change build
/// (<c>.pdca/T333/rv1/benchmark/baseline/src-tree</c>): the rv2 protocol pairs one arm from each binary, so
/// no candidate method may stand in for a preserved one.
///
/// Each benchmark method executes its prepared query <see cref="Iterations"/> times inside a single BDN
/// invocation and declares <c>OperationsPerInvoke = Iterations</c>, so BDN's reported value is already the
/// per-query figure used by AC05 — do not divide again.
///
/// Arms:
/// <list type="bullet">
/// <item><see cref="ConnectionLarge"/> — <c>Prepare()</c> (buffered, <see cref="PreparedCommandCache.Connection"/>
/// policy) over the established 10 000-row <c>large_table</c> workload, <c>ToList</c> per query: the AC05-default
/// numerator.</item>
/// <item><see cref="NoneBenchC"/> — <c>Prepare(new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true })</c>
/// over the issue's bench-c workload (<c>simple_entity</c>, 10 rows, <c>Select(it =&gt; it.Id)</c>, parameter
/// sequence 0…99, 100 queries per invocation): the AC05-none numerator. Every query goes through the real
/// production <c>None</c> execution/lease path — a fresh command is created, executed and disposed per query —
/// and the rows are counted while streaming instead of being materialised into a list, which is the closest
/// production equivalent of the historical bench-c <c>C_CommandPerInvoke</c> consumption shape
/// (<c>ExecuteReader</c> + row loop). Deviation from that historical shape and its directional bias are
/// recorded in <c>.pdca/T333/rv2/probe.json</c>.</item>
/// </list>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T333R2Benchmarks
{
    private const int Iterations = 100;

    private IDataContext _db = null!;
    private IPreparedQueryCommand<long> _connectionLarge = null!;
    private IPreparedQueryCommand<int> _noneBenchC = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();

        var repo = new TestDataRepository(_db);

        // Open the connection off the measured path: the query itself must not pay connection setup.
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _connectionLarge = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare();

        _noneBenchC = repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Id)
            .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true });

        // Warm both prepared commands (plan cache / parameter binding) so the measured loops only exercise
        // the steady-state path; warming the None arm also mints and disposes its first command.
        _ = _connectionLarge.ToList(_db, 0L);
        foreach (var _ in _noneBenchC.ToEnumerable(_db, 0))
        {
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public int ConnectionLarge()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _connectionLarge.ToList(_db, (long)i).Count;
        return n;
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public int NoneBenchC()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
        {
            // None: this execution mints its own command and the enumerator disposes that command when the
            // iteration ends, so the next query starts from a fresh one. Rows are counted, not materialised.
            foreach (var _ in _noneBenchC.ToEnumerable(_db, i))
                n++;
        }
        return n;
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
}
