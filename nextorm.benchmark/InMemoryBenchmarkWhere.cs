using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using nextorm.core;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class InMemoryBenchmarkWhere
{
    const int Iterations = 100;
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmd;
    private readonly IEnumerable<SimpleEntity> _data;
    private readonly IDataContext _provider;

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
    [Benchmark(Baseline = true)]
    public void NextormCompiledParam()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _provider.AsEnumerable(_cmd, i))
            {
            }
        }
    }
    // [Benchmark()]
    // public async Task NextormToListCached()
    // {
    //     foreach (var row in await _cmd.ToListAsync())
    //     {
    //     }
    // }
    [Benchmark()]
    public void NextormCachedParam()
    {
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id });
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in cmd.AsEnumerable(i))
            {
            }
        }
    }
    [Benchmark()]
    public void NextormCached()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var p = i;
            var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
            foreach (var row in cmd.AsEnumerable())
            {
            }
        }
    }
    [Benchmark]
    public void Linq()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _data.Where(it => it.Id == i).Select(entity => new { entity.Id }))
            {
            }
        }
    }
}
