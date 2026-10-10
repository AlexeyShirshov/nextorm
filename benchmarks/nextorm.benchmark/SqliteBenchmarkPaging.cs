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
/// Cross-library ordered paging comparison on SQLite <c>large_table</c> (~10 000 rows):
/// <c>ORDER BY id OFFSET 5000 LIMIT 100</c>. The ordering key is the unique primary key, so the
/// page is deterministic and no tie-breaker is needed. The interior page (rows 5001..5100) is
/// validated to be non-empty in setup, outside the timed region.
/// <para>
/// Category <c>A</c>: nextorm <c>Prepare()</c>, EF <c>CompileAsyncQuery</c>, linq2db
/// <c>CompiledQuery.Compile</c> and raw Dapper SQL. Category <c>B</c>: the ordinary paths.
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkPaging
{
    /// <summary>The number of rows skipped before the measured page (interior page).</summary>
    public const int OffsetRows = 5_000;

    /// <summary>The measured page size.</summary>
    public const int PageSize = 100;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;
    private readonly IPreparedQueryCommand<ProjectionDto> _nextormPrepared;

    private long _sink;

    public SqliteBenchmarkPaging()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nextormPrepared = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .OrderBy(e => e.Id)
            .Offset(OffsetRows)
            .Limit(PageSize)
            .Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite($"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }

    private const string PageSql =
        "select id, someString as Str from large_table order by id limit 100 offset 5000";

    private static readonly Func<LinqToDB.IDataContext, List<ProjectionDto>> _l2dbCompiled =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db
            .GetTable<Linq2DbLargeEntity>()
            .OrderBy(it => it.Id)
            .Skip(OffsetRows)
            .Take(PageSize)
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str })
            .ToList());

    private static readonly Func<EFDataContext, IAsyncEnumerable<ProjectionDto>> _efCompiled =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities
            .OrderBy(e => e.Id)
            .Skip(OffsetRows)
            .Take(PageSize)
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }));

    [GlobalSetup]
    public void Verify()
    {
        var rows = _ctx.LargeEntity.Count();
        BenchmarkComparisonValidation.EnsureCount(SqliteBenchmarkAggregates.ExpectedRows, rows, "Paging/large_table");

        var page = _db.ToList(_nextormPrepared);
        BenchmarkComparisonValidation.EnsureCount(PageSize, page.Count, "Paging/nextorm-page");
        BenchmarkComparisonValidation.Ensure(page.Count > 0, "Paging/nextorm measured interior page is empty.");
        BenchmarkComparisonValidation.EnsureCount(OffsetRows + 1, page[0].Id, "Paging/nextorm first-id");

        var firstChecksum = BenchmarkComparisonValidation.Checksum(page, r => r.Id);
        var l2db = _l2dbCompiled(_linq2Db.Db);
        var dapper = _conn.Query<ProjectionDto>(PageSql).ToList();
        var ef = _efCtx.LargeEntities.OrderBy(e => e.Id).Skip(OffsetRows).Take(PageSize)
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).ToList();

        BenchmarkComparisonValidation.EnsureCount(firstChecksum, BenchmarkComparisonValidation.Checksum(l2db, r => r.Id), "Paging/linq2db-checksum");
        BenchmarkComparisonValidation.EnsureCount(firstChecksum, BenchmarkComparisonValidation.Checksum(dapper, r => r.Id), "Paging/dapper-checksum");
        BenchmarkComparisonValidation.EnsureCount(firstChecksum, BenchmarkComparisonValidation.Checksum(ef, r => r.Id), "Paging/EFCore-checksum");
        BenchmarkComparisonValidation.Report("Paging/nextorm", page.Count, firstChecksum, $"offset={OffsetRows}, limit={PageSize}, large_table={rows}");
    }

    [Benchmark]
    [BenchmarkCategory("A_Paging")]
    public async Task A_Nextorm_Prepared_PageAsync()
    {
        foreach (var row in await _db.ToListAsync(_nextormPrepared))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("A_Paging")]
    public async Task A_EFCore_Compiled_PageAsync()
    {
        await foreach (var row in _efCompiled(_efCtx))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("A_Paging")]
    public long A_Linq2Db_Compiled_Page()
    {
        long n = 0;
        foreach (var row in _l2dbCompiled(_linq2Db.Db))
            n += row.Id;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Paging", "B_Paging")]
    public async Task A_Dapper_PageAsync()
    {
        foreach (var row in await _conn.QueryAsync<ProjectionDto>(PageSql))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Paging")]
    public async Task B_Nextorm_Cached_PageAsync()
    {
        var rows = await _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .OrderBy(e => e.Id)
            .Offset(OffsetRows)
            .Limit(PageSize)
            .ToListAsync();
        foreach (var row in rows)
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Paging")]
    public async Task B_EFCore_PageAsync()
    {
        var rows = await _efCtx.LargeEntities
            .OrderBy(e => e.Id)
            .Skip(OffsetRows)
            .Take(PageSize)
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .ToListAsync();
        foreach (var row in rows)
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Paging")]
    public async Task B_Linq2Db_PageAsync()
    {
        var rows = await LinqToDB.Async.AsyncExtensions.ToListAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .OrderBy(it => it.Id)
            .Skip(OffsetRows)
            .Take(PageSize)
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str }));
        foreach (var row in rows)
            _sink += row.Id;
    }
}
