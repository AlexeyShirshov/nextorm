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
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCache
{
    private readonly SqliteConnection _conn;
    private readonly ILoggerFactory? _logFactory;
    private readonly TestDataRepository _repo;
    private readonly EFDataContext _efCtx;
    private readonly Linq2DbDataRepository _linq2Db;
    private readonly IPreparedQueryCommand<LargeEntity> _nextormPreparedCmd;

    [Params(1, 3, 5, 10, 15, 20, 30)]
    public int Iterations { get; set; } = 1;
    public SqliteBenchmarkCache() : this(false) { }
    public SqliteBenchmarkCache(bool withLogging = false)
    {
        var filepath = BenchDb.FilePath;
        _conn = new SqliteConnection($"Data Source='{filepath}'");
        var builder = new DataContextBuilder();
        builder.UseSqlite(_conn);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }

        var db = builder.CreateDataContext();
        _repo = new TestDataRepository(db);

        // Prepared once (real usage); the benchmark must not re-compile the plan per invocation.
        _nextormPreparedCmd = _repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => new LargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(_conn);
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }
        _efCtx = new EFDataContext(efBuilder.Options);

        _linq2Db = new Linq2DbDataRepository();

        _conn.Open();
    }
    [Benchmark()]
    public void NextormCached()
    {
        _repo.DataContext.PurgeQueryCache();
        for (int i = 0; i < Iterations; i++)
        {
            _repo.LargeEntity.Where(it => it.Id == i).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
        }
    }
    // [Benchmark()]
    // public void NextormNonCached()
    // {
    //     for (int i = 0; i < Iterations; i++)
    //     {
    //         var cmd = _repo.LargeEntity.Where(it => it.Id == i).Select(it => new { it.Id, it.Str, it.Dt });
    //         cmd.Cache = false;
    //         cmd.ToList();
    //     }
    // }
    [Benchmark()]
    public void NextormPrepared()
    {
        for (int i = 0; i < Iterations; i++)
        {
            _nextormPreparedCmd.ToList(_repo.DataContext, i);
        }
    }
    // [Benchmark()]
    // public void EFcore()
    // {
    //     for (int i = 0; i < Iterations; i++)
    //     {
    //         var cmd = _efCtx.LargeEntities.Where(it => it.Id == i).Select(it => new { it.Id, it.Str, it.Dt });
    //         _ = cmd.ToList();
    //     }
    // }
    [Benchmark()]
    public void Dapper()
    {
        SqlMapper.PurgeQueryCache();
        for (int i = 0; i < Iterations; i++)
        {
            _conn.Query<LargeEntity>("select id, someString as str, dt from large_table where id=@id", new { id = i });
        }
    }
    [Benchmark()]
    public void Linq2Db()
    {
        for (int i = 0; i < Iterations; i++)
        {
            _linq2Db.LargeByIdToList(i);
        }
    }
    [Benchmark()]
    public void Linq2Db_Compiled()
    {
        for (int i = 0; i < Iterations; i++)
        {
            _linq2Db.LargeByIdCompiled(i);
        }
    }
}
