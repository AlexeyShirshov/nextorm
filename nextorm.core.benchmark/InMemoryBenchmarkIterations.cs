using BenchmarkDotNet.Attributes;
using nextorm.core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class InMemoryBenchmarkIteration
{
    private readonly InMemoryDataContext _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly IEnumerable<SimpleEntity> _data;
    public InMemoryBenchmarkIteration()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        var builder = new DataContextOptionsBuilder();
        builder.UseInMemoryClient();
        _ctx = new InMemoryDataContext(builder);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(false);
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCached()
    {
        await foreach (var row in _cmd)
        {
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
    public async Task Nextorm()
    {
        await foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }))
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormToList()
    // {
    //     foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
    //     {
    //     }
    // }
    [Benchmark]
    public void Linq()
    {
        foreach (var row in _data.Select(entity => new { entity.Id }))
        {
        }
    }
}
