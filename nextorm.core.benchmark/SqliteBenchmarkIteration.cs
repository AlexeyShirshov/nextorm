using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkIteration
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkIteration()
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id));
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmd);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCached()
    {
        await foreach (var row in _cmd)
        {
        }
    }
    [Benchmark()]
    public async Task NextormToListCached()
    {
        foreach (var row in await _cmd.ToListAsync())
        {
        }
    }
    [Benchmark()]
    public async Task Nextorm()
    {
        await foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }))
        {
        }
    }
    [Benchmark()]
    public async Task NextormToList()
    {
        foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.SimpleEntities.Select(entity => new { entity.Id }).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<SimpleEntity>("select * from simple_entity"))
        {
        }
    }
}
