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
public class SqliteBenchmarkFirst
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<int> _cmd;
    private readonly IPreparedQueryCommand<LargeEntity?> _cmdEntCompiled;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Func<EFDataContext, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Select(it => it.Id).First());
    private readonly Func<EFDataContext, int, Task<LargeEntity>> _efLargeCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.LargeEntities.FirstOrDefault(it => it.Id == i));
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
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.FirstOrFirstOrDefaultCommand(it => it.Id).Compile(true);
        _cmdEntCompiled = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).FirstOrFirstOrDefaultCommand().Compile(true);

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
        for (int i = 0; i < 10; i++)
            await _db.FirstOrDefaultAsync(_cmdEntCompiled, i);
    }
    // [Benchmark()]
    // public async Task NextormFirstOrDefaultCompiled()
    // {
    //     await _cmd.FirstOrDefaultAsync();
    // }
    [Benchmark()]
    public async Task NextormFirstParam()
    {
        var cmdEnt = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0));
        for (int i = 0; i < 10; i++)
            await cmdEnt.FirstOrDefaultAsync(i);
    }
    [Benchmark()]
    public async Task NextormFirst()
    {
        for (int i = 0; i < 10; i++)
            await _ctx.LargeEntity.Where(it => it.Id == i).FirstOrDefaultAsync();
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
        for (int i = 0; i < 10; i++)
            await _efLargeCompiled(_efCtx, i);
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
        for (int i = 0; i < 10; i++)
            await _conn.QueryFirstOrDefaultAsync<LargeEntity>("select id, someString as str, dt from large_table where id = @id", new { id = i });
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
