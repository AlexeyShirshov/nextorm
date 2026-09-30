using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// CSV streaming (<c>WriteCsv</c>, #112) versus the buffered baseline it replaces: materialise the
/// projection with <c>ToList()</c> and write the same CSV by hand. All cases run on the same SQLite
/// <c>large_table</c> slice (a GUID string and a DateTime, so the string/date formatting path is
/// exercised).
/// </summary>
/// <remarks>
/// The constructor warms the query-plan cache for the <c>ToList</c> baselines, so their measured
/// invocations do not include the first query-plan build. It does <b>not</b> warm the <c>WriteCsv</c>
/// cases: <c>WriteCsv</c> prepares with <c>storeInCache:false</c>, so its query plan is rebuilt on
/// every invocation, and that fixed per-call cost is included in the <c>Nextorm_WriteCsv</c> /
/// <c>Nextorm_StringProjection_WriteCsv</c> measurements. The CSV column plan itself is likewise
/// <b>not</b> cached and is compiled on every invocation.
/// The class is tagged <c>csv</c> only; the frozen seven-case perf category is not touched by this
/// benchmark.
/// </remarks>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("csv")]
public class SqliteBenchmarkCsv
{
    private const string Header = "Id,Str,Dt\r\n";

    // The whole large_table (10,000 rows). The workload is deliberately row-heavy: streaming keeps
    // the per-row cost O(1) while <c>ToList()</c> materialises every row before the CSV is written.
    private const int Rows = 10_000;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly MemoryStream _destination = new(1 << 18);
    private readonly StreamWriter _manualWriter;
    private long _sink;

    public SqliteBenchmarkCsv()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _manualWriter = new StreamWriter(_destination, new UTF8Encoding(false), 1 << 16, leaveOpen: true);

        // Warm the plan cache for the ToList baselines; WriteCsv rebuilds its plan on every call.
        _ctx.LargeEntity.Limit(Rows).ToList();
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).ToList();
    }

    [Benchmark(Baseline = true)]
    public void Nextorm_ToList()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).ToList();
        _sink = rows.Count;
    }

    [Benchmark]
    public void Nextorm_ToList_ManualCsv()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).ToList();
        _destination.SetLength(0);
        _destination.Position = 0;
        _manualWriter.Write(Header);
        foreach (var row in rows)
            _manualWriter.Write($"{row.Id},{row.Str},{row.Dt:O}\r\n");
        _manualWriter.Flush();
        _sink = rows.Count;
    }

    [Benchmark]
    public void Nextorm_WriteCsv()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
        _ctx.LargeEntity.Limit(Rows).WriteCsv(_destination, null, CancellationToken.None);
        _sink = _destination.Length;
    }

    [Benchmark]
    public void Nextorm_StringProjection_ToList()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).ToList();
        _sink = rows.Count;
    }

    [Benchmark]
    public void Nextorm_StringProjection_WriteCsv()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).WriteCsv(_destination, null, CancellationToken.None);
        _sink = _destination.Length;
    }
}
