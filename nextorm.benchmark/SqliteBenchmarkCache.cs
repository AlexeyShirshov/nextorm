using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.SQLite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using nextorm.core;

namespace nextorm.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 20)]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCache
{
    private readonly SQLiteConnection _conn;
    private readonly ILoggerFactory? _logFactory;
    private readonly TestDataRepository _repo;
    private readonly EFDataContext _efCtx;

    [Params(1, 3, 5, 10, 15, 20, 30)]
    public int Iterations { get; set; } = 1;
    public SqliteBenchmarkCache(bool withLogging = false)
    {
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        _conn = new SQLiteConnection($"Data Source='{filepath}'");
        var builder = new DbContextBuilder();
        builder.UseSqlite(_conn);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }

        var db = builder.CreateDbContext();
        _repo = new TestDataRepository(db);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(_conn);
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn.Open();
    }
    [Benchmark()]
    public void NextormCached()
    {
        _repo.DbContext.PurgeQueryCache();
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
        var cmd = _repo.LargeEntity.Where(it => it.Id == NORM.Param<int>(0)).Select(it => new LargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt }).Prepare();
        for (int i = 0; i < Iterations; i++)
        {
            cmd.ToList(_repo.DbContext);
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
}
