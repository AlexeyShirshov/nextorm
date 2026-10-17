using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Measures how much of one CSV export is paid before the first row — the fixed per-call cost — as
/// opposed to the per-row cost. The CSV terminal prepares with <c>storeInCache: false</c> (the query is
/// re-planned on every invocation) and compiles the per-column plan on every invocation, so a repeated
/// small export pays that work each time. The <see cref="Rows"/> sweep lets the intercept of a linear
/// fit be read off directly; the 1- and 8-column writes give the per-column share of that intercept
/// (the slope over column count), while the warmed <c>ToList</c> arm is the reference for a command
/// whose plan and materializer come from the cache.
/// </summary>
/// <remarks>
/// Comparison base: <see cref="SqliteBenchmarkCsv"/> (row-heavy, fixed 10 000 rows). This class is
/// deliberately small-row heavy and tagged <c>csv-fixed</c> only, so the frozen perf category is not
/// touched.
/// </remarks>
[Config(typeof(NextormConfig))]
[BenchmarkCategory("csv-fixed")]
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
public class SqliteBenchmarkCsvFixedCost
{
    // Small row counts make the intercept of T(N) = fixed + N * perRow readable; the 10 000 arm is the
    // row-heavy reference. Note Limit(0) means "no limit", so there is no zero-row point: from the
    // smallest N the residual is per-row formatting, not plan work.
    [Params(1, 10, 100, 1000, 10000)]
    public int Rows { get; set; }

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly MemoryStream _destination = new(1 << 18);
    private long _sink;

    public SqliteBenchmarkCsvFixedCost()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Warm the plan cache and the compiled materializer of the reference arm, so its measured
        // invocations are pure execution: unlike WriteCsv it does not re-plan per call.
        _ctx.LargeEntity.Limit(10_000).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
    }

    private void ResetDestination()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
    }

    /// <summary>Reference: a warmed buffered read of the three-column projection (no CSV, no re-plan).</summary>
    [Benchmark(Baseline = true)]
    public void Nextorm_ToList_3Col()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
        _sink = rows.Count;
    }

    /// <summary>The realistic shape: one <c>WriteCsv</c> call over a three-column projection.</summary>
    [Benchmark]
    public void Nextorm_WriteCsv_3Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).WriteCsv(_destination, null, CancellationToken.None);
        _sink = _destination.Length;
    }

    /// <summary>Single-column write: the low end of the per-column cost slope.</summary>
    [Benchmark]
    public void Nextorm_WriteCsv_1Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).WriteCsv(_destination, null, CancellationToken.None);
        _sink = _destination.Length;
    }

    /// <summary>Eight-column write: the high end of the per-column cost slope.</summary>
    [Benchmark]
    public void Nextorm_WriteCsv_8Col()
    {
        ResetDestination();
        _ctx.LargeEntity
            .Limit(Rows)
            .Select(it => new
            {
                it.Id,
                S1 = it.Str,
                D1 = it.Dt,
                S2 = it.Str,
                D2 = it.Dt,
                S3 = it.Str,
                D3 = it.Dt,
                D4 = it.Dt,
            })
            .WriteCsv(_destination, null, CancellationToken.None);
        _sink = _destination.Length;
    }
}
