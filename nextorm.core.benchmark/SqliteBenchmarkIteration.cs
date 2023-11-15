using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkIteration
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<Tuple<int>> _cmd;
    private readonly QueryCommand<Tuple<int>> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, IAsyncEnumerable<SimpleEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities);
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkIteration(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(false);

        _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(true);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);
        _conn.Open();
    }
    [Benchmark()]
    public async Task NextormCompiledAsync()
    {
        await foreach (var row in _cmd)
        {
        }
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCompiled()
    {
        foreach (var row in await _cmd.Exec())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        foreach (var row in await _cmdToList.ToListAsync())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCached()
    {
        foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Exec())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task EFCore()
    {
        foreach (var row in await _efCtx.SimpleEntities.Select(entity => new { entity.Id }).ToListAsync())
        {
        }
    }
    [Benchmark]
    public async Task EFCoreStream()
    {
        await foreach (var row in _efCtx.SimpleEntities.Select(entity => new { entity.Id }).AsAsyncEnumerable())
        {
        }
    }
    [Benchmark]
    public async Task EFCoreCompiled()
    {
        await foreach (var row in _efCompiled(_efCtx).Select(entity => new { entity.Id }))
        {
        }
    }
    [Benchmark]
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<SimpleEntity>("select id from simple_entity"))
        {
        }
    }
    [Benchmark]
    public async Task DapperUnbuffered()
    {
        await foreach (var row in _conn.QueryUnbufferedAsync<SimpleEntity>("select id from simple_entity"))
        {
        }
    }
}
