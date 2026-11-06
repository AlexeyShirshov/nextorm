using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

[MemoryDiagnoser]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("d362")]
public class D362SelectGroupBenchmarks
{
    private SqliteDataContext _ctx = null!;
    private const string DbPath = "/tmp/nextorm.d362.bench.db";

    [GlobalSetup]
    public void Setup()
    {
        // Seed a real SQLite table (deterministic, fixed schema + 1000 rows).
        SeedDatabase();
        _ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        _ctx.PurgeQueryCache();
    }

    private static void SeedDatabase()
    {
        using var conn = new SqliteConnection($"Data Source='{DbPath}'");
        conn.Open();
        using var cmd = conn.CreateCommand();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("drop table if exists D362BenchRow;");
        sb.AppendLine("create table D362BenchRow (id integer not null primary key, score integer null, category text null);");
        sb.Append("insert into D362BenchRow (id, score, category) values ");
        for (var i = 0; i < 1000; i++)
        {
            var score = i % 50;
            var cat = "c" + (i % 10);
            sb.Append(i == 0 ? "" : ",").Append($"({i}, {score}, '{cat}')");
        }
        sb.AppendLine(";");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    [Benchmark]
    public int Construct()
    {
        var e = _ctx.From<D362BenchRow>();
        var cmd = e.SelectGroup(x => new { x.Category, Total = x.Score });
        return cmd is null ? 0 : 1;
    }

    [Benchmark]
    public void Prepare_Cold()
    {
        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        var e = ctx.From<D362BenchRow>();
        var cmd = e.SelectGroup(x => new { x.Category, Total = x.Score });
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Warm_RawHit()
    {
        var e = _ctx.From<D362BenchRow>();
        var cmd = e.SelectGroup(x => new { x.Category, Total = x.Score });
        _ = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Prepared_FallbackHit()
    {
        var e = _ctx.From<D362BenchRow>();
        var cmd = e.SelectGroup(x => new { x.Category, Total = x.Score, K = 999 });
        _ = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public int Execute_Keyed() => _ctx.From<D362BenchRow>().SelectGroup(x => new { x.Category, Total = x.Score }).ToList().Count;

    [Benchmark]
    public int Execute_CompositeKey() => _ctx.From<D362BenchRow>().SelectGroup(x => new { x.Category, x.Score }).ToList().Count;

    [Benchmark]
    public int Execute_GlobalAggregate_WithHaving() => _ctx.From<D362BenchRow>().SelectGroup(x => new { Total = SqlFunctions.Sql.sum(x.Score) }).ToList().Count;

    [Benchmark]
    public int Execute_ConstantProjection() => _ctx.From<D362BenchRow>().SelectGroup(x => 42).ToList().Count;

    [Benchmark]
    public int Execute_CapturedCondition()
    {
        int t = 5;
        return _ctx.From<D362BenchRow>().SelectGroupHaving(x => new { x.Category, Total = x.Score }, x => x.Score > t).ToList().Count;
    }

    [Benchmark]
    public int Execute_PositionalRebind()
    {
        int t = 7;
        return _ctx.From<D362BenchRow>().SelectGroupHaving(x => new { x.Category, Total = x.Score }, x => x.Score > t).ToList().Count;
    }
}

public class D362BenchRow
{
    public long Id { get; set; }
    public int? Score { get; set; }
    public string? Category { get; set; }
}
