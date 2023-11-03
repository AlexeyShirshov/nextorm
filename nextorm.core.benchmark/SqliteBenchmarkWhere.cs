using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkWhere
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<SimpleEntity>[] _cmds = new QueryCommand<SimpleEntity>[100];
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkWhere()
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        _ctx = new TestDataContext(builder);

        for (var i = 0; i < 100; i++)
        {
            var cmd = _ctx.SimpleEntity.Where(it => it.Id == i).Select(entity => new SimpleEntity { Id = entity.Id });
            (_ctx.DataProvider as SqlDataProvider).Compile(cmd, CancellationToken.None);
            _cmds[i] = cmd;
        }

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCached()
    {
        for (var i = 0; i < 100; i++)
            await foreach (var row in _cmds[i])
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
        for (var i = 0; i < 100; i++)
            await foreach (var row in _ctx.SimpleEntity.Where(it => it.Id == i).Select(entity => new { entity.Id }))
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
        for (var i = 0; i < 100; i++)
            foreach (var row in await _efCtx.SimpleEntities.Where(it => it.Id == i).Select(entity => new { entity.Id }).ToListAsync())
            {
            }
    }
    [Benchmark]
    public async Task Dapper()
    {
        for (var i = 0; i < 100; i++)
            foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = i }))
            {
            }
    }
}
