using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B D-R3-4/5/6-SQL: closes the SQL side of the r3 residual shapes. A collection reached
/// through a declared reference prefix (2+ hops) correlates off the reference LEFT JOIN alias, so an
/// absent intermediate reference yields an empty collection and can never phantom-match a
/// default-valued foreign key; the reference <c>AsEntityBuilder</c> adapter (<c>object?</c> overload)
/// lowers with the same whole-reference LEFT-JOIN null semantics as an explicit reference presence
/// check. Mirrors the InMemory <c>ImplicitNavigationR3ResidualTests</c>.
/// </summary>
public class ImplicitNavigationR3SqlGapTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-r3sqlg-nav-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = Schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        ctx.From<R3SqRoot>(b => b.HasMany(r => r.Mids, m => m.RootId));
        ctx.From<R3SqMid>(b =>
        {
            b.HasOne(m => m.Root, m => m.RootId);
            b.HasMany(m => m.Leaves, l => l.MidId);
        });
        ctx.From<R3SqLeaf>(b => b.HasOne(l => l.Mid, l => l.MidId));
        return (ctx, path);
    }

    private const string Schema =
        "create table r3sq_root (id integer primary key, name text);" +
        "create table r3sq_mid (id integer primary key, root_id integer, name text);" +
        "create table r3sq_leaf (id integer primary key, mid_id integer, name text);" +
        "insert into r3sq_root (id, name) values (1, 'r1'), (2, 'r2');" +
        "insert into r3sq_mid (id, root_id, name) values (10, 1, 'm10'), (11, 2, 'm11'), (12, 999, 'm12-dangling-root');" +
        "insert into r3sq_leaf (id, mid_id, name) values " +
        "(100, 10, 'l100'), (101, 10, 'l101'), (102, 12, 'l102'), (103, 999, 'l103-dangling-mid'), (104, 0, 'l104-default-mid');";

    // ---- ref -> collection on SQL -----------------------------------------------------------------

    [Fact]
    public void Collection_through_a_reference_should_report_the_principals_children()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<R3SqLeaf>().OrderBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    Has = l.Mid!.Leaves.Any(),
                    C = l.Mid!.Leaves.Count(),
                    L = l.Mid!.Leaves.LongCount(),
                    P = l.Mid!.Leaves.Count,
                })
                .ToList();

            var two = rows.Single(r => r.Id == 100);
            two.Has.Should().BeTrue();
            two.C.Should().Be(2);
            two.L.Should().Be(2L);
            two.P.Should().Be(2);

            var one = rows.Single(r => r.Id == 102);
            one.Has.Should().BeTrue();
            one.C.Should().Be(1);
            one.L.Should().Be(1L);
            one.P.Should().Be(1);

            // A dangling intermediate reference (999) and a default-valued foreign key (0) both yield
            // an empty correlated collection: the absent reference must never phantom-match a
            // default-valued key (leaf 104 itself carries MidId 0).
            foreach (var id in new[] { 103, 104 })
            {
                var absent = rows.Single(r => r.Id == id);
                absent.Has.Should().BeFalse();
                absent.C.Should().Be(0);
                absent.L.Should().Be(0L);
                absent.P.Should().Be(0);
            }
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Collection_through_a_reference_predicate_should_not_match_a_default_key()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ids = ctx.From<R3SqLeaf>()
                .Where(l => l.Mid!.Leaves.Any())
                .Select(l => l.Id)
                .ToList();

            ids.Should().BeEquivalentTo(new[] { 100, 101, 102 },
                "an absent reference must not match a default-valued foreign key");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Collection_through_a_reference_should_left_join_the_reference()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<R3SqLeaf>().Select(l => new { l.Id, C = l.Mid!.Leaves.Count() }));

            sql.ToLowerInvariant().Should().Contain("left join r3sq_mid");
            sql.ToLowerInvariant().Should().Contain("r3sq_leaf");
            sql.ToLowerInvariant().Should().NotContain("inner join r3sq_mid");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Multi_hop_collection_through_references_should_propagate_absence()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<R3SqLeaf>().OrderBy(l => l.Id)
                .Select(l => new { l.Id, C = l.Mid!.Root!.Mids.Count() })
                .ToList();

            rows.Single(r => r.Id == 100).C.Should().Be(1, "mid 10 -> root 1 -> its mids");
            rows.Single(r => r.Id == 101).C.Should().Be(1);
            rows.Single(r => r.Id == 102).C.Should().Be(0, "the root foreign key dangles");
            rows.Single(r => r.Id == 103).C.Should().Be(0, "the intermediate mid foreign key dangles");
            rows.Single(r => r.Id == 104).C.Should().Be(0, "the default-valued mid foreign key is absent");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---- reference adapter on SQL -----------------------------------------------------------------

    [Fact]
    public void Reference_adapter_terminal_should_use_reference_semantics_and_chain_null_behaviour()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<R3SqLeaf>().OrderBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    Has = l.Mid!.AsEntityBuilder<R3SqMid>().Any(),
                    C = l.Mid!.AsEntityBuilder<R3SqMid>().Count(),
                    L = l.Mid!.AsEntityBuilder<R3SqMid>().LongCount(),
                })
                .ToList();

            // Mid 10 has two leaves, so a collection reinterpretation would report 2; the reference
            // adapter must report the single related principal (1), proving it is not a collection.
            var present = rows.Single(r => r.Id == 100);
            present.Has.Should().BeTrue();
            present.C.Should().Be(1);
            present.L.Should().Be(1L);

            foreach (var id in new[] { 103, 104 })
            {
                var absent = rows.Single(r => r.Id == id);
                absent.Has.Should().BeFalse();
                absent.C.Should().Be(0);
                absent.L.Should().Be(0L);
            }
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Reference_adapter_on_a_multi_hop_reference_should_propagate_absence()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<R3SqLeaf>().OrderBy(l => l.Id)
                .Select(l => new { l.Id, Has = l.Mid!.Root!.AsEntityBuilder<R3SqRoot>().Any() })
                .ToList();

            rows.Single(r => r.Id == 100).Has.Should().BeTrue("both hops resolve");
            foreach (var id in new[] { 102, 103, 104 })
                rows.Single(r => r.Id == id).Has.Should().BeFalse("any absent hop makes the reference adapter absent");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Reference_adapter_presence_should_equal_the_explicit_whole_reference_access()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var adapterSql = SqlOf(ctx, ctx.From<R3SqLeaf>().Select(l => new { l.Id, Has = l.Mid!.AsEntityBuilder<R3SqMid>().Any() }));
            var explicitSql = SqlOf(ctx, ctx.From<R3SqLeaf>().Select(l => new { l.Id, Has = l.Mid != null }));

            adapterSql.Should().Be(explicitSql);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Reference_adapter_linq_composition_should_fail_closed()
    {
        var (ctx, path) = CreateDb();
        try
        {
            Action act = () => SqlOf(ctx, ctx.From<R3SqLeaf>()
                .Select(l => new { l.Id, X = l.Mid!.AsEntityBuilder<R3SqMid>().Where(m => m.Id > 0) }));

            act.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("r3sq_root")]
public sealed class R3SqRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<R3SqMid> Mids { get; set; } = new List<R3SqMid>();
}

[SqlTable("r3sq_mid")]
public sealed class R3SqMid
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("root_id")]
    public int RootId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R3SqRoot? Root { get; set; }

    public ICollection<R3SqLeaf> Leaves { get; set; } = new List<R3SqLeaf>();
}

[SqlTable("r3sq_leaf")]
public sealed class R3SqLeaf
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mid_id")]
    public int MidId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R3SqMid? Mid { get; set; }
}
