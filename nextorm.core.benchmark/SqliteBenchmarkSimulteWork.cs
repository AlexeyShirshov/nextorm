using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using TupleLargeEntity = System.Tuple<long, string?, System.DateTime?>;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;

namespace nextorm.core.benchmark;

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
public class SqliteBenchmarkSimulateWork
{
    const int WorkDuration = 1;
    const int WorkIterations = 1000;
    const int SmallIterations = 10;
    const int LargeListSize = 500;
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<TupleLargeEntity> _cmd;
    private readonly QueryCommand<TupleLargeEntity> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly QueryCommand<Tuple<int>> _cmdInner;
    private readonly Func<EFDataContext, int, IAsyncEnumerable<LargeEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int lim) => ctx.LargeEntities.Where(it => it.Id < lim));
    private readonly Func<EFDataContext, long, int, IAsyncEnumerable<SimpleEntity>> _efInnerCompiled = EF.CompileAsyncQuery((EFDataContext ctx, long id, int i) => ctx.SimpleEntities.Where(it => it.Id == id + i));
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkSimulateWork(bool withLogging = false)
    {

        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new TupleLargeEntity(entity.Id, entity.Str, entity.Dt)).Compile(false);

        _cmdToList = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new TupleLargeEntity(entity.Id, entity.Str, entity.Dt)).Compile(true);

        _cmdInner = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0) + NORM.Param<int>(1)).Select(entity => new Tuple<int>(entity.Id)).Compile(false);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db")}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        //_conn.Open();
    }
    private static ValueTask DoWork()
    {
        //await Task.Delay(1);
        //for (var i = 0; i < workIterations; i++) { }
        return ValueTask.CompletedTask;
    }
    // [Benchmark()]
    // public async Task NextormCompiledAsync()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
    //         }
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCompiled()
    // {
    //     foreach (var row in await _cmd.Exec())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = (await _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).Exec()).FirstOrDefault();
    //         }
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCompiledFetch()
    // {
    //     await foreach (var row in _cmd.Pipeline())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = (await _ctx.SimpleEntity.Where(it => it.Id == (row.Item1 + p)).Select(entity => new { entity.Id }).Exec()).FirstOrDefault();
    //         }
    //     }
    // }
    [Benchmark(Baseline = true)]
    public async Task NextormCompiledToList()
    {
        foreach (var row in await _cmdToList.ToListAsync())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var s = (await _cmdInner.Exec(row.Item1, i)).FirstOrDefault();
            }
        }
    }
    // [Benchmark()]
    // public async Task NextormCached()
    // {
    //     foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).Exec())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var cmd = _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id });
    //             var s = (await cmd.Exec()).FirstOrDefault();
    //         }
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCachedFetch()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).Pipeline())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = (await _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id }).Exec()).FirstOrDefault();
    //         }
    //     }
    // }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _ctx.SimpleEntity.Where(it => it.Id == row.Id + p).Select(entity => new { entity.Id }).Exec()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormCachedWithParamsToList()
    {
        var cmdInner = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0) + NORM.Param<int>(1)).Select(entity => new { entity.Id }).Compile(false);
        foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var s = (await cmdInner.Exec(row.Id, i)).FirstOrDefault();
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
    [Benchmark]
    public async Task EFCoreStream()
    {
        await foreach (var row in _efCtx.LargeEntities.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).AsAsyncEnumerable())
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                var s = (await _efCtx.SimpleEntities.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).AsAsyncEnumerable().ToListAsync()).FirstOrDefault();
            }
        }
    }
    [Benchmark]
    public async Task EFCoreCompiled()
    {
        await foreach (var row in _efCompiled(_efCtx, LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }))
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var s = (await _efInnerCompiled(_efCtx, row.Id, i).Select(entity => new { entity.Id }).ToListAsync()).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString as str, dt from large_table where id < @limit", new { limit = LargeListSize }))
        {
            await DoWork();
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                //var s = await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = row.Id + p });
                var s = (await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id+@p", new { id = row.Id, p })).FirstOrDefault();
            }
        }
    }
}
