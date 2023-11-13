using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using TupleLargeEntity = System.Tuple<long, string?, System.DateTime?>;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkSimulateWork
{
    const int WorkDuration = 1;
    const int WorkIterations = 1000;
    const int SmallIterations = 10;
    const int LargeListSize = 500;
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<TupleLargeEntity> _cmd;
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

        _cmd = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new TupleLargeEntity(entity.Id, entity.Str, entity.Dt));
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
    public async Task NextormCompiledAsync()
    {
        await foreach (var row in _cmd)
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
            }
        }
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCompiled()
    {
        foreach (var row in await _cmd.Get())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
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
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        foreach (var row in (await _cmd.Get()).ToList())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCached()
    {
        foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).Get())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCachedFetch()
    {
        await foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).Fetch())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        foreach (var row in (await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).Get()).ToList())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id }).Get()).FirstOrDefault();
            }
        }
    }
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

    [Benchmark()]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntityDapper>("select id, someString, dt from large_table where id < @limit", new { limit = LargeListSize }))
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                //var s = await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = row.Id + p });
                var s = (await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = row.Id + p })).FirstOrDefault();
            }
        }
    }
}
