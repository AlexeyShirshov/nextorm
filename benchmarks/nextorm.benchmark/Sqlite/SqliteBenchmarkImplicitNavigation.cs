using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Focused #148-B D10 probe: implicit declared-navigation lowering against the explicit native equivalent
/// for SQLite, measured cold (SQL is regenerated every iteration, so the navigation rewrite runs) and
/// warm (the plan cache absorbs the rewrite). It deliberately carries no <c>acceptance</c> category, so it
/// can never perturb the 7-case acceptance perf gate.
/// <para>
/// Reference: <c>c.Parent.Name</c> (injected single <c>LEFT JOIN</c>) vs an explicit
/// <c>Join(...)</c>. Collection: <c>p.Children.Count()</c> (correlated <c>count_big</c> subquery) vs an
/// explicit <c>GroupBy(c =&gt; c.ParentId)</c> count. Both arms execute, so a degenerate empty result
/// fails the run loudly.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkImplicitNavigation
{
    private const int Iterations = 50;
    private const int ParentCount = 200;
    private const int ChildCount = 1000;

    private readonly IDataContext _db;
    private readonly string _path;

    public SqliteBenchmarkImplicitNavigation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"nav-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText =
                "create table bench_nav_parent (id integer primary key, name text);" +
                "create table bench_nav_child (id integer primary key, parent_id integer not null, name text);" +
                "create index ix_bench_nav_child_parent on bench_nav_child (parent_id);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Register both declared navigations, then seed. The mapping must exist before the first insert.
        _db.From<BenchNavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        _db.From<BenchNavParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        Seed();

        // Warm the plan caches and fail loudly if either implicit arm returns nothing.
        if (Reference_Implicit_Warm() <= 0 || Collection_Implicit_Warm() <= 0)
            throw new InvalidOperationException("An implicit-navigation benchmark arm returned no rows.");
    }

    private void Seed()
    {
        for (var p = 1; p <= ParentCount; p++)
            _db.CreateInsertBuilder<BenchNavParent>().Values(new BenchNavParent { Id = p, Name = "p" + p }).Insert();

        for (var c = 1; c <= ChildCount; c++)
        {
            var parentId = (c % ParentCount) + 1;
            _db.CreateInsertBuilder<BenchNavChild>()
                .Values(new BenchNavChild { Id = c, ParentId = parentId, Name = "c" + c })
                .Insert();
        }
    }

    [Benchmark(Baseline = true)]
    public int Reference_Implicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            foreach (var row in _db.From<BenchNavChild>().Select(c => new { c.Id, Name = c.Parent!.Name }).ToList())
                sum += row.Id;
        return sum;
    }

    [Benchmark]
    public int Reference_Explicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            foreach (var row in _db.From<BenchNavChild>()
                .Join(_db.From<BenchNavParent>(), (c, p) => c.ParentId == p.Id)
                .Select(x => new { x.Item1.Id, Name = x.Item2.Name })
                .ToList())
                sum += row.Id;
        return sum;
    }

    [Benchmark]
    public int Reference_Implicit_Cold()
    {
        var built = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _db.From<BenchNavChild>().Select(c => new { c.Id, Name = c.Parent!.Name });
            _ = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
            built++;
        }
        return built;
    }

    [Benchmark]
    public int Collection_Implicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            foreach (var row in _db.From<BenchNavParent>().Select(p => new { p.Id, C = p.Children.Count() }).ToList())
                sum += row.C;
        return sum;
    }

    [Benchmark]
    public int Collection_Explicit_Warm()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
            sum += _db.From<BenchNavChild>()
                .GroupBy(c => c.ParentId)
                .Select(c => new { c.ParentId, C = SqlFunctions.Sql.count() })
                .ToList().Count;
        return sum;
    }

    [Benchmark]
    public int Collection_Implicit_Cold()
    {
        var built = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _db.From<BenchNavParent>().Select(p => new { p.Id, C = p.Children.Count() });
            _ = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
            built++;
        }
        return built;
    }
}

[SqlTable("bench_nav_parent")]
public sealed class BenchNavParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<BenchNavChild> Children { get; } = new List<BenchNavChild>();
}

[SqlTable("bench_nav_child")]
public sealed class BenchNavChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public BenchNavParent? Parent { get; set; }
}
