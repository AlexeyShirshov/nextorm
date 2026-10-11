using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// #211 per-parent top-N SQL generation on SQL Server. A child query's own
/// <c>Limit</c>/<c>Offset</c>/<c>Page</c> is lowered to a <c>row_number() over (partition by &lt;child key&gt;
/// order by &lt;child sort&gt;)</c> derived source whose page bounds filter each partition, on both eager
/// modes. These tests never open a connection: the command is prepared and its text inspected.
/// </summary>
[Trait("Feature", "D211")]
public sealed class EagerLoadPerParentPagingTests
{
    [SqlTable("d211_parent")]
    public sealed class D211Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<D211Child> Children { get; } = new List<D211Child>();
    }

    [SqlTable("d211_child")]
    public sealed class D211Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }

        [Column("flag")]
        public int Flag { get; set; }
    }

    private static IDataContext CreateCtx() => SqlServerTestContext.Create();

    private static string SqlOf(IDataContext ctx, QueryCommand cmd)
        => ((dynamic)ctx.GetPreparedQueryCommand((dynamic)cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    /// <summary>
    /// Renders the split child statement the loader builds for <paramref name="builder"/>: the embedded
    /// ranked source is flipped back to a top-level statement (the only rendering difference is the
    /// trailing child ORDER BY the nested derived join drops).
    /// </summary>
    private static string SplitChildSql(IDataContext ctx, EntityBuilder<D211Parent> builder)
    {
        var spec = builder.LoadSpecs![0];
        _ = spec.ToJoinIntoSpec(ctx, QueryFilterScope.None, out var childSource);
        var child = childSource.SubQuery!;
        child.PerParentPagingEmbedded = false;
        return SqlOf(ctx, child);
    }

    private static string SingleQuerySql(IDataContext ctx, EntityBuilder<D211Parent> builder)
    {
        var (specs, joins) = builder.BuildSingleQueryJoins();
        return SqlOf(ctx, builder.CreatePairCommand(specs, joins));
    }

    [Fact]
    public void Split_SkipAndTake_RendersPartitionedRankingWithPerPartitionBounds()
    {
        using var ctx = CreateCtx();

        var sql = SplitChildSql(ctx, ctx.From<D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<D211Child>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Offset(1).Limit(2),
            p => p.Id, c => c.ParentId));

        var lower = sql.ToLowerInvariant();
        lower.Should().Contain("row_number()");
        lower.Should().Contain("over (");
        lower.Should().Contain("partition by parent_id");
        lower.Should().Contain("order by sort_key");

        // The child predicate is evaluated inside the ranked source before the rank filter (filter-before-rank):
        // the WHERE sits before the derived table closes, which in turn precedes the outer rank bound.
        var rankExpr = sql.IndexOf("row_number()", StringComparison.OrdinalIgnoreCase);
        var windowClose = sql.IndexOf(") as ", rankExpr, StringComparison.OrdinalIgnoreCase);
        var derivedClose = sql.IndexOf(") as ", windowClose + 1, StringComparison.OrdinalIgnoreCase);
        derivedClose.Should().BeGreaterThan(windowClose);
        sql.IndexOf("flag = 1", rankExpr, StringComparison.OrdinalIgnoreCase)
            .Should().BeGreaterThan(rankExpr).And.BeLessThan(derivedClose, "the child filter precedes the partition rank");

        // Per-partition page bounds: Skip(1) becomes rn > 1, Skip+Take(1+2) becomes rn <= 3.
        sql.Should().Contain("t1.[__nextorm_rn] > 1");
        sql.Should().Contain("t1.[__nextorm_rn] <= 3");

        // A top-level split child keeps the child query's output ordering.
        sql.TrimEnd().Should().EndWith("order by t1.sort_key");
    }

    [Fact]
    public void Split_SkipOnly_EmitsLowerBoundWithoutUpper()
    {
        using var ctx = CreateCtx();

        var sql = SplitChildSql(ctx, ctx.From<D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<D211Child>().OrderBy(x => x.SortKey).Offset(3),
            p => p.Id, c => c.ParentId));

        sql.Should().Contain("t1.[__nextorm_rn] > 3");
        sql.Should().NotContain("<=");
    }

    [Fact]
    public void Split_DescendingOrder_OrdersWindowAndOutputByDescending()
    {
        using var ctx = CreateCtx();

        var sql = SplitChildSql(ctx, ctx.From<D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<D211Child>().OrderByDescending(x => x.SortKey).Limit(2),
            p => p.Id, c => c.ParentId));

        sql.ToLowerInvariant().Should().Contain("order by sort_key desc");
        sql.Should().Contain("t1.[__nextorm_rn] <= 2");
        sql.TrimEnd().Should().EndWith("order by t1.sort_key desc");
    }

    [Fact]
    public void SingleQuery_PerParentPaging_NestedDerivedSourceHasNoStatementLevelOrderBy()
    {
        using var ctx = CreateCtx();

        var sql = SingleQuerySql(ctx, ctx.From<D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<D211Child>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Offset(1).Limit(2),
            p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery).OrderBy(p => p.Id));

        var lower = sql.ToLowerInvariant();
        lower.Should().Contain("left join");
        lower.Should().Contain("row_number()");
        lower.Should().Contain("partition by parent_id");
        lower.Should().Contain("order by sort_key");
        sql.Should().Contain("t2.[__nextorm_rn] > 1");
        sql.Should().Contain("t2.[__nextorm_rn] <= 3");

        // The nested derived source must not carry a statement-level ORDER BY: only the window's own
        // ORDER BY (inside `over (...)`) is allowed there. The order moves to the outer pair command.
        var lastRank = sql.LastIndexOf("__nextorm_rn", StringComparison.Ordinal);
        lastRank.Should().BeGreaterThanOrEqualTo(0);
        var derivedClose = sql.IndexOf(") as ", lastRank, StringComparison.OrdinalIgnoreCase);
        derivedClose.Should().BeGreaterThan(lastRank);
        sql[lastRank..derivedClose].ToLowerInvariant().Should().NotContain("order by");

        // The outer pair command rebinds the child key against its own join alias, after the parent key.
        sql.Should().Contain("order by t1.id, t3.[SortKey]");
    }

    [Theory]
    [InlineData("Distinct")]
    [InlineData("GroupBy")]
    public void SingleQuery_PerParentPaging_UnsupportedDerivedSourceShape_FailsClosedWithZeroSql(string shape)
    {
        using var ctx = CreateCtx();

        var act = () => ctx.From<D211Parent>()
            .LoadWith(p => p.Children, c => UnsupportedChild(ctx, shape), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>().WithMessage($"*{shape}*");
    }

    [Fact]
    public void Correlated_PerParentPaging_UnsupportedWindowStrategy_FailsClosedBeforeSql()
    {
        using var ctx = CreateCtx();

        var act = () => ctx.From<D211Parent>()
            .LoadWith(p => p.Children, c => c.From<D211Child>().OrderBy(x => x.SortKey).Limit(1), p => p.Id, c => c.ParentId, EagerLoadMode.Correlated)
            .ToCommand()
            .PrepareCommand(CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*Correlated*");
    }

    private static EntityBuilder<D211Child> UnsupportedChild(IDataContext ctx, string shape) => shape switch
    {
        "Distinct" => ctx.From<D211Child>().Distinct().OrderBy(x => x.Id).Limit(1),
        "GroupBy" => ctx.From<D211Child>().GroupBy(x => x.ParentId).OrderBy(x => x.Id).Limit(1),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown child shape"),
    };
}
