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
/// Many-to-many <c>JoinInto</c> against SQLite (#135), the per-row denormalized path the 7-case
/// acceptance suite does not exercise: the junction is projected as a derived <c>row_number()</c> link
/// subquery between the parent and two flat joins, and the list terminal stitches the denormalized rows
/// (parent dedup + child grouping/materialization).
/// <para>
/// The harness exposes no prepared stitched <c>JoinInto</c> command: <c>Prepare()</c> goes through
/// <c>ToParentCommand()</c>, which strips the <c>JoinInto</c> joins, so a prepared/cached ratio cannot be
/// derived here. This is a single warmed (plan-cached) case, per the task's fallback.
/// </para>
/// The dataset is seeded once in the constructor: <see cref="ParentCount"/> parents, <see cref="ChildCount"/>
/// children and <see cref="LinksPerParent"/> junction rows per parent (~4000 link rows), with junction
/// duplicates allowed so the occurrence partition has repeats to collapse.
/// The executed-command count is captured per measurement and asserted, mirroring the eager-loading case.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkManyToManyJoinInto
{
    private const int ParentCount = 500;
    private const int ChildCount = 400;
    private const int LinksPerParent = 8;
    private const int InsertBatchSize = 250;

    private readonly IDataContext _db;
    private readonly CountingInterceptor _counter = new();
    private readonly string _path;

    private int _commandCount;

    public SqliteBenchmarkManyToManyJoinInto()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"mn-joininto-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText =
                "create table mn_parent (id integer primary key, name text);" +
                "create table mn_child (id integer primary key, name text);" +
                "create table mn_link (id integer primary key, parent_id integer not null, child_id integer not null);" +
                "create index ix_mn_link_parent on mn_link (parent_id);" +
                "create index ix_mn_link_child on mn_link (child_id);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        builder.AddInterceptor(_counter);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Register the many-to-many mapping before the first insert: From(cfg) only runs the config when
        // the type is first mapped, so seeding first would cache a parent metadata without the relationship.
        _ = Parents();

        Seed();

        // Warm the plan cache so the measured run exercises the cached path only, and fail loudly if the
        // M:N query ever stops returning stitched children (a degenerate empty result is not evidence).
        if (JoinInto_ManyToMany_ToList() <= 0)
            throw new InvalidOperationException("The many-to-many JoinInto query returned no children.");
        _ = JoinInto_ManyToMany_ToList_Half();
    }

    private EntityBuilder<MnBenchParent> Parents() =>
        _db.From<MnBenchParent>(b => b.HasManyThrough<MnBenchChild, MnBenchLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

    private void Seed()
    {
        var parents = new List<MnBenchParent>(ParentCount);
        var children = new List<MnBenchChild>(ChildCount);
        var links = new List<MnBenchLink>(ParentCount * LinksPerParent);

        for (var p = 1; p <= ParentCount; p++)
            parents.Add(new MnBenchParent { Id = p, Name = "p" + p });

        for (var c = 1; c <= ChildCount; c++)
            children.Add(new MnBenchChild { Id = c, Name = "c" + c });

        for (var p = 1; p <= ParentCount; p++)
        {
            for (var k = 0; k < LinksPerParent; k++)
            {
                // A small modulus keeps (parent, child) pairs repeating across k, so duplicate junction
                // rows exist and the occurrence partition has repeats to collapse.
                var childId = ((p * 3 + k * 17) % ChildCount) + 1;
                links.Add(new MnBenchLink { Id = p * 10 + k, ParentId = p, ChildId = childId });
            }
        }

        InsertInBatches(parents);
        InsertInBatches(children);
        InsertInBatches(links);
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

    /// <summary>Full M:N <c>JoinInto</c> list path: link derivation + <c>row_number()</c> + two joins + stitching.</summary>
    [Benchmark(Baseline = true)]
    public int JoinInto_ManyToMany_ToList()
    {
        _counter.Reset();
        var parents = Parents()
            .JoinInto(_db.From<MnBenchChild>(), (p, c) => c.Id > 0, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();
        _commandCount = _counter.Executing;
        EnsureCommandCount(1);
        return parents.Sum(p => p.Children.Count);
    }

    /// <summary>
    /// Scaling probe: the same shape over half the parents (~2000 link rows instead of ~4000). A roughly
    /// proportional drop in Mean/Allocated shows the measured cost is driven by the per-row stitching.
    /// </summary>
    [Benchmark]
    public int JoinInto_ManyToMany_ToList_Half()
    {
        _counter.Reset();
        var parents = Parents()
            .Where(p => p.Id <= ParentCount / 2)
            .JoinInto(_db.From<MnBenchChild>(), (p, c) => c.Id > 0, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();
        _commandCount = _counter.Executing;
        EnsureCommandCount(1);
        return parents.Sum(p => p.Children.Count);
    }

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

[SqlTable("mn_parent")]
public sealed class MnBenchParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MnBenchChild> Children { get; } = new List<MnBenchChild>();
}

[SqlTable("mn_child")]
public sealed class MnBenchChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("mn_link")]
public sealed class MnBenchLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
