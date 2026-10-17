using System.ComponentModel.DataAnnotations.Schema;
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
/// Cross-library DTO-projection comparison on SQLite <c>large_table</c> (~10 000 rows). All arms
/// materialise the same shared <see cref="ProjectionDto"/> from the same columns and are validated
/// against each other outside the timed region.
/// <para>
/// Category <c>A</c> pairs the compiled/prepared forms: nextorm <c>Prepare()</c>, EF
/// <c>CompileAsyncQuery</c>, linq2db <c>CompiledQuery.Compile</c> and raw Dapper SQL (whose
/// command text is cached). Category <c>B</c> is the ordinary path: nextorm's implicit plan cache,
/// regular EF/linq2db queries and the same raw Dapper call.
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkProjection
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;
    private readonly IPreparedQueryCommand<ProjectionDto> _nextormPrepared;

    private long _sink;

    public SqliteBenchmarkProjection()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nextormPrepared = _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite($"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }

    private static readonly Func<LinqToDB.IDataContext, List<ProjectionDto>> _l2dbCompiled =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db
            .GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str })
            .ToList());

    private static readonly Func<EFDataContext, IAsyncEnumerable<ProjectionDto>> _efCompiled =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }));

    [GlobalSetup]
    public void Verify()
    {
        var expected = CountLargeRows();

        var nextorm = ToList(_nextormPrepared);
        var linq2Db = _linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str }).ToList();
        var ef = _efCtx.LargeEntities
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str }).AsNoTracking().ToList();
        var dapper = _conn.Query<ProjectionDto>("select id, someString as Str from large_table").ToList();

        var baseline = BenchmarkComparisonValidation.Measure(nextorm, r => r.Id);
        BenchmarkComparisonValidation.EnsureCount(expected, baseline.Count, "Projection/nextorm");
        Compare("Projection/linq2db", baseline, BenchmarkComparisonValidation.Measure(linq2Db, r => r.Id));
        Compare("Projection/EFCore", baseline, BenchmarkComparisonValidation.Measure(ef, r => r.Id));
        Compare("Projection/Dapper", baseline, BenchmarkComparisonValidation.Measure(dapper, r => r.Id));
        BenchmarkComparisonValidation.Report("Projection/nextorm", baseline.Count, baseline.Checksum, $"large_table={expected}");
    }

    private static void Compare(string context, (int Count, long Checksum) expected, (int Count, long Checksum) actual)
    {
        BenchmarkComparisonValidation.EnsureCount(expected.Count, actual.Count, context);
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, actual.Checksum, context);
    }

    private List<ProjectionDto> ToList(IPreparedQueryCommand<ProjectionDto> command) => _db.ToList(command);

    private long CountLargeRows() => _ctx.LargeEntity.Count();

    [Benchmark]
    [BenchmarkCategory("A_Projection")]
    public async Task A_Nextorm_Prepared_ToListAsync()
    {
        foreach (var row in await _db.ToListAsync(_nextormPrepared))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("A_Projection")]
    public async Task A_EFCore_Compiled_ToListAsync()
    {
        await foreach (var row in _efCompiled(_efCtx))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("A_Projection")]
    public long A_Linq2Db_Compiled_ToList()
    {
        long n = 0;
        foreach (var row in _l2dbCompiled(_linq2Db.Db))
            n += row.Id;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Projection", "B_Projection")]
    public async Task A_Dapper_ToListAsync()
    {
        foreach (var row in await _conn.QueryAsync<ProjectionDto>("select id, someString as Str from large_table"))
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Projection")]
    public async Task B_Nextorm_Cached_ToListAsync()
    {
        var rows = await _ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .ToListAsync();
        foreach (var row in rows)
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Projection")]
    public async Task B_EFCore_ToListAsync()
    {
        var rows = await _efCtx.LargeEntities
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .ToListAsync();
        foreach (var row in rows)
            _sink += row.Id;
    }

    [Benchmark]
    [BenchmarkCategory("B_Projection")]
    public async Task B_Linq2Db_ToListAsync()
    {
        var rows = await LinqToDB.Async.AsyncExtensions.ToListAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .Select(it => new ProjectionDto { Id = it.Id, Str = it.Str }));
        foreach (var row in rows)
            _sink += row.Id;
    }
}

/// <summary>The shared non-LOB projection DTO: the entity key and one variable-width string column.</summary>
public sealed class ProjectionDto
{
    public long Id { get; set; }

    [Column("someString")]
    public string? Str { get; set; }
}
