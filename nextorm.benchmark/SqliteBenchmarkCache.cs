using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.SQLite;
using Dapper;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Jobs;
using OneOf.Types;
using nextorm.core;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace nextorm.benchmark;

//[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkCache
{
    private readonly DbContextBuilder _builder;
    public SqliteBenchmarkCache(bool withLogging = false)
    {
        var filepath = Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
        var conn = new SQLiteConnection($"Data Source='{filepath}'");
        _builder = new DbContextBuilder();
        _builder.UseSqlite(conn);
        if (withLogging)
        {
            var logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Trace));
            _builder.UseLoggerFactory(logFactory);
            _builder.LogSensetiveData(true);
        }
        conn.Open();
    }
    [Benchmark()]
    public void Cached()
    {
        using var ctx = _builder.CreateDbContext();
        var repo = new TestDataRepository(ctx);
        repo.LargeEntity.Where(it => it.Id == 1).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
    }
    [Benchmark()]
    public void NonCached()
    {
        using var ctx = _builder.CreateDbContext();
        var repo = new TestDataRepository(ctx);
        var cmd = repo.LargeEntity.Where(it => it.Id == 1).Select(it => new { it.Id, it.Str, it.Dt });
        cmd.Cache = false;
        cmd.ToList();
    }
}
