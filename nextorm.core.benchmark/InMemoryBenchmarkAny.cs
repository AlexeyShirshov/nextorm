using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class InMemoryBenchmarkAny
{
    private readonly InMemoryDataContext _ctx;
    private readonly QueryCommand<bool> _cmd;
    private readonly QueryCommand<bool> _cmdToList;
    private readonly IEnumerable<SimpleEntity> _data;
    public InMemoryBenchmarkAny()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        var builder = new DataContextOptionsBuilder();
        builder.UseInMemoryClient();
        _ctx = new InMemoryDataContext(builder);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.AnyCommand().Compile(true);
        // _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(true);
    }
    [Benchmark()]
    public void NextormCompiled()
    {
        _cmd.Any();
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
