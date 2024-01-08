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
public class SqliteBenchmarkAny
{
    private TestDataRepository _ctx;
    private QueryCommand<bool> _cmd;
    // private QueryCommand<bool> _cmdFilter;
    // private QueryCommand<bool> _cmdFilterParam;
    private EFDataContext _efCtx;
    private SqliteConnection _conn;
    private Func<EFDataContext, Task<bool>> _efCompiled;
    // private readonly Func<EFDataContext, Task<bool>> _efCompiledFilter = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id > 5).Any());
    // private readonly Func<EFDataContext, int, Task<bool>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Any());
    //private ILoggerFactory? _logFactory;
    public SqliteBenchmarkAny(bool withLogging = false)
    {
        SetupNext(withLogging);

        SetupEF(withLogging);

        SetupDapper();
    }

    private void SetupNext(bool withLogging)
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(GetDatabasePath());
        if (withLogging)
        {
            var logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(logFactory);
            builder.LogSensitiveData(true);
        }
        _ctx = new TestDataRepository(builder.CreateDbContext());

        _cmd = _ctx.SimpleEntity.AnyCommand().Compile(true);

        _ctx.DbContext.EnsureConnectionOpen();
    }

    private static string GetDatabasePath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
    }

    private void SetupDapper()
    {
        var connString = $"Data Source='{GetDatabasePath()}'";
        _conn = new SqliteConnection(connString);
        _conn.Open();
    }

    private void SetupEF(bool withLogging)
    {
        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={GetDatabasePath()}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            var logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            efBuilder.UseLoggerFactory(logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Any());
    }
    // [GlobalSetup(Targets = new[] { nameof(NextormCached) })]
    // public void PrepareNext()
    // {
    //     SetupNext(false);
    // }
    // [GlobalSetup(Targets = new[] { nameof(NextormCompiled) })]
    // public void CompileQueries()
    // {
    //     SetupNext(false);
    //     _cmd = _ctx.SimpleEntity.AnyCommand().Compile(true);
    //     // _cmdFilter = _ctx.SimpleEntity.Where(it => it.Id > 5).AnyCommand().Compile(true);
    //     // _cmdFilterParam = _ctx.SimpleEntity.Where(it => it.Id > NORM.Param<int>(0)).AnyCommand().Compile(true);
    // }
    // [GlobalSetup(Targets = new[] { nameof(EFCoreCompiled) })]
    // public void CompileEFQueries()
    // {
    //     SetupEF(false);
    //     _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Any());
    // }
    // [GlobalSetup(Targets = new[] { nameof(EFCore) })]
    // public void PrepareEFCore()
    // {
    //     SetupEF(false);
    // }
    // [GlobalSetup(Targets = new[] { nameof(Dapper) })]
    // public void PrepareDapper()
    // {
    //     SetupDapper();
    // }
    [Benchmark()]
    public async Task NextormCompiled()
    {
        await _cmd.AnyAsync();
    }
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
    // [Benchmark()]
    // [BenchmarkCategory("Filter")]
    // public async Task NextormFilterCompiled()
    // {
    //     await _cmdFilter.AnyAsync();
    // }
    // [Benchmark()]
    // [BenchmarkCategory("Filter")]
    // public async Task NextormFilterCached()
    // {
    //     await _ctx.SimpleEntity.Where(it => it.Id > 5).AnyAsync();
    // }
    // [Benchmark]
    // [BenchmarkCategory("Filter")]
    // public async Task EFCoreFilter()
    // {
    //     await _efCtx.SimpleEntities.Where(it => it.Id > 5).AnyAsync();
    // }
    // [Benchmark]
    // [BenchmarkCategory("Filter")]
    // public async Task EFCoreFilterCompiled()
    // {
    //     await _efCompiledFilter(_efCtx);
    // }
    // [Benchmark]
    // [BenchmarkCategory("Filter")]
    // public async Task DapperFilter()
    // {
    //     await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity where id > 5)");
    // }
    // [Benchmark()]
    // [BenchmarkCategory("FilterParam")]
    // public async Task NextormFilterParamCompiled()
    // {
    //     await _cmdFilterParam.AnyAsync(5);
    // }
    // [Benchmark()]
    // [BenchmarkCategory("FilterParam")]
    // public async Task NextormFilterParamCached()
    // {
    //     await _ctx.SimpleEntity.Where(it => it.Id > NORM.Param<int>(0)).AnyAsync(5);
    // }
    // [Benchmark]
    // [BenchmarkCategory("FilterParam")]
    // public async Task EFCoreFilterParam()
    // {
    //     var id = 5;
    //     await _efCtx.SimpleEntities.Where(it => it.Id > id).AnyAsync();
    // }
    // [Benchmark]
    // [BenchmarkCategory("FilterParam")]
    // public async Task EFCoreFilterParamCompiled()
    // {
    //     await _efCompiledFilterParam(_efCtx, 5);
    // }
    // [Benchmark]
    // [BenchmarkCategory("FilterParam")]
    // public async Task DapperFilterParam()
    // {
    //     await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity where id > @id)", new { id = 5 });
    // }
}
