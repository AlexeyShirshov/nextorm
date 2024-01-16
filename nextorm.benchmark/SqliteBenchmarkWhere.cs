using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using nextorm.core;

namespace nextorm.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkWhere
{
    const int Iterations = 100;
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<SimpleEntity> _cmd;
    private readonly IPreparedQueryCommand<SimpleEntity> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, int, IAsyncEnumerable<SimpleEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.SimpleEntities.Where(it => it.Id == i));
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkWhere(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        builder.UseSqlite(filepath);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).ToCommand().Prepare(false);

        _cmdToList = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).ToCommand().Prepare(true);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={filepath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDbContext)_ctx.DbContext).ConnectionString);
        _conn.Open();
    }
    [Benchmark()]
    //[BenchmarkCategory("Stream")]
    public async Task NextormCompiledAsync()
    {
        for (var i = 0; i < Iterations; i++)
        {
            await foreach (var row in _db.AsAsyncEnumerable(_cmd, i))
            {
            }
        }
    }
    [Benchmark(Baseline = true)]
    //[BenchmarkCategory("Stream")]
    public async Task NextormCompiled()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _db.AsEnumerableAsync(_cmd, i))
            {
            }
        }
    }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public async Task NextormCompiledToList()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _db.ToListAsync(_cmdToList, i))
            {
            }
        }
    }
    // [Benchmark()]
    // public async Task Nextorm()
    // {
    //     for (var i = 0; i < Iterations; i++)
    //     {
    //         var p = i;
    //         // var cmd = new QueryCommand<SimpleEntity>(_ctx.DataProvider,
    //         //     (ISimpleEntity entity) => new SimpleEntity { Id = entity.Id },
    //         //     (ISimpleEntity it) => it.Id == p);
    //         var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
    //         cmd.Cache = false;
    //         // cmd.PrepareCommand(CancellationToken.None);
    //         await foreach (var row in cmd)
    //         {
    //         }
    //     }
    // }
    [Benchmark()]
    //[BenchmarkCategory("Stream")]
    public async Task NextormCachedParam()
    {
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id }).Prepare(false);
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _db.AsEnumerableAsync(cmd, i))
            {
            }
        }
    }
    [Benchmark()]
    //[BenchmarkCategory("Stream")]
    public async Task NextormCached()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var p = i;
            var cmd = _ctx.SimpleEntity.Where(it => it.Id == p).Select(entity => new { entity.Id });
            foreach (var row in await cmd.AsEnumerableAsync())
            {
            }
        }
    }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public async Task NextormCachedToList()
    {
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id }).Prepare(true);
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _db.ToListAsync(cmd, i))
            {
            }
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Buffered")]
    public async Task EFCore()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var p = i;
            foreach (var row in await _efCtx.SimpleEntities.Where(it => it.Id == p).Select(entity => new { entity.Id }).ToListAsync())
            {
            }
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task EFCoreStream()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var p = i;
            await foreach (var row in _efCtx.SimpleEntities.Where(it => it.Id == p).Select(entity => new { entity.Id }).AsAsyncEnumerable())
            {
            }
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task EFCoreCompiled()
    {
        for (var i = 0; i < Iterations; i++)
        {
            await foreach (var row in _efCompiled(_efCtx, i).Select(entity => new { entity.Id }))
            {
            }
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Buffered")]
    public async Task Dapper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = i }))
            {
            }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task DapperUnbuffered()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _conn.QueryUnbufferedAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = i }))
            {
            }
    }
}
