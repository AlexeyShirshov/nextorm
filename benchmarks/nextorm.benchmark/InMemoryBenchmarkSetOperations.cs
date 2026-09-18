using BenchmarkDotNet.Attributes;
using nextorm.core;

namespace nextorm.benchmark;

/// <summary>
/// Set operations (<c>UNION</c>/<c>INTERSECT</c>/<c>EXCEPT</c>) in nextorm's in-memory provider versus
/// raw LINQ and the EF Core InMemory provider.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("InMemoryNew")]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkSetOperations
{
    private const int Rows = 10_000;
    private const int Iterations = 100;

    private readonly InMemoryDataRepository _ctx;
    private readonly List<int> _ids;
    private readonly List<int> _half;
    private EFInMemoryDataContext? _efCtx;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkSetOperations()
    {
        var data = new List<SimpleEntity>(Rows);
        for (var i = 0; i < Rows; i++)
            data.Add(new SimpleEntity { Id = i });

        var provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(data);

        _ids = Enumerable.Range(0, Rows).ToList();
        _half = Enumerable.Range(0, Rows / 2).ToList();
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(Rows);

    [Benchmark(Baseline = true)]
    public void Nextorm_Except()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity.Select(e => e.Id)
                .Except(_ctx.SimpleEntity.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Except()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ids.Except(_half).Count();
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Except()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = EfContext.SimpleEntities.Select(e => e.Id)
                .Except(EfContext.SimpleEntities.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_Intersect()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity.Select(e => e.Id)
                .Intersect(_ctx.SimpleEntity.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Intersect()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ids.Intersect(_half).Count();
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Intersect()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = EfContext.SimpleEntities.Select(e => e.Id)
                .Intersect(EfContext.SimpleEntities.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_Union()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity.Select(e => e.Id)
                .Union(_ctx.SimpleEntity.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Union()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ids.Union(_half).Count();
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Union()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = EfContext.SimpleEntities.Select(e => e.Id)
                .Union(EfContext.SimpleEntities.Where(e => e.Id < Rows / 2).Select(e => e.Id))
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }
}
