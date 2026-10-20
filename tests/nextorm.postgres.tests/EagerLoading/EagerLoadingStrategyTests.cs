using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// #209 eager-load strategy coverage on PostgreSQL: the provider-independent resolution (context default,
/// query override, conservative Auto) dispatches to the existing split/single-query paths, and no
/// correlated capability is newly advertised — an explicit Correlated request fails closed before SQL and
/// the supported SQL carries no LATERAL/APPLY construct. The tests never open a connection.
/// </summary>
public class EagerLoadingStrategyTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private static IDataContext Context(EagerLoadMode? mode = null)
    {
        var builder = new DataContextBuilder();
        if (mode is { } m)
            builder.UseEagerLoadMode(m);
        return new PostgresDataContext(ConnectionString, builder);
    }

    private static EntityBuilder<EagerStratParent> Parents(IDataContext ctx)
        => ctx.From<EagerStratParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

    private static string SqlOf(IDataContext ctx, QueryCommand cmd)
        => ((dynamic)ctx.GetPreparedQueryCommand((dynamic)cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void ContextDefaultSplitQuery_KeepsTheSplitPath()
    {
        using var ctx = Context();

        Parents(ctx).LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId)
            .ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);
    }

    [Fact]
    public void ContextDefaultSingleQuery_DispatchesToTheSingleQueryJoin()
    {
        using var ctx = Context(EagerLoadMode.SingleQuery);

        var builder = Parents(ctx).LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId);
        builder.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        SqlOf(ctx, builder.CreatePairCommand(specs, joins)).ToLowerInvariant().Should().Contain(" join ");
    }

    [Fact]
    public void ContextDefaultAuto_DispatchesToSplit_AndAnExplicitOverrideWins()
    {
        using var ctx = Context(EagerLoadMode.Auto);

        Parents(ctx).LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId)
            .ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);

        Parents(ctx).LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
    }

    [Fact]
    public void ExplicitCorrelated_FailsClosed_AndNoCorrelatedSqlIsAdvertised()
    {
        using var ctx = Context();

        var cmd = Parents(ctx)
            .LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated)
            .ToCommand();
        Action act = () => cmd.PrepareCommand(CancellationToken.None);
        act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");

        var split = Parents(ctx).LoadWith(p => p.Children, c => c.From<EagerStratChild>(), p => p.Id, c => c.ParentId).ToCommand();
        SqlOf(ctx, split).ToLowerInvariant().Should().NotContain("lateral").And.NotContain(" apply ");
    }

    // --- T212-D5: a nested (level 2+) LoadWith on the single-query path renders one join per level; an
    // --- unexpressible nested child shape fails closed before any SQL is generated. A dedicated trio of
    // --- nested entities keeps the level-1 strategy fixtures scalar-only, so the existing projections
    // --- are untouched.

    private static EntityBuilder<EagerStratNestedParent> NestedSingleParents(IDataContext ctx)
        => ctx.From<EagerStratNestedParent>(b => b.HasMany(p => p.Mids, m => m.ParentId))
            .LoadWith(
                p => p.Mids,
                c => c.From<EagerStratNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerStratNestedLeaf>(), m => m.Id, l => l.MidId),
                p => p.Id,
                m => m.ParentId,
                EagerLoadMode.SingleQuery);

    [Fact]
    public void NestedSingleQuery_JoinsEveryLevelIntoOneCommand()
    {
        using var ctx = Context();

        var builder = NestedSingleParents(ctx);
        builder.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = SqlOf(ctx, builder.CreatePairCommand(specs, joins)).ToLowerInvariant();

        sql.Should().Contain("eager_strategy_nested_parent").And.Contain("eager_strategy_nested_mid").And.Contain("eager_strategy_nested_leaf");
        sql.Split(" join ").Should().HaveCount(3, "two nested joins split the chain into three segments");
        sql.Should().NotContain(" in (");
    }

    [Fact]
    public void NestedSingleQuery_InexpressibleGrandChildShape_FailsClosedBeforeSql()
    {
        using var ctx = Context();

        var builder = ctx.From<EagerStratNestedParent>(b => b.HasMany(p => p.Mids, m => m.ParentId))
            .LoadWith(
                p => p.Mids,
                c => c.From<EagerStratNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerStratNestedLeaf>().Distinct(), m => m.Id, l => l.MidId),
                p => p.Id,
                m => m.ParentId,
                EagerLoadMode.SingleQuery);

        Action act = () => builder.BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>().WithMessage("*Distinct*");
    }

    [Fact]
    public void NestedSingleQuery_PerParentPagedGrandChild_FailsClosedBeforeSql()
    {
        using var ctx = Context();

        var builder = ctx.From<EagerStratNestedParent>(b => b.HasMany(p => p.Mids, m => m.ParentId))
            .LoadWith(
                p => p.Mids,
                c => c.From<EagerStratNestedMid>().OrderBy(m => m.Id).Limit(1)
                    .LoadWith(m => m.Leaves, l => l.From<EagerStratNestedLeaf>(), m => m.Id, l => l.MidId),
                p => p.Id,
                m => m.ParentId,
                EagerLoadMode.SingleQuery);

        Action act = () => builder.BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }
}

[SqlTable("eager_strategy_parent")]
public sealed class EagerStratParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<EagerStratChild> Children { get; set; } = new List<EagerStratChild>();
}

[SqlTable("eager_strategy_child")]
public sealed class EagerStratChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("eager_strategy_nested_parent")]
public sealed class EagerStratNestedParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<EagerStratNestedMid> Mids { get; } = new List<EagerStratNestedMid>();
}

[SqlTable("eager_strategy_nested_mid")]
public sealed class EagerStratNestedMid
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    public ICollection<EagerStratNestedLeaf> Leaves { get; } = new List<EagerStratNestedLeaf>();
}

[SqlTable("eager_strategy_nested_leaf")]
public sealed class EagerStratNestedLeaf
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mid_id")]
    public int MidId { get; set; }
}
