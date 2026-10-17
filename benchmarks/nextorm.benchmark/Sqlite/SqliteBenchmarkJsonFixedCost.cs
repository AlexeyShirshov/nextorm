using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Mirror of <see cref="SqliteBenchmarkCsvFixedCost"/> for the JSON terminal: how much of one
/// <c>WriteJson</c> call is paid before the first row. <c>PrepareJsonStream</c> clones the command,
/// prepares it with <c>storeInCache: false</c>, builds a fresh <c>JsonShapePlan</c> and compiles a new row writer on every invocation, so a repeated small export pays that work each time.
/// The <see cref="Rows"/> sweep makes the intercept of <c>T(N) = fixed + N * perRow</c> readable; the
/// 1-/3-/8-column writes give the per-column share of that intercept.
/// </summary>
/// <remarks>
/// The warmed <c>ToList</c> arm is the reference for a command whose plan and materializer come from the
/// cache. Tagged <c>json-fixed</c> only, so the frozen perf category is not touched.
/// </remarks>
[Config(typeof(NextormConfig))]
[BenchmarkCategory("json-fixed")]
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
public class SqliteBenchmarkJsonFixedCost
{
    // Limit(0) means "no limit", hence the sweep starts at one row; the 10 000 arm is the row-heavy
    // reference.
    [Params(1, 10, 100, 1000, 10000)]
    public int Rows { get; set; }

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly MemoryStream _destination = new(1 << 18);
    private long _sink;

    public SqliteBenchmarkJsonFixedCost()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Warm the plan cache and compiled materializer of the reference arm: unlike WriteJson it does
        // not re-plan and does not compile a writer per call.
        _ctx.LargeEntity.Limit(10_000).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
    }

    private void ResetDestination()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
    }

    /// <summary>Reference: a warmed buffered read of the three-column projection (no JSON, no re-plan).</summary>
    [Benchmark(Baseline = true)]
    public void Nextorm_ToList_3Col()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
        _sink = rows.Count;
    }

    /// <summary>The realistic shape: one <c>WriteJson</c> call over a three-column projection.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_3Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>Single-column scalar write: a different JSON shape (a bare value, not an object).</summary>
    [Benchmark]
    public void Nextorm_WriteJson_1Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>One-member object with a string: object framing plus one string column.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_ObjectStr()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Str }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>One-member object with a numeric: object framing plus one numeric column (provider-aware read).</summary>
    [Benchmark]
    public void Nextorm_WriteJson_ObjectNum()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>Eight-column write: the high end of the per-column cost slope.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_8Col()
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
            .WriteJson(_destination);
        _sink = _destination.Length;
    }
}
