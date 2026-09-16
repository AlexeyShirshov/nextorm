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

    public Linq2DbDataRepository()
    {
        var options = new DataOptions().UseSQLite($"Data Source={BenchDb.FilePath}", SQLiteProvider.Microsoft);
        _db = new DataConnection(options);
        _simple = _db.GetTable<Linq2DbSimpleEntity>();
        _large = _db.GetTable<Linq2DbLargeEntity>();
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

    public void Dispose() => _db.Dispose();
}
