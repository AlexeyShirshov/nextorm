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
public class SqliteBenchmarkFirst
{
    private readonly TestDataRepository _ctx;
    private readonly QueryCommand<int> _cmd;
    private readonly QueryCommand<LargeEntity?> _cmdEnt;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Select(it => it.Id).First());
    private readonly Func<EFDataContext, Task<LargeEntity>> _efLargeCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities.First());
    private readonly Func<EFDataContext, Task<int>> _efCompiledFirstOrDefault = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Select(it => it.Id).FirstOrDefault());
    // private readonly Func<EFDataContext, int, Task<int>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Select(it => it.Id).First());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkFirst(bool withLogging = false)
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

        _cmd = _ctx.SimpleEntity.FirstOrFirstOrDefaultCommand(it => it.Id).Compile(true);
        _cmdEnt = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).FirstOrFirstOrDefaultCommand().Compile(true);

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
    // [Benchmark(Baseline = true)]
    // public async Task NextormFirstCompiled()
    // {
    //     await _cmd.FirstAsync();
    // }
    [Benchmark()]
    public async Task NextormLargeFirstCompiled()
    {
        await _cmdEnt.FirstAsync(1);
    }
    // [Benchmark()]
    // public async Task NextormFirstOrDefaultCompiled()
    // {
    //     await _cmd.FirstOrDefaultAsync();
    // }
    [Benchmark()]
    public async Task NextormFirstCached()
    {
        await _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).FirstAsync(1);
    }
    // [Benchmark()]
    // public async Task NextormFirstOrDefaultCached()
    // {
    //     await _ctx.SimpleEntity.Select(it => it.Id).FirstOrDefaultAsync();
    // }
    // [Benchmark]
    // public async Task EFCoreFirst()
    // {
    //     await _efCtx.SimpleEntities.Select(it => it.Id).FirstAsync();
    // }
    // [Benchmark]
    // public async Task EFCoreFirstOrDefault()
    // {
    //     await _efCtx.SimpleEntities.Select(it => it.Id).FirstOrDefaultAsync();
    // }
    // [Benchmark]
    // public async Task EFCoreFirstCompiled()
    // {
    //     await _efCompiled(_efCtx);
    // }
    [Benchmark]
    public async Task EFCoreLargeFirstCompiled()
    {
        await _efLargeCompiled(_efCtx);
    }
    // [Benchmark]
    // public async Task EFCoreFirstOrDefaultCompiled()
    // {
    //     await _efCompiledFirstOrDefault(_efCtx);
    // }
    // [Benchmark]
    // public async Task DapperFirst()
    // {
    //     await _conn.QueryFirstAsync<int>("select id from simple_entity limit 1");
    // }
    [Benchmark]
    public async Task DapperLargeFirst()
    {
        await _conn.QueryFirstAsync<LargeEntity>("select id, someString as str, dt from large_table where id = @id", new { id = 1 });
    }
    // [Benchmark]
    // public async Task DapperFirstOrDefault()
    // {
    //     await _conn.QueryFirstOrDefaultAsync<int?>("select id from simple_entity limit 1");
    // }
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
