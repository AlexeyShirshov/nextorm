using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Benchmark;

/// <summary>
/// Focused, non-acceptance benchmark for D216 (#216): the cached vs prepared cost of the source-level
/// row-lock path. It renders SQL against a PostgreSQL placeholder connection (no connection is opened),
/// so it isolates command construction + preparation/plan lookup for:
/// <list type="bullet">
/// <item>a plain query (no lock) as the untouched baseline;</item>
/// <item>a whole-query <c>ForUpdate</c> over a physical JOIN;</item>
/// <item>a source-level <c>JoinOptions.ForUpdate</c> over the same JOIN.</item>
/// </list>
/// Each shape has a <c>prepared</c> arm (<c>storeInCache:false</c>, SQL regenerated) and a <c>cached</c>
/// arm (<c>storeInCache:true</c>, plan lookup); the cached/prepared ratio is the cached-path overhead.
/// It deliberately carries the <c>T216</c> category only, so it can never perturb the acceptance gate.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("T216")]
[Config(typeof(NextormConfig))]
public class T216LockingBenchmarks
{
    private const int Iterations = 100;
    private const string PostgresPlaceholder =
        "Host=localhost;Database=nextorm;Username=nextorm;Password=nextorm";

    private readonly IDataContext _pg;

    // Consumed to keep the JIT from eliminating the work.
    private int _sink;

    public T216LockingBenchmarks()
    {
        _pg = new PostgresDataContext(PostgresPlaceholder, new DataContextBuilder());
    }

    private int Touch<T>(QueryCommand<T> cmd, bool storeInCache)
        => _pg.GetPreparedQueryCommand(cmd, false, storeInCache, CancellationToken.None).GetHashCode();

    [Benchmark]
    public int NoLock_Prepared()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(_pg.From<DmlHintTarget>().Where(x => x.Id > 5).Select(x => x.Id), false);

        return _sink = sink;
    }

    [Benchmark]
    public int NoLock_Cached()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(_pg.From<DmlHintTarget>().Where(x => x.Id > 5).Select(x => x.Id), true);

        return _sink = sink;
    }

    [Benchmark]
    public int WholeLockJoin_Prepared()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(
                _pg.From<DmlHintTarget>()
                    .Join(_pg.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                    .ForUpdate()
                    .Select(p => new { p.Item1.Id }),
                false);

        return _sink = sink;
    }

    [Benchmark]
    public int WholeLockJoin_Cached()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(
                _pg.From<DmlHintTarget>()
                    .Join(_pg.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                    .ForUpdate()
                    .Select(p => new { p.Item1.Id }),
                true);

        return _sink = sink;
    }

    [Benchmark]
    public int SourceLockJoin_Prepared()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(
                _pg.From<DmlHintTarget>()
                    .Join(_pg.From<DmlHintJoin>(), (a, b) => a.Id == b.Id, j => j.ForUpdate())
                    .Select(p => new { p.Item1.Id }),
                false);

        return _sink = sink;
    }

    [Benchmark]
    public int SourceLockJoin_Cached()
    {
        var sink = 0;
        for (var i = 0; i < Iterations; i++)
            sink = Touch(
                _pg.From<DmlHintTarget>()
                    .Join(_pg.From<DmlHintJoin>(), (a, b) => a.Id == b.Id, j => j.ForUpdate())
                    .Select(p => new { p.Item1.Id }),
                true);

        return _sink = sink;
    }
}
