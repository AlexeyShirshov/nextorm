using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
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
[Collection("Query plan cache")]
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
    // --- T249-D4: metadata-driven single execution (no explicit key selectors) on this dialect. The
    // --- cases mirror the SQLite suite: one left join, positional full-key correlation, a reference
    // --- join, the many-to-many link edges and the fail-closed composite per-parent page.

    private static string Canon(string sql)
        => Regex.Replace(
            sql.Replace("\"", string.Empty).Replace("`", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty),
            @"\s+", " ").Trim().ToLowerInvariant();

    private static int CountJoins(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static void RegisterMetadata()
    {
        var child = new EntityMetadataBuilder<MetaStratChild>();
        child.HasOne(c => c.Parent, c => new { c.TenantId, c.ParentId }, p => new { p.TenantId, p.Id });
        DataContextCache.Metadata[typeof(MetaStratChild)] = child.Build();

        var parent = new EntityMetadataBuilder<MetaStratParent>();
        parent.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId }, p => new { p.TenantId, p.Id });
        DataContextCache.Metadata[typeof(MetaStratParent)] = parent.Build();

        var tagParent = new EntityMetadataBuilder<MetaStratTagParent>();
        tagParent.HasManyThrough<MetaStratTag, MetaStratTagLink>(
            p => p.Tags, p => new { p.Id }, l => new { l.ParentId }, t => new { t.Id }, l => new { l.TagId });
        DataContextCache.Metadata[typeof(MetaStratTagParent)] = tagParent.Build();
        DataContextCache.Metadata[typeof(MetaStratTag)] = new EntityMetadataBuilder<MetaStratTag>().Build();
        DataContextCache.Metadata[typeof(MetaStratTagLink)] = new EntityMetadataBuilder<MetaStratTagLink>().Build();

        var simpleParent = new EntityMetadataBuilder<MetaStratSimpleParent>();
        simpleParent.HasMany(p => p.Children, c => c.ParentId);
        DataContextCache.Metadata[typeof(MetaStratSimpleParent)] = simpleParent.Build();
        DataContextCache.Metadata[typeof(MetaStratSimpleChild)] = new EntityMetadataBuilder<MetaStratSimpleChild>().Build();
    }

    private static EntityBuilder<MetaStratParent> MetaParents(IDataContext ctx)
    {
        RegisterMetadata();
        return ctx.From<MetaStratParent>();
    }

    [Fact]
    public void MetadataSingleQuery_CompositeCollection_RendersOneLeftJoinWithEveryComponentInDeclaredOrder()
    {
        using var ctx = Context();
        var builder = MetaParents(ctx).LoadWith(p => p.Children, EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = Canon(SqlOf(ctx, builder.CreatePairCommand(specs, joins)));

        sql.Should().Contain("left join eager_meta_child").And.NotContain(" in (");
        sql.Should().Contain("t1.tenant_id = t2.tenant_id", "component 1 pairs with component 1");
        sql.Should().Contain("t1.id = t2.parent_id", "component 2 pairs with component 2");
        sql.IndexOf("t1.tenant_id = t2.tenant_id", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("t1.id = t2.parent_id", StringComparison.Ordinal));
        sql[..sql.IndexOf(" from ", StringComparison.Ordinal)]
            .Should().NotContain("row_number").And.NotContain("__");
    }

    [Fact]
    public void MetadataSingleQuery_Reference_RendersOneLeftJoinAndPairsEveryComponent()
    {
        using var ctx = Context();
        RegisterMetadata();
        var builder = ctx.From<MetaStratChild>().LoadWith(c => c.Parent!, EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = Canon(SqlOf(ctx, builder.CreatePairCommand(specs, joins)));

        sql.Should().Contain("left join eager_meta_parent");
        sql.Should().Contain("t1.tenant_id = t2.tenant_id").And.Contain("t1.parent_id = t2.id");
    }

    [Fact]
    public void MetadataSingleQuery_ManyToMany_RendersTheLinkAndChildEdgesInOneCommand()
    {
        using var ctx = Context();
        RegisterMetadata();
        var builder = ctx.From<MetaStratTagParent>().LoadWith(p => p.Tags, EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = Canon(SqlOf(ctx, builder.CreatePairCommand(specs, joins)));

        sql.Should().Contain("eager_meta_tag_link").And.Contain("eager_meta_tag").And.NotContain(" in (");
        CountJoins(sql, " join ").Should().Be(2, "one link edge and one child edge");
    }

    [Fact]
    public void MetadataSingleQuery_PerParentPage_RendersTheRankedPartitionedSource()
    {
        using var ctx = Context();
        RegisterMetadata();

        var builder = ctx.From<MetaStratSimpleParent>()
            .LoadWith(p => p.Children, c => c.From<MetaStratSimpleChild>().OrderBy(x => x.SortKey).Limit(1), EagerLoadMode.SingleQuery);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = Canon(SqlOf(ctx, builder.CreatePairCommand(specs, joins)));

        sql.Should().Contain("row_number()").And.Contain("partition by");
        sql.Should().Contain("from eager_meta_simple_child", "the ranked source reads the child table");
        sql.Should().Contain("left join (select", "the child page is an embedded derived source");
        var outer = sql[..sql.IndexOf(" from ", StringComparison.Ordinal)];
        outer.Should().NotContain("row_number").And.NotContain("partition by");
    }

    [Fact]
    public void MetadataSingleQuery_CompositePerParentPage_FailsClosedWithANamedRefusal()
    {
        using var ctx = Context();
        var builder = MetaParents(ctx).LoadWith(
            p => p.Children,
            c => c.From<MetaStratChild>().OrderBy(x => x.Id).Limit(1),
            EagerLoadMode.SingleQuery);

        Action act = () => builder.BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("composite").And.Contain("per-parent partition");
    }

    [Fact]
    public void MetadataSingleQuery_ExplicitSingleWinsOverContextSplit_AndAutoStaysSplit()
    {
        using var ctx = Context(EagerLoadMode.SplitQuery);

        MetaParents(ctx).LoadWith(p => p.Children, EagerLoadMode.SingleQuery)
            .ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
        MetaParents(ctx).LoadWith(p => p.Children, EagerLoadMode.Auto)
            .ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);
    }
}

[SqlTable("eager_meta_parent")]
public sealed class MetaStratParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<MetaStratChild> Children { get; set; } = new List<MetaStratChild>();
}

[SqlTable("eager_meta_child")]
public sealed class MetaStratChild
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    public MetaStratParent? Parent { get; set; }
}

[SqlTable("eager_meta_tag_parent")]
public sealed class MetaStratTagParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<MetaStratTag> Tags { get; set; } = new List<MetaStratTag>();
}

[SqlTable("eager_meta_tag")]
public sealed class MetaStratTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("eager_meta_tag_link")]
public sealed class MetaStratTagLink
{
    [Key]
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }
}

[SqlTable("eager_meta_simple_parent")]
public sealed class MetaStratSimpleParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<MetaStratSimpleChild> Children { get; set; } = new List<MetaStratSimpleChild>();
}

[SqlTable("eager_meta_simple_child")]
public sealed class MetaStratSimpleChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
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
