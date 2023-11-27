using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using System.Data.Entity;

namespace nextorm.core.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class SqliteBenchmarkAny
{
    private readonly TestDataContext _ctx;
    // private readonly QueryCommand<Tuple<int>> _cmd;
    // private readonly QueryCommand<Tuple<int>> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, Task<bool>> _efCompiled = EF.CompileQuery((EFDataContext ctx) => ctx.SimpleEntities.AnyAsync());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkAny(bool withLogging = false)
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

        // _cmd = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(false);

        // _cmdToList = _ctx.SimpleEntity.Select(entity => new Tuple<int>(entity.Id)).Compile(true);

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
    // [Benchmark()]
    // public async Task NextormCompiledToList()
    // {
    //     foreach (var row in await _cmdToList.ToListAsync())
    //     {
    //     }
    // }
    [Benchmark()]
    public async Task NextormCached()
    {
        await _ctx.SimpleEntity.AnyAsync();
    }
    [Benchmark]
    public async Task EFCore()
    {
        await _efCtx.SimpleEntities.AnyAsync();
    }
    [Benchmark]
    public async Task EFCoreCompiled()
    {
        await _efCompiled(_efCtx);
    }
    [Benchmark]
    public async Task Dapper()
    {
        await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity)");
    }
}
