using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// #209 eager-load strategy coverage on MySQL: the provider-independent resolution (context default,
/// query override, conservative Auto) dispatches to the existing split/single-query paths, and no
/// correlated capability is newly advertised — an explicit Correlated request fails closed before SQL and
/// the supported SQL carries no LATERAL/CROSS APPLY construct. The tests never open a connection.
/// </summary>
public class EagerLoadingStrategyTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    private static IDataContext Context(EagerLoadMode? mode = null)
    {
        var builder = new DataContextBuilder();
        if (mode is { } m)
            builder.UseEagerLoadMode(m);
        return new MySqlDataContext(ConnectionString, builder);
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
