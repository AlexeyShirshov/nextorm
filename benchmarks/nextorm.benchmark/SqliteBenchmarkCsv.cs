using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Dapper;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;
using NextORM.Sqlite;
using IDataContext = NextORM.Core.IDataContext;

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
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;
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

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite($"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _linq2Db = new Linq2DbDataRepository();

        // Warm the plan cache for the ToList baselines; WriteCsv rebuilds its plan on every call.
        _ctx.LargeEntity.Limit(Rows).ToList();
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).ToList();

        ValidateCompetitorCsv();
    }

    /// <summary>
    /// Setup-time proof that the three materialize→manual-CSV arms emit the same bytes as the native
    /// <c>Nextorm_ToList_ManualCsv</c> baseline. Runs outside the timed region.
    /// </summary>
    private void ValidateCompetitorCsv()
    {
        var nextorm = WriteManualCsv(_ctx.LargeEntity.Limit(Rows).ToList(), static row => $"{row.Id},{row.Str},{row.Dt:O}\r\n");
        var dapper = WriteManualCsv(_conn.Query<LargeEntity>("select id, someString as str, dt from large_table").ToList(), static row => $"{row.Id},{row.Str},{row.Dt:O}\r\n");
        var linq2Db = WriteManualCsv(
            _linq2Db.Db.GetTable<Linq2DbLargeEntity>().Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt }).ToList(),
            static row => $"{row.Id},{row.Str},{row.Dt:O}\r\n");
        var ef = WriteManualCsv(
            _efCtx.LargeEntities.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).ToList(),
            static row => $"{row.Id},{row.Str},{row.Dt:O}\r\n");

        foreach (var (name, candidate) in new[] { ("dapper", dapper), ("linq2db", linq2Db), ("EFCore", ef) })
            BenchmarkComparisonValidation.Ensure(
                nextorm.AsSpan().SequenceEqual(candidate),
                $"CSV/{name}: manual output differs from the Nextorm baseline ({nextorm.Length} vs {candidate.Length} bytes).");

        BenchmarkComparisonValidation.Report("CSV/nextorm", Rows, nextorm.Length, $"bytes={nextorm.Length}");
    }

    private static byte[] WriteManualCsv<T>(IEnumerable<T> rows, Func<T, string> format)
    {
        using var stream = new MemoryStream(1 << 18);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1 << 16, leaveOpen: true))
        {
            writer.Write(Header);
            foreach (var row in rows)
                writer.Write(format(row));
            writer.Flush();
        }
        return stream.ToArray();
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

    /// <summary>Closest equivalent for Dapper: materialize the rows, then write the same CSV by hand.</summary>
    /// <remarks>This is a materialize→serialize arm, not a streaming arm; the label says so.</remarks>
    [Benchmark]
    public void Dapper_ToList_ManualCsv()
    {
        var rows = _conn.Query<LargeEntity>("select id, someString as str, dt from large_table").ToList();
        ResetDestination();
        _manualWriter.Write(Header);
        foreach (var row in rows)
            _manualWriter.Write($"{row.Id},{row.Str},{row.Dt:O}\r\n");
        _manualWriter.Flush();
        _sink = rows.Count;
    }

    /// <summary>Closest equivalent for linq2db: materialize the rows, then write the same CSV by hand.</summary>
    [Benchmark]
    public void Linq2Db_ToList_ManualCsv()
    {
        var rows = _linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .ToList();
        ResetDestination();
        _manualWriter.Write(Header);
        foreach (var row in rows)
            _manualWriter.Write($"{row.Id},{row.Str},{row.Dt:O}\r\n");
        _manualWriter.Flush();
        _sink = rows.Count;
    }

    /// <summary>Closest equivalent for EF Core: materialize the rows, then write the same CSV by hand.</summary>
    [Benchmark]
    public void EFCore_ToList_ManualCsv()
    {
        var rows = _efCtx.LargeEntities
            .Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt })
            .ToList();
        ResetDestination();
        _manualWriter.Write(Header);
        foreach (var row in rows)
            _manualWriter.Write($"{row.Id},{row.Str},{row.Dt:O}\r\n");
        _manualWriter.Flush();
        _sink = rows.Count;
    }

    private void ResetDestination()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
    }
}
