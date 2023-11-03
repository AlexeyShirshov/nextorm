using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkSimulateWork
{
    const int workDuration = 1;
    const int workIterations = 5000;
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<LargeEntity> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkSimulateWork()
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.LargeEntity.Where(it => it.Id < 1000).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt });
        (_ctx.DataProvider as SqlDataProvider).Compile(_cmd, CancellationToken.None);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    private static ValueTask DoWork()
    {
        for (var i = 0; i < workIterations; i++) { }
        return ValueTask.CompletedTask;
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCached()
    {
        await foreach (var row in _cmd)
        {
            await DoWork();
            _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).GetAsyncEnumerator();
        }
    }
    [Benchmark()]
    public async Task NextormFetch()
    {
        await foreach (var row in _cmd.Fetch(CancellationToken.None))
        {
            await DoWork();
            _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).GetAsyncEnumerator();
        }
    }
    [Benchmark()]
    public async Task NextormToListCached()
    {
        foreach (var row in await _cmd.ToListAsync())
        {
            await DoWork();
            _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).GetAsyncEnumerator();
        }
    }
    // [Benchmark()]
    // public async Task Nextorm()
    // {
    //     await foreach (var row in _ctx.SimpleEntity.Select(entity => new { entity.Id }))
    //     {
    //         await Task.Delay(1);
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormToList()
    // {
    //     foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
    //     {
    //         await Task.Delay(1);
    //     }
    // }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.LargeEntities.Where(it => it.Id < 1000).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
            await DoWork();
            var s = await _efCtx.SimpleEntities.FirstOrDefaultAsync(it => it.Id == row.Id);
        }
    }

    [Benchmark]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString, dt from large_table where id < @limit", new { limit = 1000 }))
        {
            await DoWork();
            var s = await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = row.Id });
        }
    }
}
