using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Linq.Expressions;

namespace nextorm.core.benchmark;

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
