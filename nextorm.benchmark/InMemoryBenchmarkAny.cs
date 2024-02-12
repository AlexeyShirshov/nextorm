using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using nextorm.core;

namespace nextorm.benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class InMemoryBenchmarkAny
{
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<bool> _cmd;
    private readonly IPreparedQueryCommand<bool> _cmdToList;
    private readonly IEnumerable<SimpleEntity> _data;
    public InMemoryBenchmarkAny()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        var provider = new InMemoryContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.AnyCommand().Prepare(true);
        // _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(true);
    }
    [Benchmark()]
    public void NextormPrepared()
    {
        _ctx.DataProvider.Any(_cmd, null);
    }
    [Benchmark()]
    public void NextormCached()
    {
        _ctx.SimpleEntity.Any();
    }
    [Benchmark]
    public void Linq()
    {
        _data.Any();
    }
}
