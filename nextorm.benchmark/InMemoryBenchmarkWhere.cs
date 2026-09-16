using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.EntityFrameworkCore;
using nextorm.core;

namespace nextorm.benchmark;

[Config(typeof(NextormConfig))]
[MemoryDiagnoser]
public class InMemoryBenchmarkWhere
{
    const int Iterations = 100;
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmd;
    private readonly IEnumerable<SimpleEntity> _data;
    private readonly IDataContext _provider;
    private EFInMemoryDataContext? _efCtx;
    private Func<EFInMemoryDataContext, int, IEnumerable<int>>? _efCompiled;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private long _sink;

    public InMemoryBenchmarkWhere()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        _provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(_provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new Tuple<int>(entity.Id)).Prepare(false);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(10_000);

    [Benchmark(Baseline = true)]
    public void NextormPreparedParam()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _provider.GetEnumerable(_cmd, i))
            {
                acc += row.Item1;
            }
        }
        _sink = acc;
    }
    [Benchmark]
    public void Linq()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _data.Where(it => it.Id == i).Select(entity => new { entity.Id }))
            {
                acc += row.Id;
            }
        }
        _sink = acc;
    }
    [Benchmark]
    public void EFCoreInMemory_Compiled()
    {
        _efCompiled ??= EF.CompileQuery((EFInMemoryDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id == id).Select(it => it.Id));
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _efCompiled(EfContext, i))
            {
                acc += row;
            }
        }
        _sink = acc;
    }
}
