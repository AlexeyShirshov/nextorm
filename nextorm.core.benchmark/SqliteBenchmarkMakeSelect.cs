using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;

namespace nextorm.core.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class SqliteBenchmarkMakeSelect
{
    private readonly TestDataContext _ctx;
    private readonly SqlDataProvider _provider;
    private readonly QueryCommand _cmd;

    public SqliteBenchmarkMakeSelect()
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        _ctx = new TestDataContext(builder);

        _provider = (SqlDataProvider)_ctx.DataProvider;

        var p = 10;
        _cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        _cmd.PrepareCommand(CancellationToken.None);
    }
    [Benchmark(Baseline = true)]
    public void MakeParams()
    {
        var p = 10;
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        cmd.PrepareCommand(CancellationToken.None);
        cmd.GetHashCode();
        cmd.Equals(cmd);
        _provider.MakeSelect(cmd, true);
    }
    [Benchmark()]
    public void MakeSelect()
    {
        var p = 10;
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        cmd.PrepareCommand(CancellationToken.None);
        _provider.MakeSelect(cmd, false);
    }
    [Benchmark()]
    public void Lookup()
    {
        var p = 10;
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        cmd.PrepareCommand(CancellationToken.None);
        cmd.GetHashCode();
        cmd.Equals(cmd);
    }
}
