using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #148-B D3: reference-navigation lowering. A declared reference navigation used in a query is
/// expanded to a <c>LEFT JOIN</c> and the member access is rewritten to the joined alias. Collection
/// navigations, adapter lowering, whole-reference materialization and InMemory are later units.
/// </summary>
public class ImplicitNavigationD3Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static IDataContext CreateContext()
    {
        var ctx = SqliteTestContext.Create();
        ctx.From<NavParent>();
        ctx.From<NavChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        return ctx;
    }

    [Fact]
    public void Reference_scalar_chain_should_left_join_and_rewrite_to_the_joined_alias()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Where(c => c.Parent!.Name == "x")
            .Select(c => new { c.Id }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("on t1.parent_id = t2.id");
        sql.Should().Contain("t2.name");
    }

    [Fact]
    public void Reference_presence_check_should_test_the_principal_key_for_null()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Where(c => c.Parent == null)
            .Select(c => new { c.Id }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.id is null");
    }

    [Fact]
    public void Reference_presence_negation_should_test_the_principal_key_for_not_null()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Where(c => c.Parent != null)
            .Select(c => new { c.Id }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.id is not null");
    }

    [Fact]
    public void Reference_scalar_projection_should_read_the_joined_alias()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Select(c => new { c.Id, ParentName = c.Parent!.Name }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.name");
    }

    [Fact]
    public void Lifted_nullable_reference_scalar_should_join_and_project_the_joined_column()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Select(c => new { Age = (int?)c.Parent!.Age }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.age");
    }

    [Fact]
    public void Coalesced_reference_scalar_should_join_and_coalesce_the_joined_column()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Select(c => new { Age = (int?)c.Parent!.Age ?? 0 }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.age");
        sql.Should().Contain("0");
    }

    [Fact]
    public void Reference_presence_bool_projection_should_render_is_not_null()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Select(c => new { Present = c.Parent != null }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.id is not null");
    }

    // M04 pin: a bool value projection built from a navigation scalar (here an age comparison) is a
    // null-compensated predicate and must be allowed; the A7 validator must not reject the raw joined
    // member underneath it.
    [Fact]
    public void Bool_value_projection_over_a_navigation_scalar_should_be_allowed()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Select(c => new { c.Id, Over = c.Parent!.Age > 5 }));

        sql.Should().Contain("left join nav_parent");
        sql.Should().Contain("t2.age");
    }

    [Fact]
    public void One_to_one_principal_to_dependent_should_join_on_the_principal_key()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<NavProfile>();
        ctx.From<NavOwner>(b => b.HasOneToOne(o => o.Profile, o => o.Id, p => p.OwnerId));

        var sql = SqlOf(ctx, ctx.From<NavOwner>()
            .Where(o => o.Profile!.Bio == "x")
            .Select(o => new { o.Id }));

        sql.Should().Contain("left join nav_profile");
        sql.Should().Contain("on t1.id = t2.owner_id");
        sql.Should().Contain("t2.bio");
    }

    [Fact]
    public void Collection_navigation_supported_terminal_should_lower_to_a_correlated_subquery()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<NavCollChild>();
        ctx.From<NavCollParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        // D4 supersedes the D3 collection fail-closed behaviour: a parameterless Any is now a
        // correlated EXISTS instead of a rejection.
        var sql = SqlOf(ctx, ctx.From<NavCollParent>()
            .Where(p => p.Children.Any())
            .Select(p => new { p.Id }));

        sql.Should().Contain("exists(");
        sql.Should().Contain("from nav_coll_child");
    }

    [Fact]
    public void Collection_navigation_direct_linq_composition_should_still_fail_closed()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.From<NavCollChild>();
        ctx.From<NavCollParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        var act = () => SqlOf(ctx, ctx.From<NavCollParent>()
            .Select(p => new { p.Id, X = p.Children.Where(c => c.Id > 0) }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Same_reference_path_used_twice_should_join_it_once()
    {
        using var ctx = CreateContext();

        var sql = SqlOf(ctx, ctx.From<NavChild>()
            .Where(c => c.Parent!.Name != null)
            .Select(c => new { c.Id, ParentName = c.Parent!.Name }));

        var occurrences = sql.Split("left join nav_parent", StringSplitOptions.None).Length - 1;
        occurrences.Should().Be(1);
    }
}

[SqlTable("nav_parent")]
public sealed class NavParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int Age { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("nav_child")]
public sealed class NavChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public NavParent? Parent { get; set; }
}

[SqlTable("nav_coll_parent")]
public sealed class NavCollParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<NavCollChild> Children { get; set; } = new List<NavCollChild>();
}

[SqlTable("nav_coll_child")]
public sealed class NavCollChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("nav_owner")]
public sealed class NavOwner
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public NavProfile? Profile { get; set; }
}

[SqlTable("nav_profile")]
public sealed class NavProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }
}
