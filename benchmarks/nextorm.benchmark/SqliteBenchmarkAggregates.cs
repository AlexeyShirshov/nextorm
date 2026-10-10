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
/// SQL-side aggregate comparison on SQLite <c>large_table</c> (~10 000 rows): <c>COUNT(*)</c>,
/// <c>SUM(id)</c> and <c>GROUP BY id % 100</c> with a per-group count. The grouping key is fixed at
/// 100 buckets so all four libraries translate it to SQL; no client-side aggregation is measured.
/// <para>
/// Category <c>A</c> uses nextorm <c>Prepare()</c>, EF <c>CompileAsyncQuery</c>, linq2db
/// <c>CompiledQuery.Compile</c> and raw Dapper SQL; Category <c>B</c> uses the ordinary paths
/// (nextorm plan cache, regular EF/linq2db and the same raw Dapper call).
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkAggregates
{
    /// <summary>The grouping divisor; <c>large_table</c> keys 1..10000 split into exactly 100 buckets.</summary>
    private const int GroupModulus = 100;

    /// <summary>The <see cref="SqliteBenchmarkAggregates"/> arms all aggregate the same rows; the observed count is validated to this value.</summary>
    public const int ExpectedRows = 10_000;

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;

    private readonly IPreparedQueryCommand<int> _nextormCount;
    private readonly IPreparedQueryCommand<long> _nextormSum;
    private readonly IPreparedQueryCommand<GroupCountRow> _nextormGroupBy;

    private long _sink;

    public SqliteBenchmarkAggregates()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        var countCmd = _ctx.LargeEntity.Select(e => SqlFunctions.Sql.count());
        countCmd.SingleRow = true;
        _nextormCount = countCmd.Prepare();

        var sumCmd = _ctx.LargeEntity.Select(e => SqlFunctions.Sql.sum(e.Id));
        sumCmd.SingleRow = true;
        _nextormSum = sumCmd.Prepare();

        _nextormGroupBy = _ctx.LargeEntity
            .GroupBy(e => new { Bucket = e.Id % GroupModulus })
            .Select(e => new GroupCountRow { Key = e.Id % GroupModulus, Count = SqlFunctions.Sql.count() })
            .Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite($"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }

    private static readonly Func<EFDataContext, Task<int>> _efCompiledCount =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities.Count());

    private static readonly Func<EFDataContext, Task<long>> _efCompiledSum =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities.Sum(e => e.Id));

    private static readonly Func<EFDataContext, IAsyncEnumerable<GroupCountRow>> _efCompiledGroupBy =
        EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities
            .GroupBy(e => e.Id % GroupModulus)
            .Select(g => new GroupCountRow { Key = g.Key, Count = g.Count() }));

    private static readonly Func<LinqToDB.IDataContext, int> _l2dbCompiledCount =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db.GetTable<Linq2DbLargeEntity>().Count());

    private static readonly Func<LinqToDB.IDataContext, long> _l2dbCompiledSum =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db.GetTable<Linq2DbLargeEntity>().Sum(it => it.Id));

    private static readonly Func<LinqToDB.IDataContext, List<GroupCountRow>> _l2dbCompiledGroupBy =
        LinqToDB.CompiledQuery.Compile((LinqToDB.IDataContext db) => db.GetTable<Linq2DbLargeEntity>()
            .GroupBy(it => it.Id % GroupModulus)
            .Select(g => new GroupCountRow { Key = g.Key, Count = g.Count() })
            .ToList());

    [GlobalSetup]
    public void Verify()
    {
        var rows = _ctx.LargeEntity.Count();
        BenchmarkComparisonValidation.EnsureCount(ExpectedRows, rows, "Aggregates/large_table");
        var expectedSum = SumOfFirstN(rows);

        var count = _db.ExecuteScalar(_nextormCount, ReadOnlySpan<object?>.Empty, true);
        var sum = _db.ExecuteScalar(_nextormSum, ReadOnlySpan<object?>.Empty, false);
        BenchmarkComparisonValidation.EnsureCount(rows, count, "Aggregates/nextorm-count");
        BenchmarkComparisonValidation.EnsureCount(expectedSum, sum, "Aggregates/nextorm-sum");

        var l2dbCount = _linq2Db.Db.GetTable<Linq2DbLargeEntity>().Count();
        var l2dbSum = _linq2Db.Db.GetTable<Linq2DbLargeEntity>().Sum(it => it.Id);
        BenchmarkComparisonValidation.EnsureCount(rows, l2dbCount, "Aggregates/linq2db-count");
        BenchmarkComparisonValidation.EnsureCount(expectedSum, l2dbSum, "Aggregates/linq2db-sum");

        var dapperCount = _conn.ExecuteScalar<long>("select count(*) from large_table");
        var dapperSum = _conn.ExecuteScalar<long?>("select sum(id) from large_table");
        BenchmarkComparisonValidation.EnsureCount(rows, dapperCount, "Aggregates/dapper-count");
        BenchmarkComparisonValidation.EnsureCount(expectedSum, dapperSum ?? -1, "Aggregates/dapper-sum");

        var efCount = _efCtx.LargeEntities.Count();
        var efSum = _efCtx.LargeEntities.Sum(e => e.Id);
        BenchmarkComparisonValidation.EnsureCount(rows, efCount, "Aggregates/EFCore-count");
        BenchmarkComparisonValidation.EnsureCount(expectedSum, efSum, "Aggregates/EFCore-sum");

        var groups = _db.ToList(_nextormGroupBy);
        BenchmarkComparisonValidation.EnsureCount(GroupModulus, groups.Count, "Aggregates/nextorm-groupby-groups");
        BenchmarkComparisonValidation.Report("Aggregates/nextorm", rows, expectedSum, $"sum={sum}, groups={groups.Count}");
    }

    private static long SumOfFirstN(long n) => n * (n + 1) / 2;

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public long A_Nextorm_Prepared_Count()
    {
        var value = _db.ExecuteScalar(_nextormCount, ReadOnlySpan<object?>.Empty, true);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public long A_Nextorm_Prepared_Sum()
    {
        var value = _db.ExecuteScalar(_nextormSum, ReadOnlySpan<object?>.Empty, false);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public async Task A_Nextorm_Prepared_GroupByCount()
    {
        foreach (var row in await _db.ToListAsync(_nextormGroupBy))
            _sink += row.Count;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public async Task<long> A_EFCore_Compiled_Count() => _sink += await _efCompiledCount(_efCtx);

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public async Task<long> A_EFCore_Compiled_Sum()
    {
        var value = await _efCompiledSum(_efCtx);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public async Task A_EFCore_Compiled_GroupByCount()
    {
        await foreach (var row in _efCompiledGroupBy(_efCtx))
            _sink += row.Count;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public long A_Linq2Db_Compiled_Count() => _sink += _l2dbCompiledCount(_linq2Db.Db);

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public long A_Linq2Db_Compiled_Sum()
    {
        var value = _l2dbCompiledSum(_linq2Db.Db);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates")]
    public int A_Linq2Db_Compiled_GroupByCount()
    {
        var groups = _l2dbCompiledGroupBy(_linq2Db.Db);
        foreach (var row in groups)
            _sink += row.Count;
        return groups.Count;
    }

    [Benchmark]
    [BenchmarkCategory("A_Aggregates", "B_Aggregates")]
    public async Task<long> Dapper_Count() => _sink += await _conn.ExecuteScalarAsync<long>("select count(*) from large_table");

    [Benchmark]
    [BenchmarkCategory("A_Aggregates", "B_Aggregates")]
    public async Task<long?> Dapper_Sum() => _sink += await _conn.ExecuteScalarAsync<long?>("select sum(id) from large_table") ?? 0;

    [Benchmark]
    [BenchmarkCategory("A_Aggregates", "B_Aggregates")]
    public async Task<int> Dapper_GroupByCount()
    {
        var groups = (await _conn.QueryAsync<GroupCountRow>(
            "select id % 100 as \"Key\", count(*) as \"Count\" from large_table group by id % 100")).ToList();
        foreach (var row in groups)
            _sink += row.Count;
        return groups.Count;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public long B_Nextorm_Cached_Count() => _sink += _ctx.LargeEntity.Count();

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public long B_Nextorm_Cached_Sum()
    {
        var value = _ctx.LargeEntity.Sum(e => e.Id);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<int> B_Nextorm_Cached_GroupByCount()
    {
        var groups = await _ctx.LargeEntity
            .GroupBy(e => new { Bucket = e.Id % GroupModulus })
            .Select(e => new GroupCountRow { Key = e.Id % GroupModulus, Count = SqlFunctions.Sql.count() })
            .ToListAsync();
        foreach (var row in groups)
            _sink += row.Count;
        return groups.Count;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<long> B_EFCore_Count() => _sink += await _efCtx.LargeEntities.CountAsync();

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<long> B_EFCore_Sum()
    {
        var value = await _efCtx.LargeEntities.SumAsync(e => e.Id);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<int> B_EFCore_GroupByCount()
    {
        var groups = await _efCtx.LargeEntities
            .GroupBy(e => e.Id % GroupModulus)
            .Select(g => new GroupCountRow { Key = g.Key, Count = g.Count() })
            .ToListAsync();
        foreach (var row in groups)
            _sink += row.Count;
        return groups.Count;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<long> B_Linq2Db_Count() => _sink += await LinqToDB.Async.AsyncExtensions.CountAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>());

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<long> B_Linq2Db_Sum()
    {
        var value = await LinqToDB.Async.AsyncExtensions.SumAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>(), it => it.Id);
        _sink += value;
        return value;
    }

    [Benchmark]
    [BenchmarkCategory("B_Aggregates")]
    public async Task<int> B_Linq2Db_GroupByCount()
    {
        var groups = await LinqToDB.Async.AsyncExtensions.ToListAsync(_linq2Db.Db.GetTable<Linq2DbLargeEntity>()
            .GroupBy(it => it.Id % GroupModulus)
            .Select(g => new GroupCountRow { Key = g.Key, Count = g.Count() }));
        foreach (var row in groups)
            _sink += row.Count;
        return groups.Count;
    }
}

/// <summary>One <c>GROUP BY</c> bucket: the key value and the number of rows it contains.</summary>
public sealed class GroupCountRow
{
    public long Key { get; set; }

    public int Count { get; set; }
}
