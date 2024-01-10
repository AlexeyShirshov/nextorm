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
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCache
{
    private readonly SQLiteConnection _conn;
    private readonly DbContextBuilder _builder;
    private readonly ILoggerFactory? _logFactory;
    private readonly DbContextOptionsBuilder<EFDataContext> _efBuilder;

    public SqliteBenchmarkCache(bool withLogging = false)
    {
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        _conn = new SQLiteConnection($"Data Source='{filepath}'");
        _builder = new DbContextBuilder();
        _builder.UseSqlite(_conn);
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            _builder.UseLoggerFactory(_logFactory);
            _builder.LogSensitiveData(true);
        }

        _efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        _efBuilder.UseSqlite(_conn);
        _efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            _efBuilder.UseLoggerFactory(_logFactory);
            _efBuilder.EnableSensitiveDataLogging(true);
        }

        _conn.Open();
    }
    [Benchmark()]
    public void NextormCached()
    {
        using var ctx = _builder.CreateDbContext();
        var repo = new TestDataRepository(ctx);
        repo.LargeEntity.Where(it => it.Id == 1).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
    }
    [Benchmark()]
    public void NextormNonCached()
    {
        using var ctx = _builder.CreateDbContext();
        var repo = new TestDataRepository(ctx);
        var cmd = repo.LargeEntity.Where(it => it.Id == 1).Select(it => new { it.Id, it.Str, it.Dt });
        cmd.Cache = false;
        cmd.ToList();
    }
    [Benchmark()]
    public void EFcore()
    {
        using var ctx = new EFDataContext(_efBuilder.Options);
        var cmd = ctx.LargeEntities.Where(it => it.Id == 1).Select(it => new { it.Id, it.Str, it.Dt });
        var _ = cmd.ToList();
        // pseudo code
        //Microsoft.EntityFrameworkCore.DbContext.ClearCache();
    }
    [Benchmark()]
    public void Dapper()
    {
        _conn.Query<LargeEntity>("select id, someString as str, dt from large_table where id=@id", new { id = 1 });
        SqlMapper.PurgeQueryCache();
    }
}
