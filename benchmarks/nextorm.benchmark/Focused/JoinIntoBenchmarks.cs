using System.Collections.Concurrent;
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
/// D01 (#213) in-memory <c>JoinInto</c> harness: the denormalized assembly paths the 7-case acceptance
/// suite does not exercise. A scalar one-to-many declaration and a scalar many-to-many declaration
/// (junction + occurrence dedup) are measured against a fixed in-memory dataset, so the D02/D03
/// per-row assembly changes have a comparable before/after reading. Deliberately carries no
/// <c>acceptance</c> category, so it can never perturb the acceptance perf gate.
/// </summary>
/// <remarks>
/// Run explicitly with <c>--filter "*JoinIntoBenchmarks*"</c>. Each measured method loops
/// <see cref="InternalIterations"/> times over the same prepared/cached context, so the plan cache is
/// warm; the returned child total keeps the JIT from eliminating the work and proves the join returned
/// a non-degenerate result.
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class JoinIntoBenchmarks
{
    private const int ParentCount = 500;
    private const int ChildrenPerParent = 4;
    private const int InternalIterations = 5;

    private readonly InMemoryDataContext _ctx = new();

    public JoinIntoBenchmarks()
    {
        var parents = new List<JoinIntoBenchParent>(ParentCount);
        var children = new List<JoinIntoBenchChild>(ParentCount * ChildrenPerParent);
        var tags = new List<JoinIntoBenchTag>(ChildrenPerParent);
        var links = new List<JoinIntoBenchLink>(ParentCount * ChildrenPerParent);

        for (var p = 1; p <= ParentCount; p++)
        {
            parents.Add(new JoinIntoBenchParent { Id = p });

            for (var c = 0; c < ChildrenPerParent; c++)
            {
                var childId = (p * 10) + c;
                children.Add(new JoinIntoBenchChild { Id = childId, ParentId = p });
                links.Add(new JoinIntoBenchLink { ParentId = p, ChildId = c + 1 });
            }
        }

        for (var c = 1; c <= ChildrenPerParent; c++)
            tags.Add(new JoinIntoBenchTag { Id = c });

        _ctx.From<JoinIntoBenchParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasManyThrough<JoinIntoBenchTag, JoinIntoBenchLink, int, int>(
                    p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId))
            .WithData(parents);
        _ctx.From<JoinIntoBenchChild>().WithData(children);
        _ctx.From<JoinIntoBenchTag>().WithData(tags);
        _ctx.From<JoinIntoBenchLink>().WithData(links);

        // Fail loudly if the harness ever stops stitching: a degenerate empty result is not a baseline.
        if (JoinInto_OneToMany_ToList() <= 0 || JoinInto_ManyToMany_ToList() <= 0)
            throw new InvalidOperationException("The in-memory JoinInto benchmark returned no children.");
    }

    /// <summary>Scalar one-to-many: parent dedup + child grouping/materialization from denormalized rows.</summary>
    [Benchmark(Baseline = true)]
    public int JoinInto_OneToMany_ToList()
    {
        var total = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            var parents = _ctx.From<JoinIntoBenchParent>()
                .JoinInto(_ctx.From<JoinIntoBenchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .ToList();
            total += parents.Sum(p => p.Children.Count);
        }

        return total;
    }

    /// <summary>Scalar many-to-many: junction projection + occurrence dedup + full-key grouping.</summary>
    [Benchmark]
    public int JoinInto_ManyToMany_ToList()
    {
        var total = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            var parents = _ctx.From<JoinIntoBenchParent>()
                .JoinInto(_ctx.From<JoinIntoBenchTag>(), (p, t) => true, p => p.Tags)
                .ToList();
            total += parents.Sum(p => p.Tags.Count);
        }

        return total;
    }
}

public sealed class JoinIntoBenchParent
{
    public int Id { get; set; }
    public ICollection<JoinIntoBenchChild> Children { get; set; } = new List<JoinIntoBenchChild>();
    public ICollection<JoinIntoBenchTag> Tags { get; set; } = new List<JoinIntoBenchTag>();
}

public sealed class JoinIntoBenchChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}

public sealed class JoinIntoBenchTag
{
    public int Id { get; set; }
}

public sealed class JoinIntoBenchLink
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}

/// <summary>
/// D04 (#223) composite-junction M:N performance workload: matched scalar and composite many-to-many
/// <c>JoinInto</c> arms over the same SQLite dataset shape at several junction cardinalities. Each path
/// carries a cold preparation arm (a fresh command with the plan cache bypassed, so SQL generation and
/// mapper construction run every iteration), a warm preparation arm (a fresh command against a warm plan
/// cache) and a warm execution arm (cached prepare + SQLite execute + materialization + stitching).
/// </summary>
/// <remarks>
/// The composite arm has NO runnable HEAD baseline: before #223 the composite-junction link source fails
/// closed at preparation, so this workload exists only on the candidate. The scalar arm is the retained
/// baseline behavior and is the matched control. Run with <c>--filter "*JoinInto*Scalar*"</c> for the
/// scalar arms and <c>--filter "*JoinInto*"</c> for all of them. The class deliberately carries no
/// <c>acceptance</c> category, so it can never perturb the acceptance perf gate.
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class JoinIntoM2MBenchmarks
{
    private const int LinksPerParent = 4;
    private const int PrepareIterations = 40;
    private const int ExecutionIterations = 3;

    private static readonly ConcurrentDictionary<int, M2MDataset> Datasets = new();

    private M2MDataset _ds = null!;

    static JoinIntoM2MBenchmarks() => RegisterCompositeJunction();

    /// <summary>Junction cardinality: parents (and children), each carrying <see cref="LinksPerParent"/> junction rows.</summary>
    [Params(250, 1000, 4000)]
    public int ParentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _ds = Datasets.GetOrAdd(ParentCount, BuildDataset);
        Warm(_ds);
        _ds.Counter.Reset();
    }

    // --- scalar M:N (retained baseline behavior) -------------------------------------------------

    [Benchmark]
    public object JoinInto_Scalar_ManyToMany_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = Prepare(ScalarJoined(_ds), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark]
    public object JoinInto_Scalar_ManyToMany_PrepareWarm()
    {
        object prepared = null!;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = Prepare(ScalarJoined(_ds), _ds.Db, storeInCache: true);
        return prepared;
    }

    [Benchmark]
    public int JoinInto_Scalar_ManyToMany_ExecuteWarm()
    {
        _ds.Counter.Reset();
        var total = 0;
        for (var i = 0; i < ExecutionIterations; i++)
        {
            var parents = ScalarJoined(_ds).ToList();
            total += parents.Sum(p => p.Children.Count);
        }

        EnsureCommandCount(_ds, ExecutionIterations);
        return total;
    }

    // --- composite-junction M:N (candidate only; no HEAD baseline) --------------------------------

    [Benchmark]
    public object JoinInto_Composite_ManyToMany_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = Prepare(CompositeJoined(_ds), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark]
    public object JoinInto_Composite_ManyToMany_PrepareWarm()
    {
        object prepared = null!;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = Prepare(CompositeJoined(_ds), _ds.Db, storeInCache: true);
        return prepared;
    }

    [Benchmark]
    public int JoinInto_Composite_ManyToMany_ExecuteWarm()
    {
        _ds.Counter.Reset();
        var total = 0;
        for (var i = 0; i < ExecutionIterations; i++)
        {
            var parents = CompositeJoined(_ds).ToList();
            total += parents.Sum(p => p.Children.Count);
        }

        EnsureCommandCount(_ds, ExecutionIterations);
        return total;
    }

    // --- query shape -----------------------------------------------------------------------------

    private static EntityBuilder<JibScalarParent> ScalarParents(M2MDataset ds)
        => ds.Db.From<JibScalarParent>(b => b.HasManyThrough<JibScalarChild, JibScalarLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

    private static EntityBuilder<JibScalarParent> ScalarJoined(M2MDataset ds)
        => ScalarParents(ds).JoinInto(ds.Db.From<JibScalarChild>(), (p, c) => true, p => p.Children);

    private static EntityBuilder<JibCompositeParent> CompositeParents(M2MDataset ds)
        => ds.Db.From<JibCompositeParent>();

    private static EntityBuilder<JibCompositeParent> CompositeJoined(M2MDataset ds)
        => CompositeParents(ds).JoinInto(ds.Db.From<JibCompositeChild>(), (p, c) => true, p => p.Children);

    private static object Prepare<TEntity>(EntityBuilder<TEntity> joined, IDataContext db, bool storeInCache)
        => db.GetPreparedQueryCommand((dynamic)joined.CreateJoinIntoPairCommand(), false, storeInCache, CancellationToken.None);

    private static void Warm(M2MDataset ds)
    {
        var scalar = ScalarJoined(ds).ToList();
        var composite = CompositeJoined(ds).ToList();
        _ = Prepare(ScalarJoined(ds), ds.Db, storeInCache: true);
        _ = Prepare(CompositeJoined(ds), ds.Db, storeInCache: true);

        if (scalar.Sum(p => p.Children.Count) <= 0 || composite.Sum(p => p.Children.Count) <= 0)
            throw new InvalidOperationException("The JoinInto M:N benchmark returned no stitched children.");
    }

    private static void EnsureCommandCount(M2MDataset ds, int expected)
    {
        var observed = ds.Counter.Executing;
        if (observed != expected)
            throw new InvalidOperationException($"Expected {expected} executed commands but observed {observed}.");
    }

    // --- dataset ---------------------------------------------------------------------------------

    private static M2MDataset BuildDataset(int parentCount)
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"joininto-m2m-{parentCount}-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using (var create = conn.CreateCommand())
            {
                create.CommandText =
                    "create table jib_sc_parent (id integer primary key, name text);" +
                    "create table jib_sc_child (id integer primary key, name text);" +
                    "create table jib_sc_link (parent_id integer not null, child_id integer not null);" +
                    "create index ix_jib_sc_link_parent on jib_sc_link (parent_id);" +
                    "create table jib_cj_parent (tenant_id integer not null, id integer not null, name text, primary key (tenant_id, id));" +
                    "create table jib_cj_child (id integer primary key, name text);" +
                    "create table jib_cj_link (parent_id integer not null, parent_id2 integer not null, child_id integer not null);" +
                    "create index ix_jib_cj_link_parent on jib_cj_link (parent_id, parent_id2);";
                create.ExecuteNonQuery();
            }

            Seed(conn, parentCount);
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(path);
        var counter = new CountingInterceptor();
        builder.AddInterceptor(counter);
        var db = builder.CreateDataContext();
        ((IConnectionManager)db).EnsureConnectionOpen();

        return new M2MDataset(db, counter);
    }

    private static void Seed(SqliteConnection conn, int parentCount)
    {
        using var tx = conn.BeginTransaction();

        var parents = new List<object[]>(parentCount);
        var children = new List<object[]>(parentCount);
        var scalarLinks = new List<object[]>(parentCount * LinksPerParent);
        var compositeParents = new List<object[]>(parentCount);
        var compositeLinks = new List<object[]>(parentCount * LinksPerParent);

        for (var p = 1; p <= parentCount; p++)
        {
            // Spread the composite parents across tenants so both parent key components are non-trivial.
            var tenant = 1 + (p - 1) / 1000;
            parents.Add(new object[] { p, "p" + p });
            compositeParents.Add(new object[] { tenant, p, "p" + p });
            children.Add(new object[] { p, "c" + p });

            for (var k = 0; k < LinksPerParent; k++)
            {
                // A small modulus keeps (parent, child) pairs repeating, so the occurrence partition has repeats.
                var childId = ((p * 3) + (k * 7)) % parentCount + 1;
                scalarLinks.Add(new object[] { p, childId });
                compositeLinks.Add(new object[] { tenant, p, childId });
            }
        }

        InsertMany(conn, tx, "jib_sc_parent", "id, name", 2, parents);
        InsertMany(conn, tx, "jib_sc_child", "id, name", 2, children);
        InsertMany(conn, tx, "jib_sc_link", "parent_id, child_id", 2, scalarLinks);
        InsertMany(conn, tx, "jib_cj_parent", "tenant_id, id, name", 3, compositeParents);
        InsertMany(conn, tx, "jib_cj_child", "id, name", 2, children);
        InsertMany(conn, tx, "jib_cj_link", "parent_id, parent_id2, child_id", 3, compositeLinks);

        tx.Commit();
    }

    private static void InsertMany(
        SqliteConnection conn,
        SqliteTransaction tx,
        string table,
        string columns,
        int columnCount,
        List<object[]> rows)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        var placeholders = string.Join(", ", Enumerable.Range(0, columnCount).Select(i => "$" + i));
        cmd.CommandText = $"insert into {table} ({columns}) values ({placeholders});";

        var parameters = new SqliteParameter[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            parameters[i] = new SqliteParameter { ParameterName = "$" + i };
            cmd.Parameters.Add(parameters[i]);
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < columnCount; i++)
                parameters[i].Value = row[i];
            cmd.ExecuteNonQuery();
        }
    }

    // --- composite-junction metadata (the fluent surface is single-column only) -------------------

    private static void RegisterCompositeJunction()
    {
        var parentProps = Props<JibCompositeParent>();
        var linkProps = Props<JibCompositeLink>();
        var tenant = Prop(parentProps, nameof(JibCompositeParent.TenantId)).PropertyInfo;
        var id = Prop(parentProps, nameof(JibCompositeParent.Id)).PropertyInfo;
        var childKey = Prop(Props<JibCompositeChild>(), nameof(JibCompositeChild.Id)).PropertyInfo;
        var navigation = typeof(JibCompositeParent).GetProperty(nameof(JibCompositeParent.Children));
        var built = new EntityMetadataBuilder<JibCompositeParent>().Build();

        DataContextCache.Metadata[typeof(JibCompositeParent)] = new EntityMetadata(
            built.TableName,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            built.IsTableNameAuto,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(JibCompositeParent),
                    typeof(JibCompositeChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    id,
                    typeof(JibCompositeChild),
                    typeof(JibCompositeParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(JibCompositeLink),
                        ParentKey = [tenant, id],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(JibCompositeLink.ParentId)).PropertyInfo,
                            Prop(linkProps, nameof(JibCompositeLink.ParentId2)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(JibCompositeLink.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    private sealed class M2MDataset
    {
        public M2MDataset(IDataContext db, CountingInterceptor counter)
        {
            Db = db;
            Counter = counter;
        }

        public IDataContext Db { get; }

        public CountingInterceptor Counter { get; }
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

[SqlTable("jib_sc_parent")]
public sealed class JibScalarParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JibScalarChild> Children { get; set; } = new List<JibScalarChild>();
}

[SqlTable("jib_sc_child")]
public sealed class JibScalarChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("jib_sc_link")]
public sealed class JibScalarLink
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("jib_cj_parent")]
public sealed class JibCompositeParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JibCompositeChild> Children { get; set; } = new List<JibCompositeChild>();
}

[SqlTable("jib_cj_child")]
public sealed class JibCompositeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("jib_cj_link")]
public sealed class JibCompositeLink
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("parent_id2")]
    public int ParentId2 { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
