using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using OneOf.Types;
using nextorm.core;

namespace nextorm.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkSingle
{
    private readonly TestDataRepository _ctx;
    private readonly QueryCommand<int> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id == 1).Select(it => it.Id).Single());
    private readonly Func<EFDataContext, Task<int>> _efCompiledSingleOrDefault = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id == 1).Select(it => it.Id).SingleOrDefault());
    // private readonly Func<EFDataContext, int, Task<int>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Select(it => it.Id).Single());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkSingle(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataRepository(builder.CreateDbContext());
        _ctx.DbContext.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == 1).SingleOrSingleOrDefault(it => it.Id).Compile(true);

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
    [Benchmark(Baseline = true)]
    public async Task NextormSingleCompiled()
    {
        await _cmd.SingleAsync();
    }
    [Benchmark()]
    public async Task NextormSingleOrDefaultCompiled()
    {
        await _cmd.SingleOrDefaultAsync();
    }
    [Benchmark()]
    public async Task NextormSingleCached()
    {
        await _ctx.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).SingleAsync();
    }
    [Benchmark()]
    public async Task NextormSingleOrDefaultCached()
    {
        await _ctx.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).SingleOrDefaultAsync();
    }
    [Benchmark]
    public async Task EFCoreSingle()
    {
        await _efCtx.SimpleEntities.Where(it => it.Id == 1).Select(it => it.Id).SingleAsync();
    }
    [Benchmark]
    public async Task EFCoreSingleOrDefault()
    {
        await _efCtx.SimpleEntities.Where(it => it.Id == 1).Select(it => it.Id).SingleOrDefaultAsync();
    }
    [Benchmark]
    public async Task EFCoreSingleCompiled()
    {
        await _efCompiled(_efCtx);
    }
    [Benchmark]
    public async Task EFCoreSingleOrDefaultCompiled()
    {
        await _efCompiledSingleOrDefault(_efCtx);
    }
    [Benchmark]
    public async Task DapperSingle()
    {
        await _conn.QuerySingleAsync<int>("select id from simple_entity where id = 1");
    }
    [Benchmark]
    public async Task DapperSingleOrDefault()
    {
        await _conn.QuerySingleOrDefaultAsync<int?>("select id from simple_entity where id = 1");
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
