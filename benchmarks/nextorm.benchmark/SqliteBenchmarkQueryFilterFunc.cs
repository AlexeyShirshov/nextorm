using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Focused cached-path benchmark for a global query filter declared in the builder-function form
/// (<c>FilterFunc</c>). The cached arm is <b>not</b> "predicate/params only": every fresh command
/// re-invokes the builder function (reflection + expression rebuild) <b>before</b> the plan-cache lookup,
/// so the function is not memoized per plan and the cached arm pays that cost even on a cache hit. The
/// pair mirrors <see cref="SqliteBenchmarkCachedPlan"/>: both arms execute the same SQL against SQLite,
/// so the delta is the cached-query overhead (plan lookup + function re-invocation + parameter
/// re-extraction) over a prepared command.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkQueryFilterFunc
{
    private const int Iterations = 100;
    private const string TenantKey = "bench_filter_func_tenant";

    private readonly IDataContext _db;
    private readonly IPreparedQueryCommand<int> _prepared;
    private readonly EntityBuilder<FilterFuncBenchEntity> _filtered;

    public SqliteBenchmarkQueryFilterFunc()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();
        _db.Properties[TenantKey] = 0;

        _filtered = _db.From<FilterFuncBenchEntity>(b => b.HasQueryFilter(
            "tenant", (eb, c) => eb.Where(e => e.Id >= (int)c.Properties[TenantKey])));

        _prepared = _filtered.Select(it => it.Id).Prepare();

        // Warm the plan cache so the measured loops only exercise the prepared/cached paths.
        _ = _filtered.Select(it => it.Id).ToList();
        _ = _db.GetPreparedQueryCommand(_filtered.Select(it => it.Id), false, true, CancellationToken.None);
    }

    // Prepared command: no plan lookup and no parameter re-extraction.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("query-filter")]
    public int FuncFilter_Prepared_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _prepared.ToList(_db))
                sum += row;
        }

        return sum;
    }

    // Cached query: identical SQL execution, plus the builder-function re-invocation (reflection +
    // expression rebuild), plan lookup and ExtractParams on each run.
    // The fresh command re-invokes the func before the plan-cache lookup, so a cache hit does not
    // skip it — the cached arm is not "predicate/params only".
    [Benchmark]
    [BenchmarkCategory("query-filter")]
    public int FuncFilter_Cached_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _filtered.Select(it => it.Id).ToList())
                sum += row;
        }

        return sum;
    }
}

[SqlTable("simple_entity")]
public sealed class FilterFuncBenchEntity
{
    [Column("id")]
    public int Id { get; set; }
}
