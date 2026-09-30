using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Benchmark;

/// <summary>
/// Focused, non-acceptance benchmark for task #123 (global-filter isolation of the MERGE/UPSERT target).
/// It measures the write paths the task touches, before and after the filter is injected into the
/// rendered statement:
/// <list type="bullet">
/// <item>a full-<c>MERGE</c> SQL build over a target with an active global filter (the D2/D3 seam);</item>
/// <item>the same full-<c>MERGE</c> build with <c>IgnoreFilters()</c> (the opt-out path);</item>
/// <item>an in-memory key upsert with no filter (the untouched baseline);</item>
/// <item>an in-memory key upsert over a target with an active filter — the refusal path (D4).</item>
/// </list>
/// SQL Server is used only to render SQL: the context carries a placeholder connection string and the
/// benchmark never opens a connection. The in-memory arms use <see cref="InMemoryDataContext"/>.
/// <para>
/// Pre-fix semantics (this file is added before the fix): the filtered full-<c>MERGE</c> arm renders the
/// statement with the filter <b>ignored</b> (the gap #123 closes), and the in-memory active-filter arm
/// runs the (still-permitted) upsert after its incoming-row validation. Post-fix the first arm gains the
/// <c>target</c>-qualified predicate and the last one throws before touching data, so the arm is wrapped
/// in a catch to keep the benchmark runnable on both trees.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("merge-filter")]
[Config(typeof(NextormConfig))]
public class MergeTargetFilterBenchmark
{
    private const int Iterations = 100;
    private const string TenantKey = "merge_bench_tenant";
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private readonly IDataContext _sqlServer;
    private readonly IDataContext _inMemory;
    private readonly IDataContext _inMemoryFiltered;
    private readonly MergeFilterBenchEntity _filteredRow = new() { Id = 1, TenantId = 1, Name = "a" };
    private readonly MergeNoFilterBenchEntity _plainRow = new() { Id = 1, Name = "a" };

    // Consumed to keep the JIT from eliminating the (otherwise unused) work.
    private int _sink;

    public MergeTargetFilterBenchmark()
    {
        _sqlServer = new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder());
        _sqlServer.Properties[TenantKey] = 1;
        // Filters are registered in the process-wide metadata cache, so this single registration also
        // applies to the in-memory context below (same entity type).
        _sqlServer.From<MergeFilterBenchEntity>(b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]));

        _inMemory = new InMemoryDataContext();
        _inMemory.From<MergeNoFilterBenchEntity>().WithData(new List<MergeNoFilterBenchEntity>());

        _inMemoryFiltered = new InMemoryDataContext();
        _inMemoryFiltered.Properties[TenantKey] = 1;
        _inMemoryFiltered.From<MergeFilterBenchEntity>().WithData(new List<MergeFilterBenchEntity>());
    }

    // Full-MERGE (branch form) SQL build over a filtered target: the D2/D3 injection seam.
    [Benchmark]
    public int FullMerge_Filtered_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _sqlServer.MergeInto<MergeFilterBenchEntity>()
                .Using(_filteredRow)
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .ToSql().Length;
        }

        return _sink = len;
    }

    // Same build with every target filter disabled: the IgnoreFilters opt-out must stay allocation-neutral.
    [Benchmark]
    public int FullMerge_IgnoreFilters_SqlBuild()
    {
        var len = 0;
        for (var i = 0; i < Iterations; i++)
        {
            len = _sqlServer.MergeInto<MergeFilterBenchEntity>()
                .Using(_filteredRow)
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .IgnoreFilters()
                .ToSql().Length;
        }

        return _sink = len;
    }

    // In-memory key upsert with no registered filter: the baseline the fix must not regress.
    [Benchmark(Baseline = true)]
    public int InMemory_NoFilter()
    {
        var affected = 0;
        for (var i = 0; i < Iterations; i++)
            affected = _inMemory.MergeInto<MergeNoFilterBenchEntity>()
                .Using(_plainRow)
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Merge();

        return _sink = affected;
    }

    // In-memory key upsert over a target with an active filter: pre-fix this is the current (permitted)
    // upsert; post-fix D4 makes it refuse with NotSupportedException before touching data. The catch keeps
    // the arm runnable on both trees and measures the guard path either way.
    [Benchmark]
    public int InMemory_ActiveFilter()
    {
        var affected = 0;
        for (var i = 0; i < Iterations; i++)
        {
            try
            {
                affected = _inMemoryFiltered.MergeInto<MergeFilterBenchEntity>()
                    .Using(_filteredRow)
                    .OnKeys()
                    .WhenMatchedUpdate()
                    .WhenNotMatchedInsert()
                    .Merge();
            }
            catch (Exception)
            {
                affected = -1;
            }
        }

        return _sink = affected;
    }
}

[SqlTable("merge_filter_bench")]
public sealed class MergeFilterBenchEntity
{
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("merge_no_filter_bench")]
public sealed class MergeNoFilterBenchEntity
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
