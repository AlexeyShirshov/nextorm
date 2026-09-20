using BenchmarkDotNet.Attributes;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// Terminal materializers (<c>ToArray</c>, <c>ToDictionary</c>) and ordered <c>Last</c> added to the
/// nextorm in-memory provider, compared with raw LINQ and EF Core InMemory.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("InMemoryNew")]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkMaterializers
{
    private const int Rows = 10_000;
    private const int Iterations = 100;

    private readonly InMemoryDataRepository _ctx;
    private readonly IEnumerable<SimpleEntity> _data;
    private EFInMemoryDataContext? _efCtx;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkMaterializers()
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
    public void Nextorm_ToArray()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.Select(e => e.Id).ToArray().Length;
        _sink = acc;
    }

    [Benchmark]
    public void Linq_ToArray()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.Select(e => e.Id).ToArray().Length;
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_ToArray()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.Select(e => e.Id).ToArray().Length;
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_ToDictionary()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.Select(e => e.Id).ToDictionary(id => id).Count;
        _sink = acc;
    }

    [Benchmark]
    public void Linq_ToDictionary()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.Select(e => e.Id).ToDictionary(id => id).Count;
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_ToDictionary()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.ToDictionary(e => e.Id, e => e.Id).Count;
        _sink = acc;
    }

    [Benchmark]
    public void Nextorm_Last()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _ctx.SimpleEntity.OrderBy(e => e.Id).Last().Id;
        _sink = acc;
    }

    [Benchmark]
    public void Linq_Last()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += _data.OrderBy(e => e.Id).Last().Id;
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_Last()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++) acc += EfContext.SimpleEntities.OrderBy(e => e.Id).Last().Id;
        _sink = acc;
    }
}
