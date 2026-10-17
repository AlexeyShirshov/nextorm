using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B D4: collection-navigation terminals (parameterless <c>Any</c>/<c>Count</c>/<c>LongCount</c>
/// and the <c>Count</c> property) and the <c>AsEntityBuilder</c> adapter are lowered to correlated
/// subqueries (EXISTS / scalar count). Whole-reference materialization, ClickHouse
/// <c>join_use_nulls</c> and the InMemory provider are later units (D5/D6).
/// </summary>
public class ImplicitNavigationD4Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static (SqliteDataContext Ctx, string Path) CreateDb(string schema)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-d4-nav-{Guid.NewGuid():N}.db");
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

    private static (SqliteDataContext Ctx, string Path) CreateOneToMany()
    {
        var (ctx, path) = CreateDb(OneToManySchema);
        ctx.From<D4Child>();
        ctx.From<D4Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        return (ctx, path);
    }

    private static (SqliteDataContext Ctx, string Path) CreateManyToMany()
    {
        var (ctx, path) = CreateDb(ManyToManySchema);
        ctx.From<D4MChild>();
        ctx.From<D4MLink>();
        ctx.From<D4MParent>(b => b.HasManyThrough<D4MChild, D4MLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        return (ctx, path);
    }

    // #148-B R2.1: parent 1 has 3 valid junction rows (two of them to the same child) plus 1 dangling;
    // parent 2 has only a dangling row; parent 3 has none; parent 4 has two valid duplicates.
    private static (SqliteDataContext Ctx, string Path) CreateManyToManyWithDangling()
    {
        var (ctx, path) = CreateDb(ManyToManyWithDanglingSchema);
        ctx.From<D4MChild>();
        ctx.From<D4MLink>();
        ctx.From<D4MParent>(b => b.HasManyThrough<D4MChild, D4MLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        return (ctx, path);
    }

    private const string OneToManySchema =
        "create table d4_parent (id integer primary key);" +
        "create table d4_child (id integer primary key, parent_id integer, name text);" +
        "insert into d4_parent (id) values (1), (2);" +
        "insert into d4_child (id, parent_id, name) values (10, 1, 'a'), (11, 1, 'a');";

    private const string ManyToManySchema =
        "create table d4m_parent (id integer primary key);" +
        "create table d4m_child (id integer primary key);" +
        "create table d4m_link (parent_id integer, child_id integer);" +
        "insert into d4m_parent (id) values (1), (2);" +
        "insert into d4m_child (id) values (100);" +
        "insert into d4m_link (parent_id, child_id) values (1, 100), (1, 100);";

    // #148-B R2.1: 3 valid junction rows + 1 dangling for parent 1; a dangling-only parent 2; an empty
    // parent 3; a duplicate-same-child parent 4.
    private const string ManyToManyWithDanglingSchema =
        "create table d4m_parent (id integer primary key);" +
        "create table d4m_child (id integer primary key);" +
        "create table d4m_link (parent_id integer, child_id integer);" +
        "insert into d4m_parent (id) values (1), (2), (3), (4);" +
        "insert into d4m_child (id) values (100), (101);" +
        "insert into d4m_link (parent_id, child_id) values (1, 100), (1, 100), (1, 101), (1, 999), (2, 999), (4, 100), (4, 100);";

    [Fact]
    public void One_to_many_Any_should_be_true_when_children_exist_and_false_when_empty()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var rows = ctx.From<D4Parent>()
                .Select(p => new { p.Id, Has = p.Children.Any() })
                .ToList();

            rows.Single(r => r.Id == 1).Has.Should().BeTrue();
            rows.Single(r => r.Id == 2).Has.Should().BeFalse();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void One_to_many_Count_LongCount_and_Count_property_should_return_values()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var rows = ctx.From<D4Parent>()
                .Select(p => new { p.Id, C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
                .ToList();

            var one = rows.Single(r => r.Id == 1);
            one.C.Should().Be(2);
            one.L.Should().Be(2L);
            one.P.Should().Be(2);

            var empty = rows.Single(r => r.Id == 2);
            empty.C.Should().Be(0);
            empty.L.Should().Be(0L);
            empty.P.Should().Be(0);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Many_to_many_should_preserve_duplicate_junction_rows_and_empty_parent()
    {
        var (ctx, path) = CreateManyToMany();
        try
        {
            var rows = ctx.From<D4MParent>()
                .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
                .ToList();

            var one = rows.Single(r => r.Id == 1);
            one.Has.Should().BeTrue();
            one.C.Should().Be(2, "duplicate junction rows must not be deduplicated");
            one.L.Should().Be(2L);
            one.P.Should().Be(2);

            var empty = rows.Single(r => r.Id == 2);
            empty.Has.Should().BeFalse();
            empty.C.Should().Be(0);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // #148-B R2.1 red->green: the direct many-to-many terminal counts the junction rows whose mapped
    // child exists. The fixture is 3 valid + 1 dangling -> 3, NOT 4.
    [Fact]
    public void Many_to_many_should_exclude_dangling_junction_rows_without_deduplicating()
    {
        var (ctx, path) = CreateManyToManyWithDangling();
        try
        {
            var rows = ctx.From<D4MParent>()
                .Where(p => p.Id == 1 || p.Id == 2 || p.Id == 3 || p.Id == 4)
                .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count(), L = p.Children.LongCount(), P = p.Children.Count })
                .ToList();

            var mixed = rows.Single(r => r.Id == 1);
            mixed.Has.Should().BeTrue();
            mixed.C.Should().Be(3, "3 junction rows have a mapped child; the dangling link is excluded");
            mixed.L.Should().Be(3L);
            mixed.P.Should().Be(3);

            var danglingOnly = rows.Single(r => r.Id == 2);
            danglingOnly.Has.Should().BeFalse("a junction row whose mapped child is absent yields no element");
            danglingOnly.C.Should().Be(0);
            danglingOnly.L.Should().Be(0L);
            danglingOnly.P.Should().Be(0);

            var empty = rows.Single(r => r.Id == 3);
            empty.Has.Should().BeFalse();
            empty.C.Should().Be(0);

            var duplicates = rows.Single(r => r.Id == 4);
            duplicates.Has.Should().BeTrue();
            duplicates.C.Should().Be(2, "two junction rows to the same existing child are not deduplicated");
            duplicates.L.Should().Be(2L);
            duplicates.P.Should().Be(2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Many_to_many_count_sql_should_require_the_mapped_child_without_distinct()
    {
        var (ctx, path) = CreateManyToManyWithDangling();
        try
        {
            var sql = Normalize(SqlOf(ctx, ctx.From<D4MParent>().Select(p => new { p.Id, C = p.Children.Count() }))).ToLowerInvariant();

            sql.Should().Contain("d4m_child", "the count must restrict the junction to the mapped child");
            sql.Should().NotContain("distinct", "the relation must not be deduplicated");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Adapter_Any_should_lower_to_the_same_subquery_as_the_explicit_native_query()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var adapterSql = SqlOf(ctx, ctx.From<D4Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Has = p.Children.AsEntityBuilder<D4Child>().Any() }));

            var explicitSql = SqlOf(ctx, ctx.From<D4Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Has = ctx.From<D4Child>().Where(c => c.ParentId == p.Id).Any() }));

            adapterSql.Should().Be(explicitSql);
            adapterSql.Should().Contain("exists(");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Adapter_terminal_should_execute_with_the_same_result_as_the_direct_terminal()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var rows = ctx.From<D4Parent>()
                .Select(p => new { p.Id, Has = p.Children.AsEntityBuilder<D4Child>().Any(), C = p.Children.AsEntityBuilder<D4Child>().Count() })
                .ToList();

            rows.Single(r => r.Id == 1).Has.Should().BeTrue();
            rows.Single(r => r.Id == 1).C.Should().Be(2);
            rows.Single(r => r.Id == 2).Has.Should().BeFalse();
            rows.Single(r => r.Id == 2).C.Should().Be(0);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Generated_count_subquery_should_be_a_correlated_scalar_count()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var sql = SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, C = p.Children.Count(), L = p.Children.LongCount() }));

            // #148-B r3 A3′: the correlated scalar count is emitted wide (bigint, no in-database int
            // cast); the checked narrowing to int happens only on materialization.
            sql.Should().Contain("count(");
            sql.Should().NotContain("cast(count(");
            sql.Should().NotContain(" as integer)", "no Int32 narrowing cast may wrap the count (A3′)");
            sql.Should().Contain("from d4_child");
            sql.Should().Contain("= t1.id");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // M14 pin, retargeted for #148-B r3 A3′: every count arm (Count(), the Count property and
    // LongCount()) is emitted wide (64-bit) with no in-database Int32 cast; the checked narrowing for
    // Count/property is a materialization-only CLR boundary. D2 covers the helper contract.
    [Fact]
    public void Count_arms_and_LongCount_should_all_stay_wide_in_SQL()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var countSql = SqlOf(ctx, ctx.From<D4Parent>().Select(p => new { p.Id, C = p.Children.Count() }));
            var propertySql = SqlOf(ctx, ctx.From<D4Parent>().Select(p => new { p.Id, P = p.Children.Count }));
            var longCountSql = SqlOf(ctx, ctx.From<D4Parent>().Select(p => new { p.Id, L = p.Children.LongCount() }));

            countSql.Should().Contain("count(").And.NotContain("cast(count(").And.NotContain(" as integer)", "Count() must stay wide (A3′)");
            propertySql.Should().Contain("count(").And.NotContain("cast(count(").And.NotContain(" as integer)", "the Count property must stay wide (A3′)");
            longCountSql.Should().Contain("count(").And.NotContain("cast(count(").And.NotContain(" as integer)", "LongCount() must keep the raw 64-bit count");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Predicate_overload_should_be_rejected()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var act = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, Has = p.Children.Any(c => c.Id > 0) }));

            act.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Direct_linq_composition_should_be_rejected()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            Action where = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, X = p.Children.Where(c => c.Id > 0) }));
            Action select = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, X = p.Children.Select(c => c.Id).ToList() }));
            Action sum = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, X = p.Children.Sum(c => c.Id) }));

            where.Should().Throw<NotSupportedException>();
            select.Should().Throw<NotSupportedException>();
            sum.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Extra_terminals_should_be_rejected()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var probe = new D4Child { Id = 10 };
            Action first = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, X = p.Children.First().Id }));
            Action contains = () => SqlOf(ctx, ctx.From<D4Parent>()
                .Select(p => new { p.Id, X = p.Children.Contains(probe) }));

            first.Should().Throw<NotSupportedException>();
            contains.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Captured_receiver_should_not_be_treated_as_a_navigation_source()
    {
        var (ctx, path) = CreateOneToMany();
        try
        {
            var captured = new List<D4Child> { new() { Id = 10 } };

            var rows = ctx.From<D4Parent>()
                .Select(p => new { p.Id, Has = captured.Any() })
                .ToList();

            // The captured list is read as an ordinary in-memory collection, never as p.Children.
            rows.Should().OnlyContain(r => r.Has);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("d4_parent")]
public sealed class D4Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<D4Child> Children { get; set; } = new List<D4Child>();
}

[SqlTable("d4_child")]
public sealed class D4Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("d4m_parent")]
public sealed class D4MParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<D4MChild> Children { get; set; } = new List<D4MChild>();
}

[SqlTable("d4m_child")]
public sealed class D4MChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("d4m_link")]
public sealed class D4MLink
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
