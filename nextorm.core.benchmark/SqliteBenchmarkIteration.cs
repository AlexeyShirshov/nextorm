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
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;

    public SqliteBenchmarkIteration(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        if (withLogging)
        {
            builder.UseLoggerFactory(LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id));
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmd, CancellationToken.None);

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
        await foreach (var row in _cmd)
        {
        }
    }
    [Benchmark(Baseline = true)]
    public async Task NextormCompiled()
    {
        foreach (var row in await _cmd.Get())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCompiledToList()
    {
        foreach (var row in (await _cmd.Get()).ToList())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCached()
    {
        foreach (var row in await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Get())
        {
        }
    }
    [Benchmark()]
    public async Task NextormCachedToList()
    {
        foreach (var row in (await _ctx.SimpleEntity.Select(entity => new { entity.Id }).Get()).ToList())
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
    public async Task Dapper()
    {
        foreach (var row in await _conn.QueryAsync<SimpleEntity>("select * from simple_entity"))
        {
        }
    }
}
