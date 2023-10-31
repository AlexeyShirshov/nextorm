using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqlBenchmarkLargeIteration
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<LargeEntity> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqlBenchmarkLargeIteration()
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.LargeEntity.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt });
        (_ctx.DataProvider as SqlDataProvider).Compile(_cmd);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    // public async Task FillLargeTable()
    // {
    //     var r = new Random(Environment.TickCount);

    //     for(var i=0;i<10_000;i++)
    //         await _conn.ExecuteAsync("insert into large_table(someString,dt) values(@str,@dt)", new {str=Guid.NewGuid().ToString(),dt=DateTime.Now.AddDays(r.Next(-10,10))});
    // }
    [Benchmark(Baseline = true)]
    public async Task NextormCached()
    {
        await foreach (var row in _cmd)
        {
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
    public async Task Nextorm()
    {
        await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }))
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormToList()
    // {
    //     foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
    //     {
    //     }
    // }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.LargeEntities.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select * from large_table"))
        {
        }
    }
}
