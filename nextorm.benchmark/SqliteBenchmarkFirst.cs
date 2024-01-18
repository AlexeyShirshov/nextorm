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
    private readonly IPreparedQueryCommand<LargeEntity?> _cmdEntPrepared;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    //private readonly Func<EFDataContext, int i, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).First());
    private readonly Func<EFDataContext, int, Task<LargeEntity>> _efLargeCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.LargeEntities.FirstOrDefault(it => it.Id == i));
    private readonly Func<EFDataContext, int, Task<int>> _efCompiledFirstOrDefault = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).FirstOrDefault());
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

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == NORM.Param<int>(0)).FirstOrFirstOrDefaultCommand(it => it.Id).Prepare();
        _cmdEntPrepared = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).FirstOrFirstOrDefaultCommand().Prepare();

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
    public async Task Nextorm_Prepared_Entity_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _cmdEntPrepared.FirstOrDefaultAsync(_db, i);
    }
    [Benchmark()]
    public async Task Nextorm_Prepared_Scalar_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _cmd.FirstOrDefaultAsync(_db, i);
    }
    [Benchmark()]
    public async Task Nextorm_PreparedForLoop_Entity_FirstOrDefault()
    {
        var cmdEnt = _ctx.LargeEntity.Where(it => it.Id == NORM.Param<int>(0));
        for (int i = 0; i < 10; i++)
            await cmdEnt.FirstOrDefaultAsync(i);
    }
    [Benchmark()]
    public async Task Nextorm_Cached_Entity_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _ctx.LargeEntity.Where(it => it.Id == i).FirstOrDefaultAsync();
    }
    [Benchmark()]
    public async Task Nextorm_Cached_Scalar_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _ctx.SimpleEntity.Where(it => it.Id == i).Select(it => it.Id).FirstOrDefaultAsync();
    }
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
    public async Task EFCore_Compiled_Entity_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _efLargeCompiled(_efCtx, i);
    }
    [Benchmark]
    public async Task EFCore_Compiled_Scalar_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _efCompiledFirstOrDefault(_efCtx, i);
    }
    // [Benchmark]
    // public async Task DapperFirst()
    // {
    //     await _conn.QueryFirstAsync<int>("select id from simple_entity limit 1");
    // }
    [Benchmark]
    public async Task Dapper_Entity_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _conn.QueryFirstOrDefaultAsync<LargeEntity>("select id, someString as str, dt from large_table where id = @id limit 1", new { id = i });
    }
    [Benchmark]
    public async Task Dapper_Scalar_FirstOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _conn.QueryFirstOrDefaultAsync<int?>("select id from simple_entity where id = @id limit 1", new { id = i });
    }
}
