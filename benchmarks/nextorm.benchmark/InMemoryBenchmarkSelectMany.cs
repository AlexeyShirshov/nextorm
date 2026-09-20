using BenchmarkDotNet.Attributes;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// <c>SelectMany</c> (correlated flatten) and <c>GroupJoin</c> (grouped inner rows): nextorm in-memory
/// versus raw LINQ and the EF Core InMemory provider.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("InMemoryNew")]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkSelectMany
{
    private const int Rows = 10_000;
    private const int Iterations = 100;

    private readonly InMemoryDataRepository _ctx;
    private readonly IEnumerable<SimpleEntity> _data;
    private readonly QueryCommand<int> _preparedSelectMany;
    private readonly QueryCommand<int> _preparedGroupJoin;
    private EFInMemoryDataContext? _efCtx;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkSelectMany()
    {
        var data = new List<SimpleEntity>(Rows);
        for (var i = 0; i < Rows; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        var provider = new InMemoryDataContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);

        // Built once so the implicit plan cache can be observed (the per-call benchmarks rebuild the
        // query every iteration, matching the other InMemory* benchmarks).
        _preparedSelectMany = _ctx.SimpleEntity
            .SelectMany(e => Enumerable.Range(0, e.Id % 3 + 1))
            .Select(x => x);
        _preparedGroupJoin = _ctx.SimpleEntity
            .GroupJoin(_ctx.SimpleEntity, o => o.Id % 100, i => i.Id % 100, (o, g) => new { o.Id, Count = g.Count() })
            .Select(x => x.Id + x.Count);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(Rows);

    [Benchmark(Baseline = true)]
    public void Nextorm_SelectMany()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity
                .SelectMany(e => Enumerable.Range(0, e.Id % 3 + 1))
                .Select(x => x)
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_SelectMany()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
            acc += _data.SelectMany(e => Enumerable.Range(0, e.Id % 3 + 1)).Count();
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_GroupJoin()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity
                .GroupJoin(_ctx.SimpleEntity, o => o.Id % 100, i => i.Id % 100, (o, g) => new { o.Id, Count = g.Count() })
                .Select(x => x)
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_GroupJoin()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
            acc += _data.GroupJoin(_data, o => o.Id % 100, i => i.Id % 100, (o, g) => new { o.Id, Count = g.Count() }).Count();
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_GroupJoin()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var source = EfContext.SimpleEntities;
            acc += source.GroupJoin(source, o => o.Id % 100, i => i.Id % 100, (o, g) => new { o.Id, Count = g.Count() }).Count();
        }
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_SelectMany_Prepared()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
            acc += _preparedSelectMany.ToList().Count;
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_GroupJoin_Prepared()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
            acc += _preparedGroupJoin.ToList().Count;
        _sink = acc;
    }
}
