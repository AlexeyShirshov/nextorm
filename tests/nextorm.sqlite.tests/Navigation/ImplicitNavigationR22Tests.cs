using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B R2.2: occurrence identity by root + full navigation path + lexical scope, multi-hop
/// reference chains with absence at every hop, self-reference and dual same-typed reference paths.
/// </summary>
public class ImplicitNavigationR22Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static (SqliteDataContext Ctx, string Path) CreateDb(string schema)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-r22-nav-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private const string ChainSchema =
        "create table r22_grand (id integer primary key, label text);" +
        "create table r22_mid (id integer primary key, grand_id integer, name text);" +
        "create table r22_child (id integer primary key, mid_id integer, name text);" +
        "insert into r22_grand (id, label) values (1, 'g1'), (2, 'g2');" +
        "insert into r22_mid (id, grand_id, name) values (10, 1, 'm10'), (11, 999, 'm11-dangling-grand');" +
        "insert into r22_child (id, mid_id, name) values (100, 10, 'c100'), (101, 11, 'c101'), (102, 999, 'c102-dangling-mid');";

    private static (SqliteDataContext Ctx, string Path) CreateChain()
    {
        var (ctx, path) = CreateDb(ChainSchema);
        ctx.From<R22Grand>();
        ctx.From<R22Mid>(b => b.HasOne(m => m.Grand, m => m.GrandId));
        ctx.From<R22Child>(b => b.HasOne(c => c.Mid, c => c.MidId));
        return (ctx, path);
    }

    private const string SelfSchema =
        "create table r22_node (id integer primary key, parent_id integer, name text);" +
        "insert into r22_node (id, parent_id, name) values (1, null, 'root'), (2, 1, 'mid'), (3, 2, 'leaf'), (4, 999, 'dangling');";

    private static (SqliteDataContext Ctx, string Path) CreateSelf()
    {
        var (ctx, path) = CreateDb(SelfSchema);
        ctx.From<R22Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        return (ctx, path);
    }

    private const string DualSchema =
        "create table r22_node (id integer primary key, parent_id integer, name text);" +
        "create table r22_pair (id integer primary key, left_id integer, right_id integer);" +
        "insert into r22_node (id, parent_id, name) values (1, null, 'left-node'), (2, null, 'right-node');" +
        "insert into r22_pair (id, left_id, right_id) values (1, 1, 2);";

    private static (SqliteDataContext Ctx, string Path) CreateDual()
    {
        var (ctx, path) = CreateDb(DualSchema);
        ctx.From<R22Node>(b => b.HasOne(n => n.Parent, n => n.ParentId));
        ctx.From<R22Pair>(b =>
        {
            b.HasOne(p => p.Left, p => p.LeftId);
            b.HasOne(p => p.Right, p => p.RightId);
        });
        return (ctx, path);
    }

    [Fact]
    public void Multi_hop_reference_chain_should_left_join_every_hop()
    {
        var (ctx, path) = CreateChain();
        try
        {
            var sql = SqlOf(ctx, ctx.From<R22Child>()
                .Where(c => c.Mid!.Grand!.Label == "x")
                .Select(c => new { c.Id }));

            var joins = sql.ToLowerInvariant().Split("left join r22_mid", StringSplitOptions.None).Length - 1;
            joins.Should().Be(1);
            sql.ToLowerInvariant().Should().Contain("left join r22_grand");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Multi_hop_scalar_chain_should_read_the_innermost_principal()
    {
        var (ctx, path) = CreateChain();
        try
        {
            var rows = ctx.From<R22Child>()
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, Grand = c.Mid!.Grand!.Label })
                .ToList();

            rows.Single(r => r.Id == 100).Grand.Should().Be("g1");
            rows.Single(r => r.Id == 101).Grand.Should().BeNull("the second hop's foreign key dangles");
            rows.Single(r => r.Id == 102).Grand.Should().BeNull("the intermediate foreign key dangles and must null-propagate");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Multi_hop_intermediate_absence_should_not_reappear_via_a_default_key()
    {
        var (ctx, path) = CreateChain();
        try
        {
            // None of the dangling rows may match a real grand label such as g1.
            var ids = ctx.From<R22Child>()
                .Where(c => c.Mid!.Grand!.Label == "g1")
                .Select(c => c.Id)
                .ToList();

            ids.Should().BeEquivalentTo(new[] { 100 });
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Multi_hop_reference_presence_should_left_join_every_hop_and_propagate_absence()
    {
        var (ctx, path) = CreateChain();
        try
        {
            var rows = ctx.From<R22Child>()
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, Absent = c.Mid!.Grand == null, Present = c.Mid!.Grand != null })
                .ToList();

            rows.Should().OnlyContain(r => r.Absent != r.Present, "== null and != null must be mutually consistent");
            rows.Single(r => r.Id == 100).Present.Should().BeTrue("mid 10 -> grand 1 resolves");
            rows.Single(r => r.Id == 101).Absent.Should().BeTrue("mid 11's grand foreign key dangles");
            rows.Single(r => r.Id == 102).Absent.Should().BeTrue("the intermediate mid foreign key dangles");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Self_reference_should_join_and_read_the_parent()
    {
        var (ctx, path) = CreateSelf();
        try
        {
            var rows = ctx.From<R22Node>()
                .OrderBy(n => n.Id)
                .Select(n => new { n.Id, Parent = n.Parent!.Name })
                .ToList();

            rows.Single(r => r.Id == 1).Parent.Should().BeNull();
            rows.Single(r => r.Id == 2).Parent.Should().Be("root");
            rows.Single(r => r.Id == 3).Parent.Should().Be("mid");
            rows.Single(r => r.Id == 4).Parent.Should().BeNull("the self-reference foreign key dangles");

            var sql = Normalize(SqlOf(ctx, ctx.From<R22Node>()
                .Select(n => new { Parent = n.Parent!.Name })));
            sql.ToLowerInvariant().Should().Contain("left join r22_node");
            sql.ToLowerInvariant().Should().Contain("t2.name", "the self-join must bind its own alias, not the from source");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Self_reference_two_hops_should_chain()
    {
        var (ctx, path) = CreateSelf();
        try
        {
            var rows = ctx.From<R22Node>()
                .OrderBy(n => n.Id)
                .Select(n => new { n.Id, GrandParent = n.Parent!.Parent!.Name })
                .ToList();

            rows.Single(r => r.Id == 3).GrandParent.Should().Be("root");
            rows.Single(r => r.Id == 2).GrandParent.Should().BeNull();
            rows.Single(r => r.Id == 4).GrandParent.Should().BeNull();

            var sql = Normalize(SqlOf(ctx, ctx.From<R22Node>()
                .Select(n => new { GrandParent = n.Parent!.Parent!.Name })));
            var joins = sql.ToLowerInvariant().Split("left join r22_node", StringSplitOptions.None).Length - 1;
            joins.Should().Be(2, "each self-reference hop is its own occurrence");
            sql.ToLowerInvariant().Should().Contain("t3.name", "the second hop binds its own distinct t3 alias");
            sql.ToLowerInvariant().Should().Contain("on t2.parent_id = t3.id", "the second hop chains off the first hop's occurrence t2");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Two_same_typed_reference_paths_should_use_distinct_aliases_and_values()
    {
        var (ctx, path) = CreateDual();
        try
        {
            var rows = ctx.From<R22Pair>()
                .Select(p => new { p.Id, Left = p.Left!.Name, Right = p.Right!.Name })
                .ToList();

            var row = rows.Single();
            row.Left.Should().Be("left-node");
            row.Right.Should().Be("right-node");

            var sql = Normalize(SqlOf(ctx, ctx.From<R22Pair>()
                .Select(p => new { Left = p.Left!.Name, Right = p.Right!.Name })));
            var joins = sql.ToLowerInvariant().Split("left join r22_node", StringSplitOptions.None).Length - 1;
            joins.Should().Be(2, "both same-typed paths must be joined");
            sql.ToLowerInvariant().Should().Contain("t2.name", "the left path binds its own alias");
            sql.ToLowerInvariant().Should().Contain("t3.name", "the right path binds a distinct alias");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Same_path_reused_across_clauses_should_join_it_once()
    {
        var (ctx, path) = CreateChain();
        try
        {
            var sql = SqlOf(ctx, ctx.From<R22Child>()
                .Where(c => c.Mid!.Name == "m10")
                .OrderBy(c => c.Mid!.Grand!.Label)
                .Select(c => new { c.Id, Mid = c.Mid!.Name }));

            var joins = sql.ToLowerInvariant().Split("left join r22_mid", StringSplitOptions.None).Length - 1;
            joins.Should().Be(1, "the same path must be reused across the WHERE, ORDER BY and projection");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("r22_grand")]
public sealed class R22Grand
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("r22_mid")]
public sealed class R22Mid
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("grand_id")]
    public int GrandId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R22Grand? Grand { get; set; }
}

[SqlTable("r22_child")]
public sealed class R22Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mid_id")]
    public int MidId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R22Mid? Mid { get; set; }
}

[SqlTable("r22_node")]
public sealed class R22Node
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R22Node? Parent { get; set; }
}

[SqlTable("r22_pair")]
public sealed class R22Pair
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("left_id")]
    public int LeftId { get; set; }

    [Column("right_id")]
    public int RightId { get; set; }

    public R22Node? Left { get; set; }

    public R22Node? Right { get; set; }
}
