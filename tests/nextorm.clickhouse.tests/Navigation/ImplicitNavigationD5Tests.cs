using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #148-B D5: the ClickHouse outer-join null setting. A reference-navigation query injects the
/// query-local <c>join_use_nulls=1</c> (so the unmatched side of the injected <c>LEFT JOIN</c> is SQL
/// <c>NULL</c>, not the column default), a plain query does not, an explicit conflicting value is
/// rejected before execution, and the injection neither duplicates on re-preparation nor leaks to a
/// sibling query.
/// </summary>
public class ImplicitNavigationD5Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<D5ChChild> Children(IDataContext ctx)
    {
        ctx.From<D5ChParent>();
        return ctx.From<D5ChChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
    }

    [Fact]
    public void Reference_expansion_query_should_inject_join_use_nulls()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, Children(ctx).Select(c => new { c.Id, Parent = c.Parent }));

        sql.Should().Contain("left join d5ch_parent");
        sql.Should().Contain("settings join_use_nulls = 1");
    }

    [Fact]
    public void Plain_query_should_not_inject_join_use_nulls()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, Children(ctx).Select(c => new { c.Id }));

        sql.Should().NotContain("settings");
        sql.Should().NotContain("join_use_nulls");
    }

    [Fact]
    public void Conflicting_explicit_setting_should_be_rejected_before_execution()
    {
        using var ctx = ClickHouseTestContext.Create();

        var cmd = Children(ctx)
            .Settings(("join_use_nulls", "0"))
            .Select(c => new { c.Id, Parent = c.Parent });

        Action act = () => SqlOf(ctx, cmd);

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*join_use_nulls = 0*join_use_nulls = 1*");
    }

    [Fact]
    public void Explicit_matching_setting_should_be_accepted()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, Children(ctx)
            .Settings(("join_use_nulls", "1"))
            .Select(c => new { c.Id, Parent = c.Parent }));

        sql.Split("join_use_nulls").Length.Should().Be(2, "the setting must appear exactly once");
    }

    [Fact]
    public void Injection_should_be_idempotent_on_re_preparation_and_not_leak_to_a_sibling()
    {
        using var ctx = ClickHouseTestContext.Create();
        var source = Children(ctx).Settings(("max_threads", "2"));

        var referenceCommand = source.Select(c => new { c.Id, Parent = c.Parent });
        referenceCommand.PrepareCommand(false, CancellationToken.None);
        referenceCommand.Settings!.Count(s => s.Key == "join_use_nulls").Should().Be(1);

        // Re-preparing the same command must not append the injected setting a second time.
        referenceCommand.PrepareCommand(false, CancellationToken.None);
        referenceCommand.Settings!.Count(s => s.Key == "join_use_nulls").Should().Be(1);
        referenceCommand.Settings!.Count(s => s.Key == "max_threads").Should().Be(1);

        // A sibling command from the same source builder keeps its own settings and never inherits the
        // injected one.
        var siblingSql = SqlOf(ctx, source.Select(c => new { c.Id }));
        siblingSql.Should().Contain("settings max_threads = 2");
        siblingSql.Should().NotContain("join_use_nulls");
    }
}

[SqlTable("d5ch_parent")]
public sealed class D5ChParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("age")]
    public int Age { get; set; }
}

[SqlTable("d5ch_child")]
public sealed class D5ChChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    public D5ChParent? Parent { get; set; }
}
