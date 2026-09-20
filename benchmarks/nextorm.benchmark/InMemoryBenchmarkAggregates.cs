using BenchmarkDotNet.Attributes;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// Aggregates over an in-memory source: nextorm <c>EntityBuilder.Count/Sum/Min/Max</c> versus raw LINQ
/// and the EF Core InMemory provider.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("InMemoryNew")]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkAggregates
{
    private const int Rows = 10_000;
    private const int Iterations = 100;

    private readonly InMemoryDataRepository _ctx;
    private readonly IEnumerable<SimpleEntity> _data;
    private EFInMemoryDataContext? _efCtx;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkAggregates()
    {
        var data = new List<SimpleEntity>(Rows);
        for (var i = 0; i < Rows; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        var provider = new InMemoryDataContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(Rows);

    [Benchmark(Baseline = true)]
    public void Nextorm_Count()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.Count();
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Count()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.Count();
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Count()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.Count();
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_Sum()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.Sum(e => e.Id);
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Sum()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.Sum(e => e.Id);
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Sum()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.Sum(e => e.Id);
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_MinMax()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.Min(e => e.Id) + _ctx.SimpleEntity.Max(e => e.Id);
        _sink = acc;
    }

    [Benchmark]
    public void Linq_MinMax()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.Min(e => e.Id) + _data.Max(e => e.Id);
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_MinMax()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.Min(e => e.Id) + EfContext.SimpleEntities.Max(e => e.Id);
        _sink = acc;
    }
}
