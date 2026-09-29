using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Eager loading (<c>LoadWith</c>): the split-query default (chunked <c>IN</c> child statement, #95)
/// against single-query stitching (<c>AsSingleQuery</c>, #107).
/// The dataset is seeded once per benchmark case: <see cref="ParentCount"/> parents with
/// <see cref="ChildrenPerParent"/> children each, so the measured work is the query, not the setup.
/// The command count is captured per case in an instance field and asserted inside the benchmark, so a
/// shared/parallel value can never be reported as if it belonged to another case.
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkEagerLoading
{
    private const int ParentCount = 500;
    private const int ChildrenPerParent = 4;
    private const int InsertBatchSize = 250;

    private readonly IDataContext _db;
    private readonly CountingInterceptor _counter = new();
    private readonly string _path;

    // Per-benchmark-case command count, captured by the measured invocation itself.
    private int _commandCount;

    public SqliteBenchmarkEagerLoading()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"eager-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText =
                "create table eager_parent (id integer primary key, name text);" +
                "create table eager_child (id integer primary key, parent_id int not null, name text);" +
                "create index ix_eager_child_parent on eager_child (parent_id);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        builder.AddInterceptor(_counter);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed();

        // Warm both plan caches so the measured runs exercise the cached path only.
        _ = LoadWith_Split();
        _ = LoadWith_Single();
    }

    private void Seed()
    {
        var parents = new List<EagerBenchParent>(ParentCount);
        var children = new List<EagerBenchChild>(ParentCount * ChildrenPerParent);
        for (var p = 1; p <= ParentCount; p++)
        {
            parents.Add(new EagerBenchParent { Id = p, Name = "p" + p });
            for (var c = 0; c < ChildrenPerParent; c++)
                children.Add(new EagerBenchChild { Id = p * 10 + c, ParentId = p, Name = "c" + p + "_" + c });
        }

        InsertInBatches(parents);
        InsertInBatches(children);
    }

    private void InsertInBatches<T>(IReadOnlyList<T> rows)
    {
        for (var offset = 0; offset < rows.Count; offset += InsertBatchSize)
        {
            var count = Math.Min(InsertBatchSize, rows.Count - offset);
            var batch = new T[count];
            for (var i = 0; i < count; i++)
                batch[i] = rows[offset + i];

            _db.InsertInto<T>().Values(batch).Insert();
        }
    }

    /// <summary>Split-query default. Parent query + one chunked child <c>IN</c> query = 2 commands.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("eager-loading")]
    public int LoadWith_Split()
    {
        _counter.Reset();
        var parents = _db.From<EagerBenchParent>()
            .LoadWith(p => p.Children, c => c.From<EagerBenchChild>(), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();
        _commandCount = _counter.Executing;
        EnsureCommandCount(2);
        return parents.Sum(p => p.Children.Count);
    }

    /// <summary>Single denormalized command with the child join = 1 command.</summary>
    [Benchmark]
    [BenchmarkCategory("eager-loading")]
    public int LoadWith_Single()
    {
        _counter.Reset();
        var parents = _db.From<EagerBenchParent>()
            .LoadWith(p => p.Children, c => c.From<EagerBenchChild>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .OrderBy(p => p.Id)
            .ToList();
        _commandCount = _counter.Executing;
        EnsureCommandCount(1);
        return parents.Sum(p => p.Children.Count);
    }

    /// <summary>
    /// Fails the measured invocation when it did not issue exactly <paramref name="expected"/> commands.
    /// The count is read from this instance's field, so a stale or parallel value can never be reported.
    /// </summary>
    private void EnsureCommandCount(int expected)
    {
        if (_commandCount != expected)
            throw new InvalidOperationException($"Expected {expected} executed commands but observed {_commandCount}.");
    }

    private sealed class CountingInterceptor : IQueryInterceptor
    {
        private int _executing;

        public int Executing => Volatile.Read(ref _executing);

        public void Reset() => Volatile.Write(ref _executing, 0);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);
    }
}

[SqlTable("eager_parent")]
public sealed class EagerBenchParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<EagerBenchChild> Children { get; } = new List<EagerBenchChild>();
}

[SqlTable("eager_child")]
public sealed class EagerBenchChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
