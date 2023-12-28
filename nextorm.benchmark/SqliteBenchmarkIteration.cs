using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using nextorm.core;

namespace nextorm.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class SqliteBenchmarkIteration
{
    private readonly TestDataRepository _ctx;
    private readonly QueryCommand<SimpleEntity> _cmd;
    private readonly QueryCommand<SimpleEntity> _cmdToList;
    private readonly QueryCommand<SimpleEntity> _cmdManualToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    // private readonly Func<EFDataContext, DbSet<SimpleEntity>> _efCompiled = EF.CompileQuery((EFDataContext ctx) => ctx.SimpleEntities);
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkIteration(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataRepository(builder.CreateDbContext());
        _ctx.DbContext.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.ToCommand().Compile(false);

        _cmdToList = _ctx.SimpleEntity.ToCommand().Compile(true);

        _cmdManualToList = _ctx.SimpleEntity.ToCommand().Compile("select id from simple_entity", true);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db")}");
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
    // [Benchmark()]
    // public async Task NextormCompiledAsync()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //     }
    // }
    // [Benchmark(Baseline = true)]
    // public async Task NextormCompiled()
    // {
    //     foreach (var row in await _cmd.Exec())
    //     {
    //     }
    // }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        foreach (var row in await _cmdToList.ToListAsync())
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCompiledManualToList()
    // {
    //     foreach (var row in await _cmdManualToList.ToListAsync())
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCached()
    // {
    //     foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Exec())
    //     {
    //     }
    // }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        foreach (var row in await _ctx.SimpleEntity.ToListAsync())
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormManualSQLCachedToList()
    // {
    //     var cmd = _ctx.SimpleEntity.Select(entity => new { entity.Id }).FromSql("select id from simple_entity");
    //     foreach (var row in await cmd.ToListAsync())
    //     {
    //     }
    // }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.SimpleEntities.ToListAsync())
        {
        }
    }
    // [Benchmark]
    // public async Task EFCoreAny()
    // {
    //     await _efCtx.SimpleEntities.Select(entity => new { entity.Id }).AnyAsync();
    // }
    // [Benchmark]
    // public async Task EFCoreStream()
    // {
    //     await foreach (var row in _efCtx.SimpleEntities.Select(entity => new { entity.Id }).AsAsyncEnumerable())
    //     {
    //     }
    // }
    // [Benchmark]
    // public async Task EFCoreCompiled()
    // {
    //     foreach (var row in await _efCompiled(_efCtx))
    //     {
    //     }
    // }
    // [Benchmark]
    // public async Task DapperUnbuffered()
    // {
    //     await foreach (var row in _conn.QueryUnbufferedAsync<SimpleEntity>("select id from simple_entity"))
    //     {
    //     }
    // }
    [Benchmark]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity"))
        {
        }
    }
}
