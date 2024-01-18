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
public class SqliteBenchmarkSingle
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<int> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    // private readonly Func<EFDataContext, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id == 1).Select(it => it.Id).Single());
    private readonly Func<EFDataContext, int, Task<int>> _efCompiledSingleOrDefault = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).SingleOrDefault());
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
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _cmd = _ctx.SimpleEntity.Where(it => it.Id == 1).SingleOrSingleOrDefaultCommand(it => it.Id).Prepare();

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
    public async Task Nextorm_Prepared_SingleOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _cmd.SingleOrDefaultAsync(_db, i);
    }
    [Benchmark()]
    public async Task Nextorm_Cached_SingleOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _ctx.SimpleEntity.Where(it => it.Id == i).Select(it => it.Id).SingleOrDefaultAsync();
    }
    [Benchmark]
    public async Task EFCore_SingleOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _efCtx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).SingleOrDefaultAsync();
    }
    [Benchmark]
    public async Task EFCore_Compiled_SingleOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _efCompiledSingleOrDefault(_efCtx, i);
    }
    [Benchmark]
    public async Task Dapper_SingleOrDefault()
    {
        for (int i = 0; i < 10; i++)
            await _conn.QuerySingleOrDefaultAsync<int?>("select id from simple_entity where id = @id", new { id = i });
    }
}
