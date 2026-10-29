using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T333 (#333) prepared-command per-invoke benchmark: one prepared query executed
/// <see cref="Iterations"/> times per invocation under each command-ownership policy.
///
/// Each benchmark method executes the prepared query <see cref="Iterations"/> times inside a single
/// BDN invocation and declares <c>OperationsPerInvoke = Iterations</c>, so BDN's reported value is
/// already the per-query figure used by AC05 (its <c>ns/op</c> line divides by OperationsPerInvoke;
/// do not divide again). Issue #333 quotes 10.88 μs/query for the legacy warm arm and 17.76 μs/query
/// for the arm that mints a command on every call.
///
/// Workload: one bind parameter over <c>large_table</c> (10 000 rows in the checked-in
/// <c>data/test.db</c> resolved by <see cref="BenchDb"/>), a point lookup on the rowid primary key
/// projecting a single column. Execution is therefore a real seek plus row materialisation rather
/// than a one-row plan, and the per-call client overhead is measured on top of genuine DB work.
/// Every arm uses the same SQL, the same bind parameter and the same row consumption; only the
/// command-ownership policy differs, so the comparison against the preserved T333 baseline is direct.
///
/// Arms:
/// <list type="bullet">
/// <item><see cref="LegacyWarm"/> - <c>Prepare()</c> (buffered, <see cref="PreparedCommandCache.Connection"/>
/// policy), the pre-change warm path. After T333 the single underlying command is no longer a field of
/// the prepared object: it lives in a per-connection bag, so this arm is the per-connection-reuse
/// equivalent of the legacy warm path (same connection throughout, therefore the same command reused).</item>
/// <item><see cref="LegacyWarmStreaming"/> - <c>Prepare(new PrepareOptions { Streaming = true })</c>
/// (streaming, <see cref="PreparedCommandCache.Connection"/> policy), fully drained and its reader
/// disposed after each execution: the legacy streaming reference arm.</item>
/// <item><see cref="NoneBuffered"/> - <c>Prepare(new PrepareOptions { Cache = PreparedCommandCache.None })</c>
/// (buffered, fresh command created and disposed per execution): the AC05 <c>None</c> arm.</item>
/// <item><see cref="NoneStreaming"/> - buffered policy overridden to
/// <c>PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true }</c>, fully enumerated and
/// disposed per execution, so the command and the reader are created and released once per query.</item>
/// </list>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class PreparedCommandPerInvokeBenchmark
{
    private const int Iterations = 100;

    private IDataContext _db = null!;
    private IPreparedQueryCommand<long> _preparedBuffered = null!;
    private IPreparedQueryCommand<long> _preparedStreaming = null!;
    private IPreparedQueryCommand<long> _preparedNoneBuffered = null!;
    private IPreparedQueryCommand<long> _preparedNoneStreaming = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();

        var repo = new TestDataRepository(_db);

        // Open the connection off the measured path: the query itself must not pay connection setup.
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _preparedBuffered = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare();

        _preparedStreaming = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare(new PrepareOptions { Streaming = true });

        _preparedNoneBuffered = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None });

        _preparedNoneStreaming = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true });

        // Warm both prepared shapes (plan cache / first reader) so the measured loops only exercise
        // the steady-state path; the None arms warm the same way, one command per call.
        _ = _preparedBuffered.ToList(_db, 0L);
        foreach (var _ in _preparedStreaming.ToEnumerable(_db, 0L))
        {
        }
        _ = _preparedNoneBuffered.ToList(_db, 0L);
        foreach (var _ in _preparedNoneStreaming.ToEnumerable(_db, 0L))
        {
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Iterations)]
    public int LegacyWarm()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _preparedBuffered.ToList(_db, (long)i).Count;
        return n;
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public int LegacyWarmStreaming()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
        {
            // ToEnumerable drains the prepared query through the command's streaming enumerator and
            // disposes the reader when the iteration ends; the prepared command itself is not owned
            // by the enumerator on this path (no streaming columns), so it survives to the next call.
            foreach (var _ in _preparedStreaming.ToEnumerable(_db, (long)i))
                n++;
        }
        return n;
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public int NoneBuffered()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _preparedNoneBuffered.ToList(_db, (long)i).Count;
        return n;
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public int NoneStreaming()
    {
        var n = 0;
        for (var i = 0; i < Iterations; i++)
        {
            // None + streaming: the enumerator owns the command it was handed, so the foreach disposes
            // both the reader and that command when the iteration ends, and the next call mints a new one.
            foreach (var _ in _preparedNoneStreaming.ToEnumerable(_db, (long)i))
                n++;
        }
        return n;
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
}
