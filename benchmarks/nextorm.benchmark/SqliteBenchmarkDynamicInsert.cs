using BenchmarkDotNet.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// DML write-path benchmark for the dynamic-columns store (#104): a plain mapped <c>INSERT</c> versus the
/// same <c>INSERT</c> whose entity carries a populated store. The measured region is the real
/// <c>CreateInsertBuilder&lt;T&gt;().Values(...)</c> build (store key extraction + ordinal sort inside it), the SQL
/// render and the execution against a benchmark-owned SQLite database, so the dynamic-key work is not
/// hoisted out of the measurement and the statement actually runs. Each invocation executes inside a
/// transaction that is rolled back, so the writes are real but repeated runs neither fsync per row nor
/// accumulate data. Each iteration writes a fresh (monotonically increasing) key, so no primary-key
/// collision.
/// Cases: <c>Insert_Static</c> uses a genuinely store-less entity (<c>StaticInsertEntity</c>, mapped
/// columns only) and <c>Insert_DynamicColumns</c> uses the store-carrying <c>DynamicInsertEntity</c>.
/// Both arms measure the identical region, so the delta is the true cost of the dynamic-columns path.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkDynamicInsert
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly string _path;
    private int _nextId;

    public SqliteBenchmarkDynamicInsert()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"dynamic-insert-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText =
                "create table dynamic_insert_bench (id integer primary key, name text, \"age\" integer, \"city\" text);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();
    }

    /// <summary>Baseline: a mapped-only <c>INSERT ... VALUES</c> built, rendered and executed per iteration.
    /// Uses <see cref="StaticInsertEntity"/>, which has no dynamic-columns store at all.</summary>
    /// <returns>The number of rows written, so the loop is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int Insert_Static()
    {
        using var tx = ((ITransactionManager)_db).BeginTransaction();
        var written = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var entity = new StaticInsertEntity { Id = ++_nextId, Name = "static" };
            written += _db.CreateInsertBuilder<StaticInsertEntity>().Values(entity).Insert();
        }

        tx.Rollback();
        return written;
    }

    /// <summary>A dynamic-columns <c>INSERT ... VALUES</c>: the store's keys are quoted columns and its values bind as extra parameters.</summary>
    /// <returns>The number of rows written, so the loop is not optimised away.</returns>
    [Benchmark]
    public int Insert_DynamicColumns()
    {
        using var tx = ((ITransactionManager)_db).BeginTransaction();
        var written = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var entity = new DynamicInsertEntity { Id = ++_nextId, Name = "dynamic" };
            entity.Extra["age"] = 30;
            entity.Extra["city"] = "NY";
            written += _db.CreateInsertBuilder<DynamicInsertEntity>().Values(entity).Insert();
        }

        tx.Rollback();
        return written;
    }
}

/// <summary>The true store-less benchmark entity: only the two mapped columns, no dynamic-columns store.</summary>
[SqlTable("dynamic_insert_bench")]
public class StaticInsertEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>The benchmark entity: two mapped columns plus a dynamic-columns store.</summary>
[SqlTable("dynamic_insert_bench")]
public class DynamicInsertEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
