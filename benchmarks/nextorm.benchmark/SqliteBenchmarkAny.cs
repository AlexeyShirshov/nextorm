using BenchmarkDotNet.Attributes;
using NextORM.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using LinqToDB;
using IDataContext = NextORM.Core.IDataContext;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using NextORM.Core;

namespace NextORM.Benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkAny
{
    const int Iterations = 100;
    private IDataContext _db = null!;
    private TestDataRepository _ctx = null!;
    private IPreparedQueryCommand<bool> _cmd = null!;
    // private QueryCommand<bool> _cmdFilter;
    // private QueryCommand<bool> _cmdFilterParam;
    private EFDataContext _efCtx = null!;
    private SqliteConnection _conn = null!;
    private Func<EFDataContext, int, Task<bool>> _efCompiled = null!;
    private Linq2DbDataRepository _linq2Db;
    // private readonly Func<EFDataContext, Task<bool>> _efCompiledFilter = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.SimpleEntities.Where(it => it.Id > 5).Any());
    // private readonly Func<EFDataContext, int, Task<bool>> _efCompiledFilterParam = EF.CompileAsyncQuery((EFDataContext ctx, int id) => ctx.SimpleEntities.Where(it => it.Id > id).Any());
    //private ILoggerFactory? _logFactory;
    public SqliteBenchmarkAny() : this(false) { }
    public SqliteBenchmarkAny(bool withLogging = false)
    {
        SetupNext(withLogging);

        SetupEF(withLogging);

        SetupDapper();

        _linq2Db = new Linq2DbDataRepository();
    }

    private void SetupNext(bool withLogging)
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(GetDatabasePath());
        if (withLogging)
        {
            var logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);

        _cmd = _ctx.SimpleEntity.Where(e => e.Id == SqlFunctions.Parameter<int>(0)).AnyCommand().Prepare(true);

        ((IConnectionManager)_db).EnsureConnectionOpen();
    }

    private static string GetDatabasePath()
    {
        return BenchDb.FilePath;
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
    //     // _cmdFilterParam = _ctx.SimpleEntity.Where(it => it.Id > SqlFunctions.Parameter<int>(0)).AnyCommand().Compile(true);
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
    [Benchmark]
    public async Task Linq2Db()
    {
        for (var i = 0; i < Iterations; i++)
            await _linq2Db.AnyAsync(i);
    }
    [Benchmark]
    public long Linq2Db_Compiled()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            if (_l2dbAny(_linq2Db.Db, i)) n++;
        _sink = n;
        return n;
    }

    private long _sink;
    private static readonly Func<LinqToDB.IDataContext, int, bool> _l2dbAny = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db, int id) => db.GetTable<Linq2DbSimpleEntity>().Any(it => it.Id == id));

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
    //     await _ctx.SimpleEntity.Where(it => it.Id > SqlFunctions.Parameter<int>(0)).AnyAsync(5);
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
