using BenchmarkDotNet.Attributes;
using nextorm.core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class InMemoryBenchmarkWhere
{
    const int Iterations = 100;
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

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new Tuple<int>(entity.Id)).Compile(false);
    }
    [Benchmark(Baseline = true)]
    public void NextormCompiledParam()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _cmd.AsEnumerable(i))
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
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id }).Compile(false);
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
