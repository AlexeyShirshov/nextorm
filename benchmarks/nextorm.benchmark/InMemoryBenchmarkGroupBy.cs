using BenchmarkDotNet.Attributes;
using nextorm.core;

namespace nextorm.benchmark;

/// <summary>
/// <c>GROUP BY</c> plus a per-group count: nextorm in-memory grouping versus raw LINQ and the EF Core
/// InMemory provider.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("InMemoryNew")]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkGroupBy
{
    private const int Rows = 10_000;
    private const int Iterations = 100;

    private readonly InMemoryDataRepository _ctx;
    private readonly IEnumerable<SimpleEntity> _data;
    private EFInMemoryDataContext? _efCtx;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkGroupBy()
    {
        var data = new List<SimpleEntity>(Rows);
        for (var i = 0; i < Rows; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        var provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(Rows);

    [Benchmark(Baseline = true)]
    public void Nextorm_GroupByCount()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.SimpleEntity
                .GroupBy(e => new { Parity = e.Id % 2 })
                .Select(e => new { Key = e.Id % 2, Count = NORM.SQL.count() })
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void Linq_GroupByCount()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _data
                .GroupBy(e => e.Id % 2)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }

    [Benchmark]
    public void EFCoreInMemory_GroupByCount()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = EfContext.SimpleEntities
                .GroupBy(e => e.Id % 2)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToList();
            acc += rows.Count;
        }
        _sink = acc;
    }
}
