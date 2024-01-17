using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.SQLite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using nextorm.core;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkSimulateWork
{
    const int WorkDuration = 1;
    const int WorkIterations = 1000;
    const int SmallIterations = 10;
    const int LargeListSize = 500;
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<LargeEntity> _cmd;
    private readonly IPreparedQueryCommand<LargeEntity> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SQLiteConnection _conn;
    private readonly IPreparedQueryCommand<SimpleEntity?> _cmdInner;
    private readonly Func<EFDataContext, IAsyncEnumerable<LargeEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities.Where(it => it.Id < LargeListSize));
    private readonly Func<EFDataContext, long, int, Task<SimpleEntity?>> _efInnerCompiled = EF.CompileAsyncQuery((EFDataContext ctx, long id, int i) => ctx.SimpleEntities.Where(it => (it.Id - i) == id).FirstOrDefault());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkSimulateWork(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        _conn = new SQLiteConnection($"Data Source='{filepath}'");
        _conn.Open();

        builder.UseSqlite(_conn);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _ctx.DbContext.EnsureConnectionOpen();

        _cmd = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).Prepare(false);

        _cmdToList = _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).Prepare(true);

        _cmdInner = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0) + NORM.Param<int>(1)).FirstOrFirstOrDefaultCommand().Prepare(true);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(_conn);
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);
    }
    // [Benchmark()]
    // public async Task NextormPreparedAsync()
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
    [Benchmark()]
    public async Task NextormPreparedStreamAsync()
    {
        foreach (var row in await _cmd.AsEnumerableAsync(_db))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                var _ = await _cmdInner.FirstOrDefaultAsync(_db, row.Id, i);
            }
        }
    }
    // [Benchmark()]
    // public async Task NextormPreparedFetch()
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
    public async Task NextormPreparedToListAsync()
    {
        foreach (var row in await _cmdToList.ToListAsync(_db))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                await _cmdInner.FirstOrDefaultAsync(_db, row.Id, i);
                //var s = await _cmdInner.AnyAsync(row.Item1, i);
            }
        }
    }
    [Benchmark()]
    public async Task NextormCachedAsyncStream()
    {
        await foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                var _ = await _ctx.SimpleEntity.Where(it => it.Id == row.Id + i).Select(entity => new { entity.Id }).FirstOrDefaultAsync();
            }
        }
    }
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
    public void NextormCachedToList()
    {
        foreach (var row in _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToList())
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                var _ = _ctx.SimpleEntity.Where(it => it.Id == row.Id + i).Select(entity => new { entity.Id }).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task NextormPreparedForLoopToListAsync()
    {
        var cmdInner = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0) + NORM.Param<int>(1)).FirstOrFirstOrDefaultCommand(entity => new { entity.Id }).Prepare();
        foreach (var row in await _ctx.LargeEntity.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                await _db.FirstOrDefaultAsync(cmdInner, row.Id, i);
            }
        }
    }
    // [Benchmark]
    // public async Task EFCore()
    // {
    //     foreach (var row in await _efCtx.LargeEntities.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = (await _efCtx.SimpleEntities.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).ToListAsync()).FirstOrDefault();
    //         }
    //     }
    // }
    // [Benchmark]
    // public async Task EFCoreStream()
    // {
    //     await foreach (var row in _efCtx.LargeEntities.Where(it => it.Id < LargeListSize).Select(entity => new { entity.Id, entity.Str, entity.Dt }).AsAsyncEnumerable())
    //     {
    //         await DoWork();
    //         for (var i = 0; i < SmallIterations; i++)
    //         {
    //             var p = i;
    //             var s = (await _efCtx.SimpleEntities.Where(it => it.Id == (row.Id + p)).Select(entity => new { entity.Id }).AsAsyncEnumerable().ToListAsync()).FirstOrDefault();
    //         }
    //     }
    // }
    [Benchmark]
    public async Task EFCoreCompiledToListAsync()
    {
        foreach (var row in await _efCompiled(_efCtx).ToListAsync())
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                await _efInnerCompiled(_efCtx, row.Id, i);
            }
        }
    }
    [Benchmark()]
    public async Task DapperAsync()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString as str, dt from large_table where id < @limit", new { limit = LargeListSize }))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id+@p", new { id = row.Id, p });
                // var s = (await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id+@p", new { id = row.Id, p })).FirstOrDefault();
            }
        }
    }
    [Benchmark()]
    public async Task DapperAsyncStream()
    {
        await foreach (var row in _conn.QueryUnbufferedAsync<LargeEntity>("select id, someString as str, dt from large_table where id < @limit", new { limit = LargeListSize }))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                var p = i;
                await _conn.QueryFirstOrDefaultAsync<SimpleEntity>("select id from simple_entity where id=@id+@p", new { id = row.Id, p });
                // var s = (await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id+@p", new { id = row.Id, p })).FirstOrDefault();
            }
        }
    }
}
