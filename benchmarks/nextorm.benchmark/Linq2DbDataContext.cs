using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;

namespace nextorm.benchmark;

[Table("simple_entity")]
public sealed class Linq2DbSimpleEntity
{
    [PrimaryKey, Column("id")]
    public int Id { get; set; }
}

[Table("large_table")]
public sealed class Linq2DbLargeEntity
{
    [PrimaryKey, Column("id")]
    public long Id { get; set; }
    [Column("someString")]
    public string? Str { get; set; }
    [Column("dt")]
    public DateTime? Dt { get; set; }
}

[Table("complex_entity")]
public sealed class Linq2DbComplexEntity
{
    [PrimaryKey, Column("id")]
    public long Id { get; set; }
    [Column("nullableInt")]
    public int? Int { get; set; }
    [Column("someString")]
    public string? String { get; set; }
    [Column("d")]
    public double? Double { get; set; }
    [Column("dt")]
    public DateTime? Datetime { get; set; }
    [Column("b")]
    public bool? Boolean { get; set; }
    [Column("requiredString")]
    public string RequiredString { get; set; } = null!;
}

public sealed class Linq2DbDataRepository : IDisposable
{
    private static readonly Func<IDataContext, int, List<Linq2DbLargeEntity>> _compiledLargeById =
        LinqToDB.CompiledQuery.Compile((IDataContext db, int id) => db
            .GetTable<Linq2DbLargeEntity>()
            .Where(it => it.Id == id)
            .Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .ToList());

    private readonly DataConnection _db;
    private readonly ITable<Linq2DbSimpleEntity> _simple;
    private readonly ITable<Linq2DbLargeEntity> _large;
    private readonly ITable<Linq2DbComplexEntity> _complex;

    public Linq2DbDataRepository()
    {
        var options = new DataOptions().UseSQLite($"Data Source={BenchDb.FilePath}", SQLiteProvider.Microsoft);
        _db = new DataConnection(options);
        _simple = _db.GetTable<Linq2DbSimpleEntity>();
        _large = _db.GetTable<Linq2DbLargeEntity>();
        _complex = _db.GetTable<Linq2DbComplexEntity>();
    }

    public DataConnection Db => _db;

    // SqliteBenchmarkSingle
    public Task<int> SingleOrDefaultScalarAsync(int id, CancellationToken ct = default)
        => _simple.Where(it => it.Id == id).Select(it => it.Id).SingleOrDefaultAsync(ct);

    // SqliteBenchmarkAny
    public Task<bool> AnyAsync(int id, CancellationToken ct = default)
        => _simple.AnyAsync(it => it.Id == id, ct);

    // SqliteBenchmarkWhere
    public Task<List<int>> WhereIdsToListAsync(int id, CancellationToken ct = default)
        => _simple.Where(it => it.Id == id).Select(it => it.Id).ToListAsync(ct);

    public IAsyncEnumerable<int> WhereIdsStreamAsync(int id, CancellationToken ct = default)
        => _simple.Where(it => it.Id == id).Select(it => it.Id).AsAsyncEnumerable();

    // SqliteBenchmarkFirst
    public Task<Linq2DbLargeEntity?> FirstLargeAsync(long id, CancellationToken ct = default)
        => _large.FirstOrDefaultAsync(it => it.Id == id, ct);

    public Task<int> FirstScalarAsync(int id, CancellationToken ct = default)
        => _simple.Where(it => it.Id == id).Select(it => it.Id).FirstOrDefaultAsync(ct);

    // SqliteBenchmarkIteration
    public Task<List<Linq2DbSimpleEntity>> ToListSimpleAsync(CancellationToken ct = default)
        => _simple.ToListAsync(ct);

    public IAsyncEnumerable<Linq2DbSimpleEntity> StreamSimpleAsync(CancellationToken ct = default)
        => _simple.AsAsyncEnumerable();

    // SqliteBenchmarkJoin
    public Task<List<Linq2DbLargeEntity>> JoinAsync(int id, CancellationToken ct = default)
        => (from t1 in _large
            join t2 in _simple on t1.Id equals t2.Id
            where t2.Id == id
            select new Linq2DbLargeEntity { Id = t1.Id, Str = t1.Str, Dt = t1.Dt }).ToListAsync(ct);

    // SqliteBenchmarkCache
    public List<Linq2DbLargeEntity> LargeByIdToList(int id)
        => _large.Where(it => it.Id == id)
            .Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .ToList();

    public List<Linq2DbLargeEntity> LargeByIdCompiled(int id)
        => _compiledLargeById(_db, id);

    // SqliteBenchmarkSimulateWork
    public Task<List<Linq2DbLargeEntity>> LargeLessThanToListAsync(long limit, CancellationToken ct = default)
        => _large.Where(it => it.Id < limit)
            .Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .ToListAsync(ct);

    public Task<Linq2DbLargeEntity?> InnerFirstAsync(long id, int i, CancellationToken ct = default)
        => _large.Where(it => it.Id == id + i)
            .Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt })
            .FirstOrDefaultAsync(ct);

    // SqliteBenchmarkLargeIteration
    public Task<List<Linq2DbLargeEntity>> LargeToListAsync(CancellationToken ct = default)
        => _large.Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt }).ToListAsync(ct);

    public IAsyncEnumerable<Linq2DbLargeEntity> LargeStreamAsync(CancellationToken ct = default)
        => _large.Select(it => new Linq2DbLargeEntity { Id = it.Id, Str = it.Str, Dt = it.Dt }).AsAsyncEnumerable();

    // SqliteBenchmarkFeatures - LEFT JOIN
    public Task<List<LeftJoinRow>> LeftJoinAsync(CancellationToken ct = default)
        => (from s in _simple
            from c in _complex.LeftJoin(c => c.Id == (long)s.Id)
            select new LeftJoinRow { Id = s.Id, RightString = c.RequiredString }).ToListAsync(ct);

    // SqliteBenchmarkFeatures - four-table join
    public Task<List<FourJoinRow>> Join4Async(CancellationToken ct = default)
        => (from s1 in _simple
            join c1 in _complex on (long)s1.Id equals c1.Id
            join s2 in _simple on c1.Id equals (long)s2.Id
            join c2 in _complex on (long)s2.Id equals c2.Id
            select new FourJoinRow { A = s1.Id, B = c1.RequiredString, C = s2.Id, D = c2.RequiredString }).ToListAsync(ct);

    // SqliteBenchmarkFeatures - SELECT DISTINCT
    public Task<List<int?>> DistinctAsync(CancellationToken ct = default)
        => _complex.Select(it => it.Int).Distinct().ToListAsync(ct);

    // SqliteBenchmarkFeatures - CASE WHEN / ternary
    public Task<List<int>> CaseAsync(CancellationToken ct = default)
        => _complex.Select(it => it.Id > 1 ? 10 : 20).ToListAsync(ct);

    // SqliteBenchmarkFeatures - string functions
    public Task<List<string>> ToUpperAsync(CancellationToken ct = default)
        => _complex.Where(it => it.Id == 2).Select(it => it.String!.ToUpper()).ToListAsync(ct);

    public Task<List<long>> ContainsAsync(CancellationToken ct = default)
        => _complex.Where(it => it.String!.Contains("df")).Select(it => it.Id).ToListAsync(ct);

    // SqliteBenchmarkFeatures - IN list
    public Task<List<long>> InAsync(CancellationToken ct = default)
    {
        var values = new long[] { 1, 3, 10 };
        return _complex.Where(it => values.Contains(it.Id)).Select(it => it.Id).ToListAsync(ct);
    }

    // SqliteBenchmarkFeatures - window functions
    public Task<List<RowNumberRow>> RowNumberAsync(CancellationToken ct = default)
        => _complex.Select(it => new RowNumberRow
        {
            Id = it.Id,
            Rn = Sql.Window.RowNumber(f => f.PartitionBy(it.Int).OrderBy(it.Id))
        }).ToListAsync(ct);

    public Task<List<SumOverRow>> SumOverAsync(CancellationToken ct = default)
        => _complex.Select(it => new SumOverRow
        {
            Id = it.Id,
            Total = Sql.Window.Sum(it.Id, f => f.PartitionBy(it.Int))
        }).ToListAsync(ct);

    // SqliteBenchmarkFeatures - non-recursive CTE
    public Task<List<CteJoinRow>> CteAsync(CancellationToken ct = default)
    {
        var recent = _complex.Where(it => it.Id > 1)
            .Select(it => new { it.Id, it.RequiredString })
            .AsCte("recent");

        return (from r in recent
                join s in _simple on r.Id equals (long)s.Id
                select new CteJoinRow { Id = (int)r.Id, SimpleId = s.Id }).ToListAsync(ct);
    }

    // SqliteBenchmarkFeatures - recursive CTE
    public Task<List<int>> RecursiveCteAsync(CancellationToken ct = default)
    {
        var nums = _db.GetCte<int>(cte =>
            _simple.Where(s => s.Id == 1).Select(s => s.Id)
                .Concat(cte.Where(n => n < 5).Select(n => n + 1)),
            "nums");

        return nums.ToListAsync(ct);
    }

    // SqliteBenchmarkFeatures - INTERSECT / EXCEPT
    public Task<List<int>> IntersectAsync(CancellationToken ct = default)
        => _simple.Select(it => it.Id).Intersect(_complex.Select(it => (int)it.Id)).ToListAsync(ct);

    public Task<List<int>> ExceptAsync(CancellationToken ct = default)
        => _simple.Select(it => it.Id).Except(_complex.Select(it => (int)it.Id)).ToListAsync(ct);

    public void Dispose() => _db.Dispose();
}
