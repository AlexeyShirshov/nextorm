using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using OneOf.Types;

namespace nextorm.core.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkAny
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<bool> _cmd;
    private readonly QueryCommand<bool> _cmdFilter;
    private readonly QueryCommand<bool> _cmdFilterParam;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, Task<bool>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Any());
    private readonly Func<EFDataContext, Task<bool>> _efCompiledFilter = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id > 5).Any());
    private readonly Func<EFDataContext, int, Task<bool>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Any());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkAny(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        // _cmd = _ctx.SimpleEntity.AnyCommand().Compile(false);
        // _cmdFilter = _ctx.SimpleEntity.Where(it => it.Id > 5).AnyCommand().Compile(false);
        // _cmdFilterParam = _ctx.SimpleEntity.Where(it => it.Id > NORM.Param<int>(0)).AnyCommand().Compile(false);

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
    [Benchmark()]
    [BenchmarkCategory("Filter")]
    public async Task NextormFilterCompiled()
    {
        await _cmdFilter.AnyAsync();
    }
    [Benchmark()]
    [BenchmarkCategory("Filter")]
    public async Task NextormFilterCached()
    {
        await _ctx.SimpleEntity.Where(it => it.Id > 5).AnyAsync();
    }
    [Benchmark]
    [BenchmarkCategory("Filter")]
    public async Task EFCoreFilter()
    {
        await _efCtx.SimpleEntities.Where(it => it.Id > 5).AnyAsync();
    }
    [Benchmark]
    [BenchmarkCategory("Filter")]
    public async Task EFCoreFilterCompiled()
    {
        await _efCompiledFilter(_efCtx);
    }
    [Benchmark]
    [BenchmarkCategory("Filter")]
    public async Task DapperFilter()
    {
        await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity where id > 5)");
    }
    [Benchmark()]
    [BenchmarkCategory("FilterParam")]
    public async Task NextormFilterParamCompiled()
    {
        await _cmdFilterParam.AnyAsync(5);
    }
    [Benchmark()]
    [BenchmarkCategory("FilterParam")]
    public async Task NextormFilterParamCached()
    {
        await _ctx.SimpleEntity.Where(it => it.Id > NORM.Param<int>(0)).AnyAsync(5);
    }
    [Benchmark]
    [BenchmarkCategory("FilterParam")]
    public async Task EFCoreFilterParam()
    {
        var id = 5;
        await _efCtx.SimpleEntities.Where(it => it.Id > id).AnyAsync();
    }
    [Benchmark]
    [BenchmarkCategory("FilterParam")]
    public async Task EFCoreFilterParamCompiled()
    {
        await _efCompiledFilterParam(_efCtx, 5);
    }
    [Benchmark]
    [BenchmarkCategory("FilterParam")]
    public async Task DapperFilterParam()
    {
        await _conn.ExecuteScalarAsync<bool>("select exists(select id from simple_entity where id > @id)", new { id = 5 });
    }
}
