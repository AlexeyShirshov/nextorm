using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>Timing comparison for constructor-based class and readonly-struct projections over SQLite.</summary>
[MemoryDiagnoser]
[InProcess]
public class Issue26StructProjectionBenchmark
{
    private const string Table = "issue26_numeric_projection";
    private SqliteDataContext _context = null!;

    [Params(1000, 10000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm.issue26.{Guid.NewGuid():N}.db");
        _context = new SqliteDataContext($"Data Source='{path}';Pooling=False", new DataContextBuilder());
        _context.EnsureConnectionOpen();
        using (var command = _context.CreateCommand($"create table {Table} (id integer primary key); with recursive numbers(id) as (select 1 union all select id + 1 from numbers where id < 10000) insert into {Table} select id from numbers;"))
            command.ExecuteNonQuery();

        var classes = QueryClass();
        var structs = QueryStruct();
        if (classes.Count != Count || structs.Count != Count || classes[0].Id != 1 || structs[0].Id != 1 || classes[^1].Id != Count || structs[^1].Id != Count)
            throw new InvalidOperationException("Projection correctness check failed.");
    }

    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    [Benchmark(Baseline = true)]
    public List<ClassProjection> ClassConstructorProjection() => QueryClass();

    [Benchmark]
    public List<StructProjection> ReadonlyStructConstructorProjection() => QueryStruct();

    private List<ClassProjection> QueryClass() => _context.From<NumericRow>()
        .Where(row => row.Id <= Count)
        .OrderBy(row => row.Id)
        .Select(row => new ClassProjection(row.Id))
        .ToList();

    private List<StructProjection> QueryStruct() => _context.From<NumericRow>()
        .Where(row => row.Id <= Count)
        .OrderBy(row => row.Id)
        .Select(row => new StructProjection(row.Id))
        .ToList();

    [SqlTable(Table)]
    public sealed class NumericRow
    {
        [System.ComponentModel.DataAnnotations.Key, System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public int Id { get; set; }
    }

    public sealed class ClassProjection(int id)
    {
        public int Id { get; } = id;
    }

    public readonly record struct StructProjection(int Id);
}
