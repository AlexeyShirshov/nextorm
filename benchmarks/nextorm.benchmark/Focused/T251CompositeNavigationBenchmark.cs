using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T251 (#251) composite-relationship-key workload: the preparation and execution cost of a SQL join
/// that pairs <b>every</b> declared key component, at arity 1, 2, 4 and 8, for a direct relationship
/// (composite primary/foreign key) and for a junction relationship (M:N identity through a link table).
/// </summary>
/// <remarks>
/// <para>
/// Every "post" arm is declared through the new public A&#8211;J surface
/// (<see cref="EntityMetadataBuilder{T}.HasMany{TChild}(System.Linq.Expressions.Expression{System.Func{T,ICollection{TChild}}},System.Linq.Expressions.Expression{System.Func{TChild,object?}},System.Linq.Expressions.Expression{System.Func{T,object?}})"/>
/// and <see cref="EntityMetadataBuilder{T}.HasManyThrough{TChild,TJunction}(System.Linq.Expressions.Expression{System.Func{T,IEnumerable{TChild}}},System.Linq.Expressions.Expression{System.Func{T,object?}},System.Linq.Expressions.Expression{System.Func{TJunction,object?}},System.Linq.Expressions.Expression{System.Func{TChild,object?}},System.Linq.Expressions.Expression{System.Func{TJunction,object?}})"/>),
/// not through hand-written metadata. Before T251 a composite route failed closed, so these arms have
/// <b>no</b> pre-change reading and none is invented; <see cref="T251CompositeNavigationBenchmarkManualComposite"/>
/// is the pre-change comparison point (manual <see cref="IRelationshipMetadata"/> that already worked) and
/// <see cref="T251CompositeNavigationBenchmarkScalarControl"/> is the retained scalar navigation control.
/// </para>
/// <para>
/// Each scenario carries three arms: <c>PrepareCold</c> (fresh command, plan cache bypassed, so SQL
/// generation and row-mapper construction run every iteration), <c>ExecuteCached</c> (plan-cache hit +
/// SQLite execute + materialization + stitching) and <c>ExecutePrepared</c> (a command prepared once and
/// reused). Deliberately carries no <c>acceptance</c> category, so it can never perturb the acceptance
/// perf gate. Run with <c>--filter "*T251CompositeNavigationBenchmark*"</c>.
/// </para>
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T251CompositeNavigationBenchmark
{
    private T251BenchDataset _ds = null!;
    private T251PreparedPair _preparedDirect = null!;
    private T251PreparedPair _preparedJunction = null!;

    /// <summary>Number of key components paired by the measured relationship (1 is the scalar control arity).</summary>
    [Params(1, 2, 4, 8)]
    public int Arity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _ds = T251BenchDataset.Get();

        // Fail loudly if either route silently degenerates: an empty stitch is not a baseline.
        var direct = DirectJoinInto().ToList();
        if (direct.Count != T251BenchDataset.ParentCount
            || StitchedChildren(direct) != T251BenchDataset.ParentCount * T251BenchDataset.ChildrenPerParent)
            throw new InvalidOperationException(
                $"Direct arity {Arity} stitched {direct.Count} parents / {StitchedChildren(direct)} children.");

        var junction = JunctionJoinInto().ToList();
        if (junction.Count != T251BenchDataset.ParentCount
            || StitchedJunctionChildren(junction) != T251BenchDataset.ParentCount * T251BenchDataset.ChildrenPerParent)
            throw new InvalidOperationException(
                $"Junction arity {Arity} stitched {junction.Count} parents / {StitchedJunctionChildren(junction)} children.");

        _preparedDirect = T251BenchPrepare.PrepareJoinInto(DirectJoinInto(), _ds.Db, storeInCache: true);
        _preparedJunction = T251BenchPrepare.PrepareJoinInto(JunctionJoinInto(), _ds.Db, storeInCache: true);
    }

    // --- direct composite relationship (composite PK/FK) -----------------------------------------

    [Benchmark]
    public object Direct_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < T251BenchDataset.PrepareIterations; i++)
            prepared = T251BenchPrepare.PrepareJoinInto(DirectJoinInto(), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark]
    public int Direct_ExecuteCached()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecuteCachedRows(DirectJoinInto());
        return total;
    }

    [Benchmark]
    public int Direct_ExecutePrepared()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecutePreparedRows(_preparedDirect, _ds.Db);
        return total;
    }

    [Benchmark]
    public int Direct_ExecuteStitched()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += StitchedChildren(DirectJoinInto().ToList());
        return total;
    }

    // --- junction relationship (M:N identity through the link table) -----------------------------

    [Benchmark]
    public object Junction_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < T251BenchDataset.PrepareIterations; i++)
            prepared = T251BenchPrepare.PrepareJoinInto(JunctionJoinInto(), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark]
    public int Junction_ExecuteCached()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecuteCachedRows(JunctionJoinInto());
        return total;
    }

    [Benchmark]
    public int Junction_ExecutePrepared()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecutePreparedRows(_preparedJunction, _ds.Db);
        return total;
    }

    [Benchmark]
    public int Junction_ExecuteStitched()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += StitchedJunctionChildren(JunctionJoinInto().ToList());
        return total;
    }

    // --- query shape -----------------------------------------------------------------------------

    private EntityBuilder<T251BenchDirectParent> DirectJoinInto()
    {
        var children = _ds.Db.From<T251BenchDirectChild>();
        return Arity switch
        {
            1 => _ds.Db.From<T251BenchDirectParent>()
                .JoinInto(children, (p, c) => p.K1 == c.K1, p => p.Children1),
            2 => _ds.Db.From<T251BenchDirectParent>()
                .JoinInto(children, (p, c) => p.K1 == c.K1 && p.K2 == c.K2, p => p.Children2),
            4 => _ds.Db.From<T251BenchDirectParent>()
                .JoinInto(
                    children,
                    (p, c) => p.K1 == c.K1 && p.K2 == c.K2 && p.K3 == c.K3 && p.K4 == c.K4,
                    p => p.Children4),
            8 => _ds.Db.From<T251BenchDirectParent>()
                .JoinInto(
                    children,
                    (p, c) => p.K1 == c.K1 && p.K2 == c.K2 && p.K3 == c.K3 && p.K4 == c.K4
                        && p.K5 == c.K5 && p.K6 == c.K6 && p.K7 == c.K7 && p.K8 == c.K8,
                    p => p.Children8),
            _ => throw new ArgumentOutOfRangeException(nameof(Arity), Arity, "Measured arities are 1, 2, 4 and 8."),
        };
    }

    private EntityBuilder<T251BenchJunctionParent> JunctionJoinInto()
    {
        var children = _ds.Db.From<T251BenchJunctionChild>();
        return Arity switch
        {
            1 => _ds.Db.From<T251BenchJunctionParent>().JoinInto(children, (p, c) => true, p => p.Tags1),
            2 => _ds.Db.From<T251BenchJunctionParent>().JoinInto(children, (p, c) => true, p => p.Tags2),
            4 => _ds.Db.From<T251BenchJunctionParent>().JoinInto(children, (p, c) => true, p => p.Tags4),
            8 => _ds.Db.From<T251BenchJunctionParent>().JoinInto(children, (p, c) => true, p => p.Tags8),
            _ => throw new ArgumentOutOfRangeException(nameof(Arity), Arity, "Measured arities are 1, 2, 4 and 8."),
        };
    }

    private int StitchedChildren(List<T251BenchDirectParent> parents) => Arity switch
    {
        1 => parents.Sum(p => p.Children1.Count),
        2 => parents.Sum(p => p.Children2.Count),
        4 => parents.Sum(p => p.Children4.Count),
        8 => parents.Sum(p => p.Children8.Count),
        _ => throw new ArgumentOutOfRangeException(nameof(Arity)),
    };

    private int StitchedJunctionChildren(List<T251BenchJunctionParent> parents) => Arity switch
    {
        1 => parents.Sum(p => p.Tags1.Count),
        2 => parents.Sum(p => p.Tags2.Count),
        4 => parents.Sum(p => p.Tags4.Count),
        8 => parents.Sum(p => p.Tags8.Count),
        _ => throw new ArgumentOutOfRangeException(nameof(Arity)),
    };
}

/// <summary>
/// T251 scalar navigation control: the retained single-component implicit reference navigation
/// (<c>Select(child =&gt; child.Parent!.Name)</c>), measured with the same three arms as the composite
/// scenarios. It is the before/after control for the navigation-expansion changes: arity 1 keeps the
/// pre-T251 behavior, so a regression here is a regression of the shared path.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T251CompositeNavigationBenchmarkScalarControl
{
    private T251BenchDataset _ds = null!;
    private IPreparedQueryCommand<string?> _prepared = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ds = T251BenchDataset.Get();

        var names = Projection().ToList();
        if (names.Count != T251BenchDataset.ParentCount * T251BenchDataset.ChildrenPerParent)
            throw new InvalidOperationException($"Scalar navigation returned {names.Count} rows.");

        _prepared = T251BenchPrepare.Select(Projection(), _ds.Db, storeInCache: true);
    }

    [Benchmark]
    public object ScalarNavigation_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < T251BenchDataset.PrepareIterations; i++)
            prepared = T251BenchPrepare.Select(Projection(), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark(Baseline = true)]
    public int ScalarNavigation_ExecuteCached()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += Projection().ToList().Count;
        return total;
    }

    [Benchmark]
    public int ScalarNavigation_ExecutePrepared()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += _prepared.ToList(_ds.Db).Count;
        return total;
    }

    private QueryCommand<string?> Projection()
        => _ds.Db.From<T251BenchDirectChild>().Select(c => c.Parent1!.Name);
}

/// <summary>
/// T251 pre-change comparison point: the manual-metadata composite <c>JoinInto</c> that already ran
/// before T251 (hand-written <see cref="RelationshipMetadata"/> plus a
/// <see cref="RelationshipJunctionDeclaration"/> with a two-component parent key). This is the only
/// route with a genuine pre-change reading; it is not a substitute for the A&#8211;J declarations.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T251CompositeNavigationBenchmarkManualComposite
{
    private T251BenchDataset _ds = null!;
    private T251PreparedPair _prepared = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ds = T251BenchDataset.Get();

        var parents = Joined().ToList();
        var children = parents.Sum(p => p.Children.Count);
        if (parents.Count != T251BenchDataset.ParentCount
            || children != T251BenchDataset.ParentCount * T251BenchDataset.ChildrenPerParent)
            throw new InvalidOperationException(
                $"Manual-metadata composite JoinInto stitched {parents.Count} parents / {children} children.");

        _prepared = T251BenchPrepare.PrepareJoinInto(Joined(), _ds.Db, storeInCache: true);
    }

    [Benchmark]
    public object ManualCompositeJoinInto_PrepareCold()
    {
        object prepared = null!;
        for (var i = 0; i < T251BenchDataset.PrepareIterations; i++)
            prepared = T251BenchPrepare.PrepareJoinInto(Joined(), _ds.Db, storeInCache: false);
        return prepared;
    }

    [Benchmark(Baseline = true)]
    public int ManualCompositeJoinInto_ExecuteCached()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecuteCachedRows(Joined());
        return total;
    }

    [Benchmark]
    public int ManualCompositeJoinInto_ExecutePrepared()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += T251BenchPrepare.ExecutePreparedRows(_prepared, _ds.Db);
        return total;
    }

    [Benchmark]
    public int ManualCompositeJoinInto_ExecuteStitched()
    {
        var total = 0;
        for (var i = 0; i < T251BenchDataset.ExecutionIterations; i++)
            total += Joined().ToList().Sum(p => p.Children.Count);
        return total;
    }

    private EntityBuilder<T251BenchManualParent> Joined()
        => _ds.Db.From<T251BenchManualParent>()
            .JoinInto(_ds.Db.From<T251BenchManualChild>(), (p, c) => true, p => p.Children);
}

/// <summary>
/// Shared prepare/execute helpers for the JoinInto pair command. The pair command's result type is the
/// runtime-generated <c>JoinIntoProjectionFactory</c> projection, so no compile-time
/// <c>IPreparedQueryCommand&lt;TEntity&gt;</c> exists for it; the prepared command is carried through
/// <see cref="T251PreparedPair"/> and executed through the internal enumerator seam. The cached and
/// prepared arms both drain an enumerator, so the only difference between them is the plan-cache lookup.
/// </summary>
internal static class T251BenchPrepare
{
    /// <summary>Prepares the denormalized pair command, cached or with the plan cache bypassed.</summary>
    public static T251PreparedPair PrepareJoinInto<T>(EntityBuilder<T> builder, IDataContext db, bool storeInCache)
        => new(db.GetPreparedQueryCommand((dynamic)builder.CreateJoinIntoPairCommand(), true, storeInCache, CancellationToken.None));

    /// <summary>Fresh pair command through the plan cache, then execute and drain every row.</summary>
    public static int ExecuteCachedRows<T>(EntityBuilder<T> builder)
        => Drain(((dynamic)builder.CreateJoinIntoPairCommand()).ToEnumerable());

    /// <summary>Re-execute the command prepared once, then drain every row.</summary>
    public static int ExecutePreparedRows(T251PreparedPair prepared, IDataContext db)
        => DrainEnumerator((System.Collections.IEnumerator)prepared.CreateEnumerator(db));

    /// <summary>Prepares a plain select command (the scalar navigation control).</summary>
    public static IPreparedQueryCommand<T> Select<T>(QueryCommand<T> command, IDataContext db, bool storeInCache)
        => db.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static int Drain(object sequence)
        => DrainEnumerator(((System.Collections.IEnumerable)sequence).GetEnumerator());

    private static int DrainEnumerator(System.Collections.IEnumerator enumerator)
    {
        try
        {
            var rows = 0;
            while (enumerator.MoveNext())
                rows++;
            return rows;
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}

/// <summary>
/// A prepared JoinInto pair command plus the one interface method that creates its enumerator. The
/// prepared command implements <c>IPreparedQueryCommand&lt;TProjection&gt;</c> explicitly and the
/// projection type only exists at runtime, so the enumerator is created through that interface method
/// (resolved once, then invoked per execution).
/// </summary>
internal sealed class T251PreparedPair
{
    private readonly System.Reflection.MethodInfo _createEnumerator;

    public T251PreparedPair(object command)
    {
        Command = command;
        var contract = command.GetType().GetInterfaces().First(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPreparedQueryCommand<>));
        _createEnumerator = contract.GetMethod(nameof(CreateEnumerator), [typeof(IDataContext), typeof(object[])])
            ?? throw new MissingMethodException(contract.FullName, nameof(CreateEnumerator));
    }

    public object Command { get; }

    public object CreateEnumerator(IDataContext db)
        => _createEnumerator.Invoke(Command, [db, Array.Empty<object?>()])!;
}

/// <summary>
/// Shared SQLite dataset for the T251 composite benchmarks: a fixed 200-parent / 4-children-per-parent
/// graph whose key component <c>k1</c> alone is unique, so a prefix join of any measured arity is still
/// a valid, non-degenerate join while every component stays non-trivial (<c>kN = parent * (N + 1)</c>).
/// Built once per process; the metadata registration of the "post" scenarios goes through the public
/// A&#8211;J fluent surface instead of hand-written metadata.
/// </summary>
internal sealed class T251BenchDataset
{
    public const int ParentCount = 200;
    public const int ChildrenPerParent = 4;
    public const int PrepareIterations = 40;
    public const int ExecutionIterations = 3;

    private static readonly Lazy<T251BenchDataset> Shared = new(Build);

    private T251BenchDataset(IDataContext db) => Db = db;

    public IDataContext Db { get; }

    public static T251BenchDataset Get() => Shared.Value;

    private static T251BenchDataset Build()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"t251-composite-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            CreateSchema(conn);
            Seed(conn);
        }

        RegisterMetadata();

        var builder = new DataContextBuilder();
        builder.UseSqlite(path);
        var db = builder.CreateDataContext();
        ((IConnectionManager)db).EnsureConnectionOpen();
        return new T251BenchDataset(db);
    }

    // --- schema ----------------------------------------------------------------------------------

    private const string KeyColumns = "k1, k2, k3, k4, k5, k6, k7, k8";

    private const string KeyDdl =
        "k1 integer not null, k2 integer not null, k3 integer not null, k4 integer not null, " +
        "k5 integer not null, k6 integer not null, k7 integer not null, k8 integer not null";

    private static void CreateSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"create table t251_dir_parent ({KeyDdl}, name text);" +
            $"create table t251_dir_child (id integer primary key, {KeyDdl}, name text);" +
            "create index ix_t251_dir_child_key on t251_dir_child (k1, k2, k3, k4, k5, k6, k7, k8);" +
            $"create table t251_jct_parent ({KeyDdl}, name text);" +
            "create table t251_jct_child (id integer primary key, name text);" +
            $"create table t251_jct_link ({KeyDdl}, child_id integer not null);" +
            "create index ix_t251_jct_link_key on t251_jct_link (k1, k2, k3, k4, k5, k6, k7, k8);" +
            "create table t251_man_parent (tenant_id integer not null, id integer not null, name text, primary key (tenant_id, id));" +
            "create table t251_man_child (id integer primary key, name text);" +
            "create table t251_man_link (parent_tenant_id integer not null, parent_id integer not null, child_id integer not null);" +
            "create index ix_t251_man_link_key on t251_man_link (parent_tenant_id, parent_id);";
        cmd.ExecuteNonQuery();
    }

    private static void Seed(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();

        var directParents = new List<object[]>(ParentCount);
        var directChildren = new List<object[]>(ParentCount * ChildrenPerParent);
        var junctionParents = new List<object[]>(ParentCount);
        var junctionChildren = new List<object[]>(ParentCount);
        var junctionLinks = new List<object[]>(ParentCount * ChildrenPerParent);
        var manualParents = new List<object[]>(ParentCount);
        var manualChildren = new List<object[]>(ParentCount);
        var manualLinks = new List<object[]>(ParentCount * ChildrenPerParent);

        for (var p = 1; p <= ParentCount; p++)
        {
            var keys = KeyOf(p);
            directParents.Add([.. keys, "p" + p]);
            junctionParents.Add([.. keys, "p" + p]);
            junctionChildren.Add([p, "c" + p]);

            for (var c = 0; c < ChildrenPerParent; c++)
            {
                directChildren.Add([(p * 10) + c, .. keys, "c" + c]);
                // Deliberately repeating child ids keep the M:N occurrence partition non-trivial.
                junctionLinks.Add([.. keys, ((p * 3) + (c * 7)) % ParentCount + 1]);
            }

            var tenant = 1 + ((p - 1) / 50);
            var id = ((p - 1) % 50) + 1;
            manualParents.Add([tenant, id, "p" + p]);
            manualChildren.Add([p, "c" + p]);
            for (var c = 0; c < ChildrenPerParent; c++)
                manualLinks.Add([tenant, id, ((p * 3) + (c * 7)) % ParentCount + 1]);
        }

        InsertMany(conn, tx, "t251_dir_parent", $"{KeyColumns}, name", 9, directParents);
        InsertMany(conn, tx, "t251_dir_child", $"id, {KeyColumns}, name", 10, directChildren);
        InsertMany(conn, tx, "t251_jct_parent", $"{KeyColumns}, name", 9, junctionParents);
        InsertMany(conn, tx, "t251_jct_child", "id, name", 2, junctionChildren);
        InsertMany(conn, tx, "t251_jct_link", $"{KeyColumns}, child_id", 9, junctionLinks);
        InsertMany(conn, tx, "t251_man_parent", "tenant_id, id, name", 3, manualParents);
        InsertMany(conn, tx, "t251_man_child", "id, name", 2, manualChildren);
        InsertMany(conn, tx, "t251_man_link", "parent_tenant_id, parent_id, child_id", 3, manualLinks);

        tx.Commit();
    }

    private static object[] KeyOf(int parent)
    {
        var keys = new object[8];
        for (var i = 0; i < 8; i++)
            keys[i] = parent * (i + 2);
        return keys;
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

    // --- metadata: public A-J declarations for the "post" scenarios ------------------------------

    private static void RegisterMetadata()
    {
        RegisterDirect();
        RegisterJunction();
        RegisterManualComposite();
    }

    private static void RegisterDirect()
    {
        var parent = new EntityMetadataBuilder<T251BenchDirectParent>();
        parent.HasMany(p => p.Children1, c => new { c.K1 }, p => new { p.K1 })
            .HasMany(p => p.Children2, c => new { c.K1, c.K2 }, p => new { p.K1, p.K2 })
            .HasMany(p => p.Children4, c => new { c.K1, c.K2, c.K3, c.K4 }, p => new { p.K1, p.K2, p.K3, p.K4 })
            .HasMany(
                p => p.Children8,
                c => new { c.K1, c.K2, c.K3, c.K4, c.K5, c.K6, c.K7, c.K8 },
                p => new { p.K1, p.K2, p.K3, p.K4, p.K5, p.K6, p.K7, p.K8 });
        DataContextCache.Metadata[typeof(T251BenchDirectParent)] = parent.Build();

        var child = new EntityMetadataBuilder<T251BenchDirectChild>();
        child.HasOne(c => c.Parent1, c => new { c.K1 }, p => new { p.K1 });
        DataContextCache.Metadata[typeof(T251BenchDirectChild)] = child.Build();
    }

    private static void RegisterJunction()
    {
        var parent = new EntityMetadataBuilder<T251BenchJunctionParent>();
        parent.HasManyThrough<T251BenchJunctionChild, T251BenchJunctionLink>(
                p => p.Tags1,
                p => new { p.K1 },
                l => new { l.K1 },
                c => new { c.Id },
                l => new { l.ChildId })
            .HasManyThrough<T251BenchJunctionChild, T251BenchJunctionLink>(
                p => p.Tags2,
                p => new { p.K1, p.K2 },
                l => new { l.K1, l.K2 },
                c => new { c.Id },
                l => new { l.ChildId })
            .HasManyThrough<T251BenchJunctionChild, T251BenchJunctionLink>(
                p => p.Tags4,
                p => new { p.K1, p.K2, p.K3, p.K4 },
                l => new { l.K1, l.K2, l.K3, l.K4 },
                c => new { c.Id },
                l => new { l.ChildId })
            .HasManyThrough<T251BenchJunctionChild, T251BenchJunctionLink>(
                p => p.Tags8,
                p => new { p.K1, p.K2, p.K3, p.K4, p.K5, p.K6, p.K7, p.K8 },
                l => new { l.K1, l.K2, l.K3, l.K4, l.K5, l.K6, l.K7, l.K8 },
                c => new { c.Id },
                l => new { l.ChildId });
        DataContextCache.Metadata[typeof(T251BenchJunctionParent)] = parent.Build();
        DataContextCache.Metadata[typeof(T251BenchJunctionChild)] = new EntityMetadataBuilder<T251BenchJunctionChild>().Build();
        DataContextCache.Metadata[typeof(T251BenchJunctionLink)] = new EntityMetadataBuilder<T251BenchJunctionLink>().Build();
    }

    /// <summary>
    /// The pre-change comparison point: hand-written metadata, exactly the shape the D04 manual M:N
    /// benchmark uses (a two-component parent key and a two-column junction foreign key), not a
    /// declaration through A&#8211;J.
    /// </summary>
    private static void RegisterManualComposite()
    {
        var built = new EntityMetadataBuilder<T251BenchManualParent>().Build();
        var parentProps = built.Properties;
        var linkProps = new EntityMetadataBuilder<T251BenchManualLink>().Build().Properties;
        var tenant = Prop(parentProps, nameof(T251BenchManualParent.TenantId)).PropertyInfo;
        var id = Prop(parentProps, nameof(T251BenchManualParent.Id)).PropertyInfo;
        var childKey = Prop(new EntityMetadataBuilder<T251BenchManualChild>().Build().Properties,
            nameof(T251BenchManualChild.Id)).PropertyInfo;
        var navigation = typeof(T251BenchManualParent).GetProperty(nameof(T251BenchManualParent.Children));

        DataContextCache.Metadata[typeof(T251BenchManualParent)] = new EntityMetadata(
            built.TableName,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            built.IsTableNameAuto,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(T251BenchManualParent),
                    typeof(T251BenchManualChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    id,
                    typeof(T251BenchManualChild),
                    typeof(T251BenchManualParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(T251BenchManualLink),
                        ParentKey = [tenant, id],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(T251BenchManualLink.ParentTenantId)).PropertyInfo,
                            Prop(linkProps, nameof(T251BenchManualLink.ParentId)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(T251BenchManualLink.ChildId)).PropertyInfo],
                    }),
            ]);

        DataContextCache.Metadata[typeof(T251BenchManualChild)] = new EntityMetadataBuilder<T251BenchManualChild>().Build();
        DataContextCache.Metadata[typeof(T251BenchManualLink)] = new EntityMetadataBuilder<T251BenchManualLink>().Build();
    }

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);
}

// --- entities ------------------------------------------------------------------------------------

[SqlTable("t251_dir_parent")]
public sealed class T251BenchDirectParent
{
    [Key]
    [Column("k1")]
    public int K1 { get; set; }

    [Key]
    [Column("k2")]
    public int K2 { get; set; }

    [Key]
    [Column("k3")]
    public int K3 { get; set; }

    [Key]
    [Column("k4")]
    public int K4 { get; set; }

    [Key]
    [Column("k5")]
    public int K5 { get; set; }

    [Key]
    [Column("k6")]
    public int K6 { get; set; }

    [Key]
    [Column("k7")]
    public int K7 { get; set; }

    [Key]
    [Column("k8")]
    public int K8 { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T251BenchDirectChild> Children1 { get; set; } = new List<T251BenchDirectChild>();

    public ICollection<T251BenchDirectChild> Children2 { get; set; } = new List<T251BenchDirectChild>();

    public ICollection<T251BenchDirectChild> Children4 { get; set; } = new List<T251BenchDirectChild>();

    public ICollection<T251BenchDirectChild> Children8 { get; set; } = new List<T251BenchDirectChild>();
}

[SqlTable("t251_dir_child")]
public sealed class T251BenchDirectChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("k1")]
    public int K1 { get; set; }

    [Column("k2")]
    public int K2 { get; set; }

    [Column("k3")]
    public int K3 { get; set; }

    [Column("k4")]
    public int K4 { get; set; }

    [Column("k5")]
    public int K5 { get; set; }

    [Column("k6")]
    public int K6 { get; set; }

    [Column("k7")]
    public int K7 { get; set; }

    [Column("k8")]
    public int K8 { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public T251BenchDirectParent? Parent1 { get; set; }
}

[SqlTable("t251_jct_parent")]
public sealed class T251BenchJunctionParent
{
    [Key]
    [Column("k1")]
    public int K1 { get; set; }

    [Key]
    [Column("k2")]
    public int K2 { get; set; }

    [Key]
    [Column("k3")]
    public int K3 { get; set; }

    [Key]
    [Column("k4")]
    public int K4 { get; set; }

    [Key]
    [Column("k5")]
    public int K5 { get; set; }

    [Key]
    [Column("k6")]
    public int K6 { get; set; }

    [Key]
    [Column("k7")]
    public int K7 { get; set; }

    [Key]
    [Column("k8")]
    public int K8 { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T251BenchJunctionChild> Tags1 { get; set; } = new List<T251BenchJunctionChild>();

    public ICollection<T251BenchJunctionChild> Tags2 { get; set; } = new List<T251BenchJunctionChild>();

    public ICollection<T251BenchJunctionChild> Tags4 { get; set; } = new List<T251BenchJunctionChild>();

    public ICollection<T251BenchJunctionChild> Tags8 { get; set; } = new List<T251BenchJunctionChild>();
}

[SqlTable("t251_jct_child")]
public sealed class T251BenchJunctionChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("t251_jct_link")]
public sealed class T251BenchJunctionLink
{
    [Column("k1")]
    public int K1 { get; set; }

    [Column("k2")]
    public int K2 { get; set; }

    [Column("k3")]
    public int K3 { get; set; }

    [Column("k4")]
    public int K4 { get; set; }

    [Column("k5")]
    public int K5 { get; set; }

    [Column("k6")]
    public int K6 { get; set; }

    [Column("k7")]
    public int K7 { get; set; }

    [Column("k8")]
    public int K8 { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("t251_man_parent")]
public sealed class T251BenchManualParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<T251BenchManualChild> Children { get; set; } = new List<T251BenchManualChild>();
}

[SqlTable("t251_man_child")]
public sealed class T251BenchManualChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("t251_man_link")]
public sealed class T251BenchManualLink
{
    [Column("parent_tenant_id")]
    public int ParentTenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
