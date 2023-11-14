using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Linq.Expressions;
using BenchmarkDotNet.Loggers;
using Microsoft.Extensions.Logging;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkWhere
{
    const int Iterations = 100;
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly QueryCommand<Tuple<int>> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkWhere(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        if (withLogging)
        {
            builder.UseLoggerFactory(LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new Tuple<int>(entity.Id));
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmd, false, CancellationToken.None);

        _cmdToList = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new Tuple<int>(entity.Id));
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmdToList, true, CancellationToken.None);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    [Benchmark()]
    public async Task NextormCompiledAsync()
    {
        for (var i = 0; i < Iterations; i++)
        {
            await foreach (var row in _cmd.WithParams(i))
            {
            }
        }
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCompiled()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _cmd.WithParams(i).Exec())
            {
            }
        }
    }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await _cmdToList.WithParams(i).ToListAsync())
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
    public async Task NextormCached()
    {
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id });
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await cmd.WithParams(i).Exec())
            {
            }
        }
    }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        var cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(entity => new { entity.Id });
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in await cmd.WithParams(i).ToListAsync())
            {
            }
        }
    }
    [Benchmark]
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
    public async Task Dapper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity where id=@id", new { id = i }))
            {
            }
    }
}
