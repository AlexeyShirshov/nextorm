using BenchmarkDotNet.Attributes;
using NextORM.Sqlite;
using Microsoft.EntityFrameworkCore;
using BenchmarkDotNet.Jobs;
using NextORM.Core;
using DataContext = NextORM.Core.DataContext;

namespace NextORM.Benchmark;

[Config(typeof(NextormConfig))]
[MemoryDiagnoser]
public class SqliteBenchmarkMakeSelect
{
    private readonly TestDataRepository _ctx;
    private readonly DataContext _provider;
    private readonly QueryCommand _cmd;

    public SqliteBenchmarkMakeSelect()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _ctx = new TestDataRepository(builder.CreateDataContext());

        _provider = (DataContext)_ctx.DataContext;

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
