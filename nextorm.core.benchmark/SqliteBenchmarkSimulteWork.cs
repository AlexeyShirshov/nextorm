using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkSimulateWork
{
    const int WorkDuration = 1;
    const int WorkIterations = 1000;
    const int SmallIterations = 10;
    const int LargeListSize = 500;
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<LargeEntity> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkSimulateWork(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        if (withLogging)
        {
            builder.UseLoggerFactory(LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt });
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmd, CancellationToken.None);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    private static ValueTask DoWork()
    {
        //await Task.Delay(1);
        //for (var i = 0; i < workIterations; i++) { }
        return ValueTask.CompletedTask;
    }
    [Benchmark()]
    public async Task NextormCompiled()
    {
        await foreach (var row in _cmd)
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = _ctx.SimpleEntity.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCompiledFetch()
    {
        await foreach (var row in _cmd.Fetch())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = _ctx.SimpleEntity.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        foreach (var row in await _cmd.ToListAsync())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = _ctx.SimpleEntity.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
            }
        }
    }
    // [Benchmark()]
    // public async Task NextormCached()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }))
    //     {
    //         await DoWork();
    //         var s = _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCachedFetch()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).Fetch())
    //     {
    //         await DoWork();
    //         var s = _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCachedToList()
    // {
    //     foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).ToListAsync())
    //     {
    //         await DoWork();
    //         var s = _ctx.SimpleEntity.Where(it => it.Id == row.Id).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
    //     }
    // }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.LargeEntities.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _efCtx.SimpleEntities.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).ToListAsync()).FirstOrDefault();
            }
        }
    }

    [Benchmark(Baseline = true)]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString, dt from large_table where id < @limit", new { limit = LargeListSize }))
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = row.Id + p });
            }
        }
    }
}
