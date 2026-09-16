using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.EntityFrameworkCore;
using nextorm.core;

namespace nextorm.benchmark;

[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkIteration
{
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmd;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmdToList;
    private readonly IEnumerable<SimpleEntity> _data;
    private readonly IDataContext _provider;
    private EFInMemoryDataContext? _efCtx;
    private Func<EFInMemoryDataContext, IEnumerable<int>>? _efCompiled;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkIteration()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        _provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(_provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Prepare(false);
        _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Prepare(true);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(10_000);

    [Benchmark(Baseline = true)]
    public void NextormPreparedSync()
    {
        var acc = 0L;
        foreach (var row in _provider.GetEnumerable(_cmd))
        {
            acc += row.Item1;
        }
        _sink = acc;
    }
    [Benchmark()]
    public void NextormPreparedSyncToList()
    {
        var acc = 0L;
        foreach (var row in _provider.ToList(_cmdToList))
        {
            acc += row.Item1;
        }
        _sink = acc;
    }
    [Benchmark]
    public void Linq()
    {
        var acc = 0L;
        foreach (var row in _data.Select(entity => new { entity.Id }))
        {
            acc += row.Id;
        }
        _sink = acc;
    }
    [Benchmark]
    public void LinqToList()
    {
        var acc = 0L;
        foreach (var row in _data.Select(entity => new { entity.Id }).ToList())
        {
            acc += row.Id;
        }
        _sink = acc;
    }
    [Benchmark]
    public void EFCoreInMemory_Compiled()
    {
        _efCompiled ??= EF.CompileQuery((EFInMemoryDataContext ctx) => ctx.SimpleEntities.Select(e => e.Id));
        var acc = 0L;
        foreach (var row in _efCompiled(EfContext))
        {
            acc += row;
        }
        _sink = acc;
    }
    [Benchmark]
    public void EFCoreInMemory_Compiled_ToList()
    {
        _efCompiled ??= EF.CompileQuery((EFInMemoryDataContext ctx) => ctx.SimpleEntities.Select(e => e.Id));
        var acc = 0L;
        foreach (var row in _efCompiled(EfContext).ToList())
        {
            acc += row;
        }
        _sink = acc;
    }
}
