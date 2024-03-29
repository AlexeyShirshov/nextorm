﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using nextorm.core;

namespace nextorm.benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class InMemoryBenchmarkIteration
{
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmd;
    private readonly IPreparedQueryCommand<Tuple<int>> _cmdToList;
    private readonly IEnumerable<SimpleEntity> _data;
    private readonly IDataContext _provider;

    public InMemoryBenchmarkIteration()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        _provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(_provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Prepare(false);
        _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Prepare(true);
    }
    // [Benchmark()]
    // public async Task NextormPrepared()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //     }
    // }
    [Benchmark()]
    public void NextormPreparedSync()
    {
        foreach (var row in _provider.GetEnumerable(_cmd))
        {
        }
    }
    [Benchmark()]
    public void NextormPreparedSyncToList()
    {
        foreach (var row in _provider.ToList(_cmdToList))
        {
        }
    }
    [Benchmark()]
    public async Task NextormCached()
    {
        await foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToAsyncEnumerable())
        {
        }
    }
    [Benchmark(Baseline = true)]
    public void NextormCachedSync()
    {
        foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToEnumerable())
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
