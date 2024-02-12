using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;
using nextorm.core;
using DbContext = nextorm.core.DbContext;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class SqliteBenchmarkMakeSelect
{
    private readonly TestDataRepository _ctx;
    private readonly DbContext _provider;
    private readonly QueryCommand _cmd;

    public SqliteBenchmarkMakeSelect()
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        _ctx = new TestDataRepository(builder.CreateDbContext());

        _provider = (DbContext)_ctx.DbContext;

        var p = 10;
        _cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
        _cmd.PrepareCommand(CancellationToken.None);
    }
    // [Benchmark(Baseline = true)]
    // public void MakeParams()
    // {
    //     var p = 10;
    //     var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
    //     cmd.PrepareCommand(CancellationToken.None);
    //     cmd.GetHashCode();
    //     cmd.Equals(cmd);
    //     _provider.MakeSelect(cmd, true);
    // }
    // [Benchmark()]
    // public void MakeSelect()
    // {
    //     var p = 10;
    //     var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
    //     cmd.PrepareCommand(CancellationToken.None);
    //     _provider.MakeSelect(cmd, false);
    // }
    // [Benchmark()]
    // public void Lookup()
    // {
    //     var p = 10;
    //     var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
    //     cmd.PrepareCommand(CancellationToken.None);
    //     cmd.GetHashCode();
    //     cmd.Equals(cmd);
    // }
}
