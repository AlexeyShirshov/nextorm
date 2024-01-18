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
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkIteration
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<SimpleEntity> _cmd;
    private readonly IPreparedQueryCommand<SimpleEntity> _cmdToList;
    private readonly IPreparedQueryCommand<SimpleEntity> _cmdManualToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, IAsyncEnumerable<SimpleEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities);
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkIteration(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.Prepare(false);

        _cmdToList = _ctx.SimpleEntity.Prepare();

        _cmdManualToList = _ctx.SimpleEntity.PrepareFromSql("select id from simple_entity");

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
    [Benchmark()]
    public async Task Nextorm_Prepared_AsyncStream()
    {
        await foreach (var row in _cmd.AsAsyncEnumerable(_db))
        {
        }
    }
    // [Benchmark(Baseline = true)]
    // public async Task NextormPrepared()
    // {
    //     foreach (var row in await _cmd.Exec())
    //     {
    //     }
    // }
    [Benchmark()]
    public async Task Nextorm_Prepared_ToListAsync()
    {
        foreach (var row in await _cmdToList.ToListAsync(_db))
        {
        }
    }
    [Benchmark()]
    public async Task Nextorm_PreparedManualSql_ToListAsync()
    {
        foreach (var row in await _cmdManualToList.ToListAsync(_db))
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCached()
    // {
    //     foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Exec())
    //     {
    //     }
    // }
    [Benchmark()]
    public async Task Nextorm_Cached_ToListAsync()
    {
        foreach (var row in await _ctx.SimpleEntity.ToListAsync())
        {
        }
    }
    [Benchmark()]
    public async Task Nextorm_CachedManualSql_ToListAsync()
    {
        var cmd = _ctx.SimpleEntity.ToCommand().WithSql("select id from simple_entity");
        foreach (var row in await cmd.ToListAsync())
        {
        }
    }
    // [Benchmark]
    // public async Task EFCore()
    // {
    //     foreach (var row in await _efCtx.SimpleEntities.ToListAsync())
    //     {
    //     }
    // }
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
    [Benchmark]
    public async Task EFCore_Compiled_ToListAsync()
    {
        foreach (var row in await _efCompiled(_efCtx).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task Dapper_AsyncStream()
    {
        await foreach (var row in _conn.QueryUnbufferedAsync<SimpleEntity>("select id from simple_entity"))
        {
        }
    }
    [Benchmark]
    public async Task DapperAsync()
    {
        foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity"))
        {
        }
    }
}
