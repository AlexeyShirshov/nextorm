using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.Benchmark;

[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class InMemoryBenchmarkAny
{
    private readonly InMemoryDataRepository _ctx;
    private readonly IPreparedQueryCommand<bool> _cmd;
    private readonly IEnumerable<SimpleEntity> _data;
    private EFInMemoryDataContext? _efCtx;
    private Func<EFInMemoryDataContext, bool>? _efCompiled;

    // Consumed by every benchmark to keep the JIT from eliminating the (otherwise unused) work.
    private int _sink;

    public InMemoryBenchmarkAny()
    {
        var data = new List<SimpleEntity>(10_000);
        for (var i = 0; i < 10_000; i++)
            data.Add(new SimpleEntity { Id = 1 });
        _data = data;

        var provider = new InMemoryDataContext();
        _ctx = new InMemoryDataRepository(provider);
        _ctx.SimpleEntity.WithData(_data);

        _cmd = _ctx.SimpleEntity.AnyCommand().Prepare(true);
    }

    private EFInMemoryDataContext EfContext => _efCtx ??= EfInMemory.Create(10_000);

    [Benchmark()]
    public void NextormPrepared()
    {
        _sink += _ctx.DataProvider.Any(_cmd, null) ? 1 : 0;
    }
    [Benchmark]
    public void Linq()
    {
        _sink += _data.Any() ? 1 : 0;
    }
    [Benchmark]
    public void EFCoreInMemory_Compiled()
    {
        _efCompiled ??= EF.CompileQuery((EFInMemoryDataContext ctx) => ctx.SimpleEntities.Any());
        _sink += _efCompiled(EfContext) ? 1 : 0;
    }
}
