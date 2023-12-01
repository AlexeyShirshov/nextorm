using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class InMemoryBenchmarkIteration
{
    private readonly InMemoryDataRepository _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly QueryCommand<Tuple<int>> _cmdToList;
    private readonly IEnumerable<SimpleEntity> _data;
    public InMemoryBenchmarkIteration()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        var provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(false);
        _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(true);
    }
    // [Benchmark()]
    // public async Task NextormCompiled()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //     }
    // }
    [Benchmark()]
    public void NextormCompiledSync()
    {
        foreach (var row in _cmd.AsEnumerable())
        {
        }
    }
    [Benchmark()]
    public void NextormCompiledSyncToList()
    {
        foreach (var row in _cmdToList.ToList())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCached()
    {
        foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Exec())
        {
        }
    }
    [Benchmark(Baseline = true)]
    public void NextormCachedSync()
    {
        foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }).AsEnumerable())
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCachedAsync()
    // {
    //     await foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }))
    //     {
    //     }
    // }
    // [Benchmark()]
    public void NextormCachedToList()
    {
        foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToList())
        {
        }
    }
    [Benchmark]
    public void Linq()
    {
        foreach (var row in _data.Select(entity => new { entity.Id }))
        {
        }
    }
    [Benchmark]
    public void LinqToList()
    {
        foreach (var row in _data.Select(entity => new { entity.Id }).ToList())
        {
        }
    }
}
