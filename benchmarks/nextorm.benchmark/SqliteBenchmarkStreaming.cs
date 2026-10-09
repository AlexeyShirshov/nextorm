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
/// DTO streaming comparison on SQLite <c>large_table</c> (~10 000 rows): buffered DTO
/// materialization (<c>ToList</c>/<c>Query</c>) versus unbuffered DTO enumeration
/// (<c>ToAsyncEnumerable</c>/<c>QueryUnbuffered</c>/<c>AsAsyncEnumerable</c>). Every arm reads the
/// same projected columns into the shared <see cref="BenchmarkRowSink"/>; the workloads are
/// validated against each other in setup.
/// <para>
/// <b>Blocked subgroup (#189).</b> The zero-materialization subgroup (raw-reader consumption with
/// no entity/DTO construction) is deliberately absent: it depends on a CHECK-passed D189 SQLite
/// <c>ToDataReader</c>/<c>ToDataReaderAsync</c> that does not exist, so per the D188 escalation this
/// unit is blocked and active and no reader arm is implemented. No materialized arm is relabeled as
/// zero, and no fallback is substituted in its place.
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

    private readonly BenchmarkRowSink _sink = new();

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
    }

    private static void Compare(string context, (int Count, long Checksum) expected, (int Count, long Checksum) actual)
    {
        BenchmarkComparisonValidation.EnsureCount(expected.Count, actual.Count, context);
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, actual.Checksum, context);
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
        await foreach (var row in _efCompiled(_efCtx))
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
