using BenchmarkDotNet.Attributes;
using nextorm.core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class InMemoryBenchmarkWhere
{
    private readonly InMemoryDataContext _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly IEnumerable<SimpleEntity> _data;
    public InMemoryBenchmarkWhere()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = i });
        _data = data;

        var builder = new DataContextOptionsBuilder();
        builder.UseInMemoryClient();
        _ctx = new InMemoryDataContext(builder);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Where(it => it.Id < 15693).Select(entity => new Tuple<int>(entity.Id));
        _ctx.DataProvider.Compile(_cmd, false, CancellationToken.None);
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
        await foreach (var row in _ctx.SimpleEntity.Where(it => it.Id < 15693).Select(entity => new { entity.Id }))
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
        foreach (var row in _data.Where(it => it.Id < 15693).Select(entity => new { entity.Id }))
        {
        }
    }
}
