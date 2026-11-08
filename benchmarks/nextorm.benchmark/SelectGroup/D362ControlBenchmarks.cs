using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// No-component control arms for the D362 performance protocol. They use the same fixture, the same
/// cold/warm bodies and the same config as the operator arms; only the operator invocation differs.
/// Each arm has its own <see cref="BenchmarkAttribute"/> so it can be A/A-paired independently.
/// </summary>
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("d362-control", "d362-control-groupby")]
public class D362ControlGroupByBenchmarks
{
    private SqliteDataContext _ctx = null!;
    private const string DbPath = "/tmp/nextorm.d362.control-groupby.db";

    [GlobalSetup]
    public void Setup()
    {
        D362ControlFixture.Seed(DbPath);
        _ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        _ctx.PurgeQueryCache();
    }

    [Benchmark]
    public void Prepare_Cold_GroupBy()
    {
        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        var cmd = ctx.From<D362BenchRow>().GroupBy(x => x.Category)
            .Select(x => new { x.Category, Total = x.Score });
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Warm_Hit_GroupBy()
    {
        var cmd = _ctx.From<D362BenchRow>().GroupBy(x => x.Category)
            .Select(x => new { x.Category, Total = x.Score });
        _ = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public int Execute_GroupBy()
        => _ctx.From<D362BenchRow>().GroupBy(x => x.Category)
            .Select(x => new { x.Category, Total = x.Score }).ToList().Count;
}

/// <summary>Extrema control arms (the shipped <c>SelectWhereMin</c>/<c>SelectWhereMax</c> components).</summary>
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("d362-control", "d362-control-extrema")]
public class D362ControlExtremaBenchmarks
{
    private SqliteDataContext _ctx = null!;
    private const string DbPath = "/tmp/nextorm.d362.control-extrema.db";

    [GlobalSetup]
    public void Setup()
    {
        D362ControlFixture.Seed(DbPath);
        _ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        _ctx.PurgeQueryCache();
    }

    [Benchmark]
    public void Prepare_Cold_SelectWhereMin()
    {
        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        var cmd = ExtremeRowExtensions.SelectWhereMin(
            ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score });
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Warm_Hit_SelectWhereMin()
    {
        var cmd = ExtremeRowExtensions.SelectWhereMin(
            _ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score });
        _ = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Prepare_Cold_SelectWhereMax()
    {
        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());
        var cmd = ExtremeRowExtensions.SelectWhereMax(
            ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score });
        _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public void Warm_Hit_SelectWhereMax()
    {
        var cmd = ExtremeRowExtensions.SelectWhereMax(
            _ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score });
        _ = _ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
    }

    [Benchmark]
    public int Execute_SelectWhereMin()
        => ExtremeRowExtensions.SelectWhereMin(
            _ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score }).ToList().Count;

    [Benchmark]
    public int Execute_SelectWhereMax()
        => ExtremeRowExtensions.SelectWhereMax(
            _ctx.From<D362BenchRow>(), x => x.Score, x => new { x.Id, x.Score }).ToList().Count;
}

/// <summary>The shared control fixture: the same deterministic table as the operator arms.</summary>
internal static class D362ControlFixture
{
    internal static void Seed(string path)
    {
        using var conn = new SqliteConnection($"Data Source='{path}'");
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
}
