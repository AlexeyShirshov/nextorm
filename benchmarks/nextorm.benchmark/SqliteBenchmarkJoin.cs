using BenchmarkDotNet.Attributes;
using NextORM.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using NextORM.Core;

namespace NextORM.Benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkJoin
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    // private readonly IPreparedQueryCommand<int> _cmd;
    private readonly IPreparedQueryCommand<LargeEntity> _cmdEntPrepared;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    //private readonly Func<EFDataContext, int i, Task<int>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).First());
    private readonly Func<EFDataContext, int, IAsyncEnumerable<LargeEntity>> _efLargeCompiled = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.LargeEntities
        .Join(ctx.SimpleEntities, it => it.Id, it => it.Id, (l, s) => new LargeEntity { Id = l.Id, Dt = l.Dt, Str = l.Str }));
    private readonly Linq2DbDataRepository _linq2Db;
    // private readonly Func<EFDataContext, int, Task<int>> _efCompiledFirstOrDefault = EF.CompileAsyncQuery((EFDataContext ctx, int i) => ctx.SimpleEntities.Where(it => it.Id == i).Select(it => it.Id).FirstOrDefault());
    // private readonly Func<EFDataContext, int, Task<int>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Select(it => it.Id).First());
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkJoin() : this(false) { }
    public SqliteBenchmarkJoin(bool withLogging = false)
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _cmdEntPrepared = _ctx.LargeEntity
            .Join(_ctx.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Where(p => p.Item2.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str }).Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }
    // [Benchmark(Baseline = true)]
    // public async Task NextormFirstCompiled()
    // {
    //     await _cmd.FirstAsync();
    // }
    [Benchmark()]
    public async Task Nextorm_Prepared()
    {
        for (int i = 0; i < 10; i++)
            await _cmdEntPrepared.ToListAsync(_db, i);
    }
    [Benchmark()]
    public async Task Nextorm_Cached()
    {
        for (int i = 0; i < 10; i++)
            await _ctx.LargeEntity.Join(_ctx.SimpleEntity, (t1, t2) => t1.Id == t2.Id).Where(p => p.Item2.Id == i).Select(p => new { p.Item1.Id, p.Item1.Dt, p.Item1.Str }).ToListAsync();
    }
    [Benchmark]
    public async Task EFCore_Compiled()
    {
        for (int i = 0; i < 10; i++)
            await _efLargeCompiled(_efCtx, i).ToListAsync();
    }
    [Benchmark]
    public async Task EFCore()
    {
        for (int i = 0; i < 10; i++)
            await _efCtx.LargeEntities
                .Join(_efCtx.SimpleEntities, it => it.Id, it => it.Id, (l, s) => new LargeEntity { Id = l.Id, Dt = l.Dt, Str = l.Str }).ToListAsync();
    }
    // [Benchmark]
    // public async Task DapperFirst()
    // {
    //     await _conn.QueryFirstAsync<int>("select id from simple_entity limit 1");
    // }
    [Benchmark]
    public async Task Dapper()
    {
        for (int i = 0; i < 10; i++)
            await _conn.QueryAsync<LargeEntity>("select t1.id, someString as str, dt from large_table t1 join simple_entity t2 on t1.id = t2.id where t2.id = @id limit 1", new { id = i });
    }
    [Benchmark]
    public async Task Linq2Db()
    {
        for (int i = 0; i < 10; i++)
            await _linq2Db.JoinAsync(i);
    }
}
