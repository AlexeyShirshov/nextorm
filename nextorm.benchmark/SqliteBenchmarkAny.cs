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
    const int Iterations = 100;
    private IDataContext _db;
    private TestDataRepository _ctx;
    private IPreparedQueryCommand<bool> _cmd;
    // private QueryCommand<bool> _cmdFilter;
    // private QueryCommand<bool> _cmdFilterParam;
    private EFDataContext _efCtx;
    private SqliteConnection _conn;
    private Func<EFDataContext, int, Task<bool>> _efCompiled;
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
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);

        _cmd = _ctx.SimpleEntity.Where(e => e.Id == NORM.Param<int>(0)).AnyCommand().Prepare(true);

        _db.EnsureConnectionOpen();
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

        _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.SimpleEntities.Any(e => e.Id == i));
    }
    // [GlobalSetup(Targets = new[] { nameof(NextormCached) })]
    // public void PrepareNext()
    // {
    //     SetupNext(false);
    // }
    // [GlobalSetup(Targets = new[] { nameof(NextormPrepared) })]
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
    public async Task Nextorm_Prepared()
    {
        for (var i = 0; i < Iterations; i++)
            await _db.AnyAsync(_cmd, i);
    }
    [Benchmark()]
    public async Task Nextorm_Cached()
    {
        for (var i = 0; i < Iterations; i++)
            await _ctx.SimpleEntity.Where(e => e.Id == i).AnyAsync();
    }
    [Benchmark]
    public async Task EFCore()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.SimpleEntities.AnyAsync(e => e.Id == i);
    }
    [Benchmark]
    public async Task EFCore_Compiled()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCompiled(_efCtx, i);
    }
    [Benchmark]
    public async Task Dapper()
    {
        for (var i = 0; i < Iterations; i++)
            await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity where id = @id)", new { id = i });
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
