using System.Data;
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
/// Streaming comparison on SQLite <c>large_table</c> (10 000 rows): buffered DTO materialization
/// (<c>ToList</c>/<c>Query</c>), unbuffered DTO enumeration
/// (<c>ToAsyncEnumerable</c>/<c>QueryUnbuffered</c>/<c>AsAsyncEnumerable</c>) and synchronous
/// zero-materialization raw-reader consumption. Every arm reads the same projected columns into the
/// shared <see cref="BenchmarkRowSink"/>; the workloads are validated against each other in setup.
/// <para>
/// <b>Zero-materialization subgroup (synchronous readers).</b> Nextorm, Dapper and linq2db consume the
/// same two-column projection through a raw <see cref="IDataReader"/> with no entity/DTO construction:
/// Nextorm via the prepared <c>ToDataReader</c> terminal, Dapper via <c>ExecuteReader</c> and linq2db
/// via its <c>DataConnection</c> raw-reader API. All three share one scanner and one
/// <see cref="BenchmarkRowSink"/>. EF Core is intentionally excluded from this subgroup because it has
/// no agreed raw-reader counterpart. The asynchronous reader arms remain deferred.
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkStreaming
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;

    private readonly IPreparedQueryCommand<ProjectionDto> _nextormBuffered;
    private readonly IPreparedQueryCommand<ProjectionDto> _nextormUnbuffered;
    private readonly QueryCommand<ProjectionDto> _nextormReader;

    private readonly BenchmarkRowSink _sink = new();

    private const string RawReaderSql = "select id, someString as Str from large_table";
    private const string EmptyRawReaderSql = "select id, someString as Str from large_table where id < 0";

    public SqliteBenchmarkStreaming()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nextormBuffered = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .Prepare();

        _nextormUnbuffered = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .Prepare(false);

        _nextormReader = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str });
        _ = _nextormReader.Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite($"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }

    private static readonly Func<EFDataContext, IAsyncEnumerable<ProjectionDto>> _efCompiled =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }));

    [GlobalSetup]
    public async Task Verify()
    {
        var rows = _ctx.LargeEntity.Count();
        BenchmarkComparisonValidation.EnsureCount(SqliteBenchmarkAggregates.ExpectedRows, rows, "Streaming/large_table");

        var buffered = await _db.ToListAsync(_nextormBuffered);
        var baseline = BenchmarkComparisonValidation.Measure(buffered, r => r.Id);
        BenchmarkComparisonValidation.EnsureCount(SqliteBenchmarkAggregates.ExpectedRows, baseline.Count, "Streaming/nextorm-buffered");
        BenchmarkComparisonValidation.Report("Streaming/nextorm-buffered", baseline.Count, baseline.Checksum);

        var dapperUnbuffered = new List<ProjectionDto>();
        await foreach (var row in _conn.QueryUnbufferedAsync<ProjectionDto>("select id, someString as Str from large_table"))
            dapperUnbuffered.Add(row);
        var efUnbuffered = new List<ProjectionDto>();
        await foreach (var row in _efCompiled(_efCtx))
            efUnbuffered.Add(row);

        Compare("Streaming/dapper-unbuffered", baseline, BenchmarkComparisonValidation.Measure(dapperUnbuffered, r => r.Id));
        Compare("Streaming/EFCore-unbuffered", baseline, BenchmarkComparisonValidation.Measure(efUnbuffered, r => r.Id));

        VerifyRawReader("Streaming/nextorm-reader",
            () => { using var reader = _nextormReader.ToDataReader(); Scan(reader); },
            baseline.Count, baseline.Checksum, referenceBytes: null);
        var readerBytes = _sink.Bytes;
        BenchmarkComparisonValidation.Ensure(readerBytes > 0, "Streaming/nextorm-reader: expected non-zero byte count.");
        VerifyRawReader("Streaming/dapper-reader",
            () => { using var reader = _conn.ExecuteReader(RawReaderSql); Scan(reader); },
            baseline.Count, baseline.Checksum, readerBytes);
        VerifyRawReader("Streaming/linq2db-reader",
            () => { using var reader = LinqToDB.Data.DataContextExtensions.ExecuteReader(_linq2Db.Db, RawReaderSql); Scan(reader.Reader!); },
            baseline.Count, baseline.Checksum, readerBytes);
        VerifyRawReader("Streaming/nextorm-regular-reader",
            () => { using var reader = _ctx.LargeEntity.Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToDataReader(); Scan(reader); },
            baseline.Count, baseline.Checksum, readerBytes);

        // Null-Str and empty-input probes: the shared scanner must fold the IsDBNull guard and return zero rows.
        _sink.Reset();
        using (var reader = _ctx.ComplexEntity.OrderBy(e => e.Id).Select(e => new ProjectionDto { Id = e.Id, Str = e.String }).ToDataReader())
            Scan(reader);
        BenchmarkComparisonValidation.EnsureCount(3, _sink.Rows, "Streaming/null-Str-probe");
        BenchmarkComparisonValidation.Ensure(_sink.Bytes == 10, $"Streaming/null-Str-probe: expected 10 non-null string chars, observed {_sink.Bytes}.");
        BenchmarkComparisonValidation.Report("Streaming/null-Str-probe", _sink.Rows, _sink.Checksum, $"bytes={_sink.Bytes}");

        _sink.Reset();
        using (var reader = _ctx.LargeEntity.Where(e => e.Id < 0).Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToDataReader())
            Scan(reader);
        BenchmarkComparisonValidation.EnsureCount(0, _sink.Rows, "Streaming/empty-reader-probe");
    }

    private void VerifyRawReader(string context, Action scanArm, int expectedRows, long expectedChecksum, long? referenceBytes)
    {
        _sink.Reset();
        scanArm();
        BenchmarkComparisonValidation.EnsureCount(expectedRows, _sink.Rows, context);
        BenchmarkComparisonValidation.EnsureChecksum(expectedChecksum, _sink.Checksum, context);
        if (referenceBytes is long bytes)
            BenchmarkComparisonValidation.Ensure(bytes == _sink.Bytes, $"{context}: byte count {_sink.Bytes} differs from reference {bytes}.");
        BenchmarkComparisonValidation.Report(context, _sink.Rows, _sink.Checksum, $"bytes={_sink.Bytes}");
    }

    /// <summary>
    /// The single non-retaining raw-reader scanner shared by all three zero-materialization arms: reads
    /// <c>id</c> (ordinal 0) and the nullable <c>someString</c> (ordinal 1, <c>IsDBNull</c>-guarded) and
    /// folds the identifier into the checksum and the string length into the byte counter. No DTO or
    /// collection is created and the caller keeps ownership of the reader.
    /// </summary>
    private void Scan(IDataReader reader)
    {
        while (reader.Read())
        {
            _sink.Add(reader.GetInt64(0));
            if (!reader.IsDBNull(1))
                _sink.AddBytes(reader.GetString(1).Length);
        }
    }

    [IterationSetup(Targets = new[]
    {
        nameof(A_Nextorm_Prepared_ToDataReader),
        nameof(B_Nextorm_ToDataReader),
        nameof(Dapper_ToDataReader),
        nameof(Linq2Db_ToDataReader),
        nameof(B_Nextorm_ToDataReader_Empty),
        nameof(Dapper_ToDataReader_Empty),
        nameof(Linq2Db_ToDataReader_Empty),
    })]
    public void ResetReaderSink() => _sink.Reset();

    private static void Compare(string context, (int Count, long Checksum) expected, (int Count, long Checksum) actual)
    {
        BenchmarkComparisonValidation.EnsureCount(expected.Count, actual.Count, context);
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, actual.Checksum, context);
    }

    [Benchmark]
    [BenchmarkCategory("A_RawReader")]
    public void A_Nextorm_Prepared_ToDataReader()
    {
        using var reader = _nextormReader.ToDataReader();
        Scan(reader);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader")]
    public void B_Nextorm_ToDataReader()
    {
        using var reader = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .ToDataReader();
        Scan(reader);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader")]
    public void Dapper_ToDataReader()
    {
        using var reader = _conn.ExecuteReader(RawReaderSql);
        Scan(reader);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader")]
    public void Linq2Db_ToDataReader()
    {
        using var reader = LinqToDB.Data.DataContextExtensions.ExecuteReader(_linq2Db.Db, RawReaderSql);
        Scan(reader.Reader!);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader_Empty")]
    public void B_Nextorm_ToDataReader_Empty()
    {
        using var reader = _ctx.LargeEntity
            .Where(e => e.Id < 0)
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .ToDataReader();
        Scan(reader);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader_Empty")]
    public void Dapper_ToDataReader_Empty()
    {
        using var reader = _conn.ExecuteReader(EmptyRawReaderSql);
        Scan(reader);
    }

    [Benchmark]
    [BenchmarkCategory("B_RawReader_Empty")]
    public void Linq2Db_ToDataReader_Empty()
    {
        using var reader = LinqToDB.Data.DataContextExtensions.ExecuteReader(_linq2Db.Db, EmptyRawReaderSql);
        Scan(reader.Reader!);
    }

    [Benchmark]
    [BenchmarkCategory("A_BufferedDto")]
    public async Task A_Nextorm_Prepared_ToList_Dto()
    {
        foreach (var row in await _db.ToListAsync(_nextormBuffered))
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_UnbufferedDto")]
    public async Task A_Nextorm_Prepared_AsyncStream_Dto()
    {
        await foreach (var row in _nextormUnbuffered.ToAsyncEnumerable(_db))
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_BufferedDto")]
    public async Task A_EFCore_Compiled_ToList_Dto()
    {
        foreach (var row in await _efCompiled(_efCtx).ToListAsync())
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_UnbufferedDto")]
    public async Task A_EFCore_Compiled_AsyncStream_Dto()
    {
        await foreach (var row in _efCompiled(_efCtx))
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_BufferedDto", "B_BufferedDto")]
    public async Task Dapper_ToList_Dto()
    {
        foreach (var row in await _conn.QueryAsync<ProjectionDto>("select id, someString as Str from large_table"))
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_UnbufferedDto", "B_UnbufferedDto")]
    public async Task Dapper_AsyncStream_Dto()
    {
        await foreach (var row in _conn.QueryUnbufferedAsync<ProjectionDto>("select id, someString as Str from large_table"))
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("A_BufferedDto")]
    public long Linq2Db_Compiled_ToList_Dto()
    {
        long n = 0;
        foreach (var row in Linq2DbCompiled(_linq2Db.Db))
            n += row.Id;
        _sink.Add(n);
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("B_BufferedDto")]
    public async Task B_Nextorm_Cached_ToList_Dto()
    {
        var rows = await _ctx.LargeEntity.Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToListAsync();
        foreach (var row in rows)
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("B_UnbufferedDto")]
    public async Task B_Nextorm_Cached_AsyncStream_Dto()
    {
        await foreach (var row in _ctx.LargeEntity.Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToAsyncEnumerable())
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("B_BufferedDto")]
    public async Task B_EFCore_ToList_Dto()
    {
        var rows = await _efCtx.LargeEntities.Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToListAsync();
        foreach (var row in rows)
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("B_UnbufferedDto")]
    public async Task B_EFCore_AsyncStream_Dto()
    {
        await foreach (var row in _efCtx.LargeEntities.Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).AsAsyncEnumerable())
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("B_BufferedDto")]
    public async Task B_Linq2Db_ToList_Dto()
    {
        var rows = await LinqToDB.Async.AsyncExtensions.ToListAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str }));
        foreach (var row in rows)
            _sink.Add(row.Id);
    }

    [Benchmark]
    [BenchmarkCategory("B_UnbufferedDto")]
    public async Task B_Linq2Db_AsyncStream_Dto()
    {
        await foreach (var row in LinqToDB.Async.AsyncExtensions.AsAsyncEnumerable(_linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str })))
            _sink.Add(row.Id);
    }

    private static readonly Func<LinqToDB.IDataContext, List<ProjectionDto>> _l2dbCompiled =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db
            .GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str })
            .ToList());

    private static List<ProjectionDto> Linq2DbCompiled(LinqToDB.IDataContext db) => _l2dbCompiled(db);
}
