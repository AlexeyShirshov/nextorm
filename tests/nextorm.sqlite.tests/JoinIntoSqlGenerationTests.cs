using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL-generation smoke tests for the <c>JoinInto</c> API (slice D4a): the join is part of the command
/// so a bare <see cref="EntityBuilder{TEntity}.ToCommand"/> renders it while the command still
/// materializes the parent type. These tests never open a database connection.
/// </summary>
public class JoinIntoSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<JoinIntoParent> Parents(IDataContext ctx) =>
        ctx.From<JoinIntoParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

    [Fact]
    public void JoinInto_Left_ShouldRenderLeftJoinAndKeepParentCommand()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from join_into_parent as 't1'")
            .And.Contain("left join join_into_child as 't2' on t1.id = t2.parent_id");
    }

    [Fact]
    public void JoinInto_Inner_ShouldRenderInnerJoin()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from join_into_parent as 't1'")
            .And.Contain(" join join_into_child as 't2' on t1.id = t2.parent_id")
            .And.NotContain("left join", "an Inner JoinInto must render an inner join");
    }

    [Fact]
    public void JoinInto_ParentOnlyTerminal_ShouldExcludeTheStitchingJoin()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToParentCommand());

        sql.Should().Be("select id, name from join_into_parent");
    }

    [Fact]
    public void JoinInto_UnsupportedJoinType_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var parent = Parents(ctx);

        var act = () => parent.JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Right);

        act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto supports only*");
    }

    [Fact]
    public void JoinInto_ShouldNotMutateSourceBuilder()
    {
        using var ctx = SqliteTestContext.Create();
        var parent = Parents(ctx);

        _ = parent.JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        SqlOf(ctx, parent.ToCommand()).Should().NotContain(" join ");
    }

    [Fact]
    public void JoinInto_ExplicitKeys_ShouldRenderJoinWithoutRelationshipMetadata()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JoinExplicitParent>()
            .JoinInto(ctx.From<JoinExplicitChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToCommand());

        sql.Should().Contain(" left join join_explicit_child")
            .And.Contain(" on ");
    }

    [Fact]
    public void JoinInto_Where_ShouldFilterTheJoinedQuery()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Where(p => p.Id == 1)
            .ToCommand());

        sql.Should().Contain(" left join join_into_child").And.Contain("where");
    }

    [Fact]
    public void JoinInto_DistinctSpecs_ShouldHaveDistinctPlanKeys_AndRepeatsHit()
    {
        using var ctx = SqliteTestContext.Create();
        var parent = ctx.From<JoinPlanParent>(b => b
            .HasMany(p => p.As, c => c.ParentId)
            .HasMany(p => p.Bs, c => c.ParentId));

        static QueryCommand<JoinPlanParent> Command(IDataContext ctx, EntityBuilder<JoinPlanParent> parent, bool useAs)
        {
            var cmd = parent
                .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, useAs ? p => p.As : p => p.Bs)
                .ToCommand();
            cmd.PrepareCommand(CancellationToken.None);
            return cmd;
        }

        var first = Command(ctx, parent, useAs: true);
        var second = Command(ctx, parent, useAs: false);
        var repeat = Command(ctx, parent, useAs: true);
        var comparer = first.GetQueryPlanEqualityComparer();

        comparer.Equals(first, second).Should().BeFalse();
        comparer.Equals(second, repeat).Should().BeFalse("a distinct declaration must not equal the repeated one either");

        comparer.Equals(first, repeat).Should().BeTrue();
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(repeat));
    }

    [Fact]
    public void JoinInto_RepeatedDeclaration_ShouldReuseCachedPreparedPlan()
    {
        using var ctx = SqliteTestContext.Create();
        var parent = ctx.From<JoinPlanParent>(b => b
            .HasMany(p => p.As, c => c.ParentId)
            .HasMany(p => p.Bs, c => c.ParentId));

        IPreparedQueryCommand<JoinPlanParent> Prepare(bool useAs)
        {
            var cmd = parent
                .JoinInto(ctx.From<JoinIntoChild>(), (p, c) => p.Id == c.ParentId, useAs ? p => p.As : p => p.Bs)
                .ToCommand();
            return ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        }

        var first = Prepare(useAs: true);
        var repeat = Prepare(useAs: true);
        var distinct = Prepare(useAs: false);

        ReferenceEquals(first, repeat).Should()
            .BeTrue("two identical JoinInto declarations on one DataContext must actually hit the plan cache");
        ReferenceEquals(first, distinct).Should()
            .BeFalse("two different JoinInto declarations must not share a cached plan");
    }
}

[SqlTable("join_into_parent")]
public sealed class JoinIntoParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JoinIntoChild> Children { get; } = new List<JoinIntoChild>();
}

[SqlTable("join_into_child")]
public sealed class JoinIntoChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("join_explicit_parent")]
public sealed class JoinExplicitParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<JoinExplicitChild> Children { get; } = new List<JoinExplicitChild>();
}

[SqlTable("join_explicit_child")]
public sealed class JoinExplicitChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("join_plan_parent")]
public sealed class JoinPlanParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<JoinIntoChild> As { get; set; } = new List<JoinIntoChild>();

    public ICollection<JoinIntoChild> Bs { get; set; } = new List<JoinIntoChild>();
}
