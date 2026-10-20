using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Benchmark;

/// <summary>
/// Focused, non-acceptance benchmark for D169 (#169): query preparation/rendering of a joined
/// <c>DELETE</c>/<c>UPDATE</c> that carries a tables-in-scope hint. The acceptance suite does not
/// exercise joined DML, so this supplemental case measures the exact seam the fix touches:
/// <list type="bullet">
/// <item>SQL Server (structural <c>WITH (...)</c> on the target and joined tables);</item>
/// <item>PostgreSQL (inline <c>/*+ ... */</c> statement comment);</item>
/// <item>a hint-free arm per provider as the untouched baseline.</item>
/// </list>
/// Both contexts carry placeholder connection strings and render SQL only; no connection is opened.
/// The benchmark deliberately carries no <c>acceptance</c> category, so it can never perturb the
/// acceptance perf gate.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("dml-scope-hint")]
[Config(typeof(NextormConfig))]
public class DmlScopeHintBenchmark
{
    private const int Iterations = 100;
    private const string SqlServerPlaceholder =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";
    private const string PostgresPlaceholder =
        "Host=localhost;Database=nextorm;Username=nextorm;Password=nextorm";

    private readonly IDataContext _sqlServer;
    private readonly IDataContext _postgres;

    // Consumed to keep the JIT from eliminating the (otherwise unused) work.
    private int _sink;

    public DmlScopeHintBenchmark()
    {
        _sqlServer = new SqlServerDataContext(SqlServerPlaceholder, new DataContextBuilder());
        _postgres = new PostgresDataContext(PostgresPlaceholder, new DataContextBuilder());
    }

    [Benchmark(Baseline = true)]
    public int SqlServer_DeleteJoin_NoHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _sqlServer.From<DmlHintTarget>()
                .Join(_sqlServer.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .ToSql().Length;
        }

        return _sink = len;
    }

    [Benchmark]
    public int SqlServer_DeleteJoin_ScopeHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _sqlServer.From<DmlHintTarget>()
                .WithTablesInScopeHint("nolock")
                .Join(_sqlServer.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .ToSql().Length;
        }

        return _sink = len;
    }

    [Benchmark]
    public int SqlServer_UpdateJoin_ScopeHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _sqlServer.From<DmlHintTarget>()
                .WithTablesInScopeHint("updlock")
                .Join(_sqlServer.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, "x")
                .ToSql().Length;
        }

        return _sink = len;
    }

    [Benchmark]
    public int Postgres_DeleteJoin_ScopeHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _postgres.From<DmlHintTarget>()
                .WithTablesInScopeHint("SeqScan(t1)")
                .Join(_postgres.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .ToSql().Length;
        }

        return _sink = len;
    }

    [Benchmark]
    public int Postgres_UpdateJoin_NoHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _postgres.From<DmlHintTarget>()
                .Join(_postgres.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, "x")
                .ToSql().Length;
        }

        return _sink = len;
    }

    [Benchmark]
    public int Postgres_UpdateJoin_ScopeHint_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _postgres.From<DmlHintTarget>()
                .WithTablesInScopeHint("SeqScan(t1)")
                .Join(_postgres.From<DmlHintJoin>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, "x")
                .ToSql().Length;
        }

        return _sink = len;
    }
}

[SqlTable("dml_hint_target")]
public sealed class DmlHintTarget
{
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("dml_hint_join")]
public sealed class DmlHintJoin
{
    [Column("id")]
    public long Id { get; set; }
}
