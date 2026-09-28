using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation coverage for <c>JoinInto</c> on ClickHouse (#105): the single denormalized statement
/// renders the parent plus the declared relationship join, and the parent-only terminals drop it. These
/// tests never open a connection, so the shared integration suite (which does not run on ClickHouse)
/// is not required.
/// </summary>
public class JoinIntoSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<ChJoinParent> Parents(IDataContext ctx) =>
        ctx.From<ChJoinParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

    [Fact]
    public void JoinInto_Left_ShouldRenderLeftJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from join_into_parent as `t1`")
            .And.Contain("left join join_into_child as `t2` on t1.id = t2.parent_id");
    }

    [Fact]
    public void JoinInto_Inner_ShouldRenderInnerJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from join_into_parent as `t1`")
            .And.Contain(" join join_into_child as `t2` on t1.id = t2.parent_id")
            .And.NotContain("left join", "an Inner JoinInto must render an inner join");
    }

    [Fact]
    public void JoinInto_ExplicitKeys_ShouldRenderJoinWithoutRelationshipMetadata()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ChExplicitParent>()
            .JoinInto(ctx.From<ChExplicitChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToCommand());

        sql.Should().Contain("select t1.id from join_explicit_parent as `t1`")
            .And.Contain("left join join_explicit_child as `t2` on t1.id = t2.parent_id");
    }

    [Fact]
    public void JoinInto_ParentPaging_ShouldWrapThePagedParentSubquery()
    {
        using var ctx = ClickHouseTestContext.Create();

        var pair = Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Limit(1);

        string sql = SqlOf(ctx, (dynamic)pair.CreateJoinIntoPairCommand());

        sql.Should().Contain("from (select id, name from join_into_parent")
            .And.Contain(") as `t1`")
            .And.Contain("left join join_into_child as `t2` on t1.id = t2.parent_id");
    }

    [Fact]
    public void JoinInto_UnsupportedJoinType_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var parent = Parents(ctx);

        var act = () => parent.JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Right);

        act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto supports only*");
    }

    [Fact]
    public void JoinInto_Global_ShouldKeepJoinIntoIdentityThroughTheModifier()
    {
        using var ctx = ClickHouseTestContext.Create();

        var parentOnly = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Global()
            .ToParentCommand());

        string pairSql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Global()
            .CreateJoinIntoPairCommand());

        // The parent-only terminal must still drop the JoinInto; the modifier must not degrade it to a
        // regular join (which would leak the child join into the parent command).
        parentOnly.Trim().Should().Be("select id, name from join_into_parent");
        pairSql.Trim().Should().Be(
            "select t1.id, t1.name, t2.id, t2.parent_id as `ParentId` from join_into_parent as `t1` "
            + "global left join join_into_child as `t2` on t1.id = t2.parent_id");

        var plain = Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .CreateJoinIntoPairCommand();
        var global = Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Global()
            .CreateJoinIntoPairCommand();

        var comparer = plain.GetQueryPlanEqualityComparer();
        comparer.Equals(plain, global).Should().BeFalse("the GLOBAL modifier is part of the join plan identity");
    }

    [Fact]
    public void JoinInto_WithStrictness_ShouldKeepJoinIntoIdentityThroughTheModifier()
    {
        using var ctx = ClickHouseTestContext.Create();

        var parentOnly = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .WithStrictness(JoinStrictness.Any)
            .ToParentCommand());

        string pairSql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .WithStrictness(JoinStrictness.Any)
            .CreateJoinIntoPairCommand());

        parentOnly.Trim().Should().Be("select id, name from join_into_parent");
        pairSql.Should().Contain("left any join join_into_child as `t2` on t1.id = t2.parent_id");

        var plain = Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .CreateJoinIntoPairCommand();
        var strict = Parents(ctx)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .WithStrictness(JoinStrictness.Any)
            .CreateJoinIntoPairCommand();

        var comparer = plain.GetQueryPlanEqualityComparer();
        comparer.Equals(plain, strict).Should().BeFalse("the strictness modifier is part of the join plan identity");
    }

    [Fact]
    public void SemiJoinThenJoinInto_ShouldBeRejected()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => Parents(ctx)
            .SemiJoin(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId)
            .JoinInto(ctx.From<ChJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with other joins*");
    }
}

[SqlTable("join_into_parent")]
public sealed class ChJoinParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ChJoinChild> Children { get; set; } = new List<ChJoinChild>();
}

[SqlTable("join_into_child")]
public sealed class ChJoinChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("join_explicit_parent")]
public sealed class ChExplicitParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<ChExplicitChild> Children { get; } = new List<ChExplicitChild>();
}

[SqlTable("join_explicit_child")]
public sealed class ChExplicitChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
