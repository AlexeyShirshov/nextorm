using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;

namespace nextorm.core.benchmark;

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
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
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        _ctx = new TestDataContext(builder);

        _provider = (SqlDataProvider)_ctx.DataProvider;

        var p = 10;
        _cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        _cmd.PrepareCommand(CancellationToken.None);
    }
    [Benchmark(Baseline = true)]
    public void MakeParams()
    {
        _provider.MakeSelect(_cmd, true);
    }
    [Benchmark()]
    public void MakeSelect()
    {
        _provider.MakeSelect(_cmd, false);
    }
}
