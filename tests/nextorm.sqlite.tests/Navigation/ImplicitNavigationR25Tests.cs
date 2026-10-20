using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B R2.5 acceptance closure (SQL): A1 (the four collection terminals and every out-of-scope
/// operator rejected), A5 (null compensation for <c>!=</c>, negation, nullable operands and OR in
/// predicates and bool projections), A7 (lifted/coalesced vs the unlifted guard), A8 (adapter
/// equivalence, expression-only marker incl. null, reference adapter and adapter composition
/// fail-closed, captured receiver not a navigation source) and A10 (a missing registered source is
/// diagnosed). Mirrors the D4/D5 SQLite style.
/// </summary>
public class ImplicitNavigationR25Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static (SqliteDataContext Ctx, string Path) CreateDb(string schema)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-r25-nav-{Guid.NewGuid():N}.db");
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

    private static (SqliteDataContext Ctx, string Path) Create()
    {
        var (ctx, path) = CreateDb(Schema);
        ctx.From<R25SqlChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<R25SqlParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        return (ctx, path);
    }

    private const string Schema =
        "create table r25_parent (id integer primary key, age integer not null, name text, score integer);" +
        "create table r25_child (id integer primary key, parent_id integer, active integer not null, name text);" +
        "insert into r25_parent (id, age, name, score) values (1, 42, 'p1', null);" +
        "insert into r25_child (id, parent_id, active, name) values (10, 1, 1, 'c10'), (11, 1, 0, 'c11'), (12, 999, 1, 'c12'), (13, 999, 0, 'c13');";

    // ---- A1: four terminals and every reject form ------------------------------------------------

    [Fact]
    public void Terminals_should_report_bool_int_long_and_int()
    {
        var (ctx, path) = Create();
        try
        {
            var row = ctx.From<R25SqlParent>()
                .Where(p => p.Id == 1)
                .Select(p => new
                {
                    B = p.Children.Any(),
                    C = p.Children.Count(),
                    L = p.Children.LongCount(),
                    P = p.Children.Count,
                })
                .ToList()
                .Single();

            row.B.GetType().Should().Be(typeof(bool));
            row.C.GetType().Should().Be(typeof(int));
            row.L.GetType().Should().Be(typeof(long));
            row.P.GetType().Should().Be(typeof(int));
            row.B.Should().BeTrue();
            row.C.Should().Be(2);
            row.L.Should().Be(2L);
            row.P.Should().Be(2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Every_unsupported_collection_operator_should_fail_closed()
    {
        var (ctx, path) = Create();
        try
        {
            Action[] rejects =
            [
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.OrderBy(c => c.Id) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.OrderByDescending(c => c.Id) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Skip(1) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Take(1) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Distinct() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.GroupBy(c => c.Id) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Join(p.Children, a => a.Id, b => b.Id, (a, b) => a) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.GroupJoin(p.Children, a => a.Id, b => b.Id, (a, b) => a) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Union(p.Children) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Except(p.Children) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Intersect(p.Children) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.SelectMany(c => p.Children) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Single() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.SingleOrDefault() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Last() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.LastOrDefault() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Average(c => c.Id) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Contains(new R25SqlChild { Id = 1 }) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.Count(c => c.Id > 0) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.LongCount(c => c.Id > 0) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.First(c => c.Id > 0) })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.ToArray() })),
                () => SqlOf(ctx, ctx.From<R25SqlParent>().Select(p => new { p.Id, X = p.Children.ToList() })),
            ];

            for (var i = 0; i < rejects.Length; i++)
                rejects[i].Should().Throw<NotSupportedException>($"reject form #{i} must fail closed");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---- A5: null compensation in predicates and projections ------------------------------------

    [Fact]
    public void Null_compensation_should_hold_for_or_not_equal_nullable_and_negation()
    {
        var (ctx, path) = Create();
        try
        {
            ctx.From<R25SqlChild>().Where(c => c.Active || c.Parent!.Name == "p1").Select(c => c.Id).ToList()
                .Should().BeEquivalentTo(new[] { 10, 11, 12 }, "an active row whose principal is absent must survive the OR");

            ctx.From<R25SqlChild>().Where(c => c.Parent!.Name != "p1").Select(c => c.Id).ToList()
                .Should().BeEquivalentTo(new[] { 12, 13 }, "an absent principal satisfies != because its value is NULL");

            ctx.From<R25SqlChild>().Where(c => !(c.Parent!.Name == "p1")).Select(c => c.Id).ToList()
                .Should().BeEquivalentTo(new[] { 12, 13 }, "a negated navigation equality must include the absent principal");

            ctx.From<R25SqlChild>().Where(c => c.Parent!.Score == null).Select(c => c.Id).ToList()
                .Should().BeEquivalentTo(new[] { 10, 11, 12, 13 }, "an absent principal OR a NULL column satisfies == null");

            ctx.From<R25SqlChild>().Where(c => (int?)c.Parent!.Age > 5).Select(c => c.Id).ToList()
                .Should().BeEquivalentTo(new[] { 10, 11 }, "an absent principal cannot satisfy a lifted relational comparison");

            var sql = SqlOf(ctx, ctx.From<R25SqlChild>().Where(c => c.Parent!.Name != "p1").Select(c => c.Id));
            sql.Should().Contain("is null", "the SQL comparison must be null-compensated, not a bare <>");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Null_compensation_should_hold_in_a_bool_value_projection()
    {
        var (ctx, path) = Create();
        try
        {
            var rows = ctx.From<R25SqlChild>()
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, Different = c.Parent!.Name != "p1" })
                .ToList();

            rows.Single(r => r.Id == 10).Different.Should().BeFalse();
            rows.Single(r => r.Id == 11).Different.Should().BeFalse();
            rows.Single(r => r.Id == 12).Different.Should().BeTrue("the absent principal is NULL, which is different from 'p1'");
            rows.Single(r => r.Id == 13).Different.Should().BeTrue();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---- A7: lifted / coalesced vs the unlifted guard --------------------------------------------

    [Fact]
    public void Lifted_and_coalesced_should_materialize_and_unlifted_should_throw_with_path_and_type()
    {
        var (ctx, path) = Create();
        try
        {
            var rows = ctx.From<R25SqlChild>()
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, Lifted = (int?)c.Parent!.Age, Coalesced = (int?)c.Parent!.Age ?? -1 })
                .ToList();

            rows.Single(r => r.Id == 10).Lifted.Should().Be(42);
            rows.Single(r => r.Id == 10).Coalesced.Should().Be(42);
            rows.Single(r => r.Id == 12).Lifted.Should().BeNull("a lifted nullable scalar reads SQL NULL");
            rows.Single(r => r.Id == 12).Coalesced.Should().Be(-1, "an explicit coalesce supplies the default");

            Action act = () => SqlOf(ctx, ctx.From<R25SqlChild>().Select(c => new { c.Id, Age = c.Parent!.Age }));

            act.Should().Throw<QueryPreparationException>()
                .WithMessage("*R25SqlChild.Parent.Age*System.Int32*",
                    "an unlifted non-nullable navigation scalar must fail with the path and result type");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---- A8: adapter equivalence and expression-only boundary ------------------------------------

    [Fact]
    public void Adapter_terminal_should_match_the_explicit_native_query()
    {
        var (ctx, path) = Create();
        try
        {
            var adapterSql = SqlOf(ctx, ctx.From<R25SqlParent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Has = p.Children.AsEntityBuilder<R25SqlChild>().Any() }));

            var explicitSql = SqlOf(ctx, ctx.From<R25SqlParent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Has = ctx.From<R25SqlChild>().Where(c => c.ParentId == p.Id).Any() }));

            adapterSql.Should().Be(explicitSql);
            adapterSql.Should().Contain("exists(");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void AsEntityBuilder_outside_a_query_should_throw_including_null()
    {
        IEnumerable<R25SqlChild>? nullCollection = null;
        object? nullReference = null;

        Action nullCollectionAct = () => nullCollection!.AsEntityBuilder();
        Action nullReferenceAct = () => nullReference.AsEntityBuilder<R25SqlParent>();
        Action collectionAct = () => new List<R25SqlChild>().AsEntityBuilder();
        Action referenceAct = () => new R25SqlParent().AsEntityBuilder<R25SqlParent>();

        nullCollectionAct.Should().Throw<NotSupportedException>("the marker is expression-only, never a materializable null adapter");
        nullReferenceAct.Should().Throw<NotSupportedException>();
        collectionAct.Should().Throw<NotSupportedException>();
        referenceAct.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Reference_adapter_inside_a_query_expression_should_fail_closed()
    {
        var (ctx, path) = Create();
        try
        {
            Action act = () => SqlOf(ctx, ctx.From<R25SqlChild>()
                .Select(c => new { c.Id, X = c.Parent.AsEntityBuilder<R25SqlParent>() }));

            act.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Adapter_linq_composition_should_fail_closed()
    {
        var (ctx, path) = Create();
        try
        {
            Action act = () => SqlOf(ctx, ctx.From<R25SqlParent>()
                .Select(p => new { p.Id, X = p.Children.AsEntityBuilder<R25SqlChild>().Where(c => c.Id > 0) }));

            act.Should().Throw<NotSupportedException>();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Captured_enumerable_should_not_be_a_navigation_source()
    {
        var (ctx, path) = Create();
        try
        {
            var captured = new List<R25SqlChild> { new() { Id = 1 } };

            var rows = ctx.From<R25SqlParent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Has = captured.Any() })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].Has.Should().BeTrue("a captured enumerable is read as an ordinary in-memory collection");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---- A10: missing registered source is diagnosed ---------------------------------------------

    [Fact]
    public void Missing_registered_collection_source_should_be_diagnosed()
    {
        var (ctx, path) = CreateDb(
            "create table r25ms_parent (id integer primary key);" +
            "insert into r25ms_parent (id) values (1);");
        try
        {
            ctx.From<R25MsParent>(b => b.HasMany(p => p.Children, c => c.ParentId!));

            Action act = () => SqlOf(ctx, ctx.From<R25MsParent>().Select(p => new { p.Id, X = p.Children.Any() }));

            act.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*R25MsChild*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Missing_registered_reference_source_should_be_diagnosed()
    {
        var (ctx, path) = CreateDb(
            "create table r25mr_child (id integer primary key, parent_id integer);" +
            "insert into r25mr_child (id, parent_id) values (1, 99);");
        try
        {
            ctx.From<R25MrChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

            Action act = () => SqlOf(ctx, ctx.From<R25MrChild>().Select(c => new { c.Id, Name = c.Parent!.Name }));

            act.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*R25MrParent*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("r25_parent")]
public sealed class R25SqlParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int Age { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("score")]
    public int? Score { get; set; }

    public ICollection<R25SqlChild> Children { get; set; } = new List<R25SqlChild>();
}

[SqlTable("r25_child")]
public sealed class R25SqlChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public R25SqlParent? Parent { get; set; }
}

[SqlTable("r25ms_parent")]
public sealed class R25MsParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<R25MsChild> Children { get; set; } = new List<R25MsChild>();
}

[SqlTable("r25ms_child")]
public sealed class R25MsChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("r25mr_child")]
public sealed class R25MrChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    public R25MrParent? Parent { get; set; }
}

[SqlTable("r25mr_parent")]
public sealed class R25MrParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
