using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// The T287 probe table: <c>id</c> plus a nullable integer <c>k</c>. The physical names reproduce the
/// recon fixture (<c>t287_null_probe</c>), so the <c>Default</c> placement can be compared byte-for-byte
/// with the captured baseline SQL in <c>.pdca/T287/rv1/recon/baseline-cases.json</c>.
/// </summary>
[SqlTable("t287_null_probe")]
internal interface IT287NullProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? Key { get; set; }
}

/// <summary>
/// The V30 fixture: a parent and a paged child with a <b>nullable</b> sort key. The child is loaded
/// through the per-parent top-N eager path, whose ranked derived table is rendered by
/// <c>SqlBuilder.MakePerParentRankExpression</c> - the third T287 emitter.
/// </summary>
[SqlTable("t287_null_rank_parent")]
internal interface IT287RankParent
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

/// <summary>The paged child of <see cref="IT287RankParent"/>; <c>k</c> is the nullable rank key.</summary>
[SqlTable("t287_null_rank_child")]
internal interface IT287RankChild
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("parent_id")]
    int ParentId { get; set; }

    [Column("k")]
    int? Key { get; set; }
}

/// <summary>
/// The V31 fixture: an extreme-row probe whose <c>score</c> selects the survivor set and whose
/// <c>k</c> carries the NULLs the outer ordering has to place explicitly.
/// </summary>
[SqlTable("t287_null_extreme")]
internal interface IT287ExtremeProbe
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? Key { get; set; }

    [Column("score")]
    int? Score { get; set; }
}

/// <summary>
/// T287 (issue #287): PostgreSQL renders explicit NULL placement with the native
/// <c>NULLS FIRST</c>/<c>NULLS LAST</c> modifier, keeps the <c>Default</c> placement byte-identical to the
/// captured baseline, resolves index keys and leaves the paging flanks untouched. No database is opened.
/// </summary>
public class T287NullOrderingTests
{
    private const string DefaultAscSql = "select id, k as \"Key\"\n from t287_null_probe\n order by k";
    private const string DefaultDescSql = "select id, k as \"Key\"\n from t287_null_probe\n order by k desc";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void T287NullOrdering_NativeDialect_ShouldRenderTheNullsModifierForAllFourCells()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by k NULLS FIRST");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by k NULLS LAST");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.First).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by k desc NULLS FIRST");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by k desc NULLS LAST");

        var sql = SqlOf(ctx, e.OrderBy(x => x.Key, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        sql.Should().NotContain("CASE", "PostgreSQL understands the native modifier and must not emulate it");
    }

    [Fact]
    public void T287NullOrdering_ParameterizedKey_ShouldBindTheSingleNativeOccurrence()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();
        var factor = -1000003;

        // Native NULLS FIRST/LAST wraps the key once, so a captured (parameterized) key is emitted once
        // and the command carries exactly one parameter occurrence for it.
        var prepared = Prepare(ctx, e.OrderBy(x => x.Key * factor, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().EndWith("order by (k * @factor) NULLS FIRST",
            "the native modifier must carry the parameterized key exactly once");
        (sql.Split("@factor").Length - 1).Should().Be(1, "the native form emits the key once");
        sql.Should().NotContain("-1000003", "the captured key value must be bound, never inlined");

        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToList();
        parameters.Should().ContainSingle(p => p.ParameterName == "factor");
        parameters.Single(p => p.ParameterName == "factor").Value.Should().Be(factor);
    }

    [Fact]
    public void T287NullOrdering_SupportsNullsOrdering_ShouldBeTrue()
    {
        using var ctx = PostgresTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsNullsOrdering.Should().BeTrue();
    }

    [Fact]
    public void T287NullOrdering_DefaultPlacement_ShouldKeepTheBaselineSqlBytes()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var asc = SqlOf(ctx, e.OrderBy(x => x.Key).Select(x => new { x.Id, x.Key }));
        var desc = SqlOf(ctx, e.OrderByDescending(x => x.Key).Select(x => new { x.Id, x.Key }));
        var explicitDefault = SqlOf(ctx, e.OrderBy(x => x.Key, NullsPosition.Default).Select(x => new { x.Id, x.Key }));

        asc.Should().Be(DefaultAscSql).And.NotContain("NULLS").And.NotContain("CASE");
        desc.Should().Be(DefaultDescSql).And.NotContain("NULLS").And.NotContain("CASE");
        explicitDefault.Should().Be(DefaultAscSql, "the explicit Default placement must take the untouched default path");
    }

    [Fact]
    public void T287NullOrdering_IndexOverload_ShouldResolveTheRealKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var first = SqlOf(ctx, e.OrderBy(2, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        var descLast = SqlOf(ctx, e.OrderBy(2, OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id, x.Key }));

        first.Should().EndWith("order by k NULLS FIRST");
        descLast.Should().EndWith("order by k desc NULLS LAST");
        first.Should().NotContain("CASE WHEN 1 IS NULL", "the ordinal must be resolved to the selected key");
    }

    [Fact]
    public void T287NullOrdering_ThenByWithMixedPlacements_ShouldRenderBothKeys()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var sql = SqlOf(ctx, e
            .OrderBy(x => x.Key, NullsPosition.Last)
            .ThenBy(x => x.Id, OrderDirection.Desc, NullsPosition.First)
            .Select(x => new { x.Id, x.Key }));

        sql.Should().EndWith("order by k NULLS LAST, id desc NULLS FIRST");
    }

    [Fact]
    public void T287NullOrdering_InvalidPlacement_ShouldFailBeforeAnySqlIsBuilt()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();
        const NullsPosition Invalid = (NullsPosition)42;

        string? sql = null;
        FluentActions.Invoking(() => sql = SqlOf(ctx, e.OrderBy(x => x.Key, Invalid).Select(x => new { x.Id, x.Key })))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
        FluentActions.Invoking(() => sql = SqlOf(ctx, e.OrderBy(1, Invalid).Select(x => new { x.Id, x.Key })))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");

        sql.Should().BeNull("the invalid placement must be rejected before execution");
    }

    [Fact]
    public void T287NullOrdering_Paging_ShouldKeepThePlacementAndThePage()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var sql = SqlOf(ctx, e
            .OrderBy(x => x.Key, NullsPosition.First)
            .Select(x => new { x.Id, x.Key })
            .Limit(2)
            .Offset(1));

        sql.Should().Contain("order by k NULLS FIRST");
        sql.Should().Contain("limit 2 offset 1", "explicit placement must not disturb the paging fallback");
    }

    [Fact]
    public void T287NullOrdering_DistinctWithNativePlacement_ShouldBeAllowed()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var sql = SqlOf(ctx, e
            .Distinct()
            .OrderBy(x => x.Key, NullsPosition.First)
            .Select(x => new { x.Key }));

        sql.Should().Contain("select distinct");
        sql.Should().EndWith("order by k NULLS FIRST", "the native form repeats no key, so DISTINCT stays legal");
    }

    [Fact]
    public void T287NullOrdering_DistinctOnWithNativePlacement_ShouldNotBeGuarded()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        // The CASE-emulation guard would refuse DISTINCT ON (the emulation repeats the ordering key). The
        // native modifier repeats nothing, so the guard predicate is false here and the shape stays legal.
        var sql = SqlOf(ctx, e
            .DistinctOn(x => x.Key)
            .OrderBy(x => x.Key, NullsPosition.Last)
            .Select(x => new { x.Id, x.Key }));

        sql.Should().Contain("select distinct on (k)");
        sql.Should().EndWith("order by k NULLS LAST");
    }

    // ---- V30: the per-parent rank emitter (explicit placement) -------------------------------------

    /// <summary>
    /// The four (direction, placement) cells of the per-parent rank window. Each carries the fragment the
    /// rank window must render (the native <c>NULLS FIRST</c>/<c>NULLS LAST</c> modifier or the portable
    /// <c>CASE</c> form), the same fragment over the derived-table alias <c>t1</c> that the outer order of
    /// the surviving rows must render, and the exact trailing outer <c>order by</c> of the extreme-row
    /// lowering.
    /// </summary>
    private static readonly
        (OrderDirection Direction, NullsPosition Placement, string Window, string Outer, string Extreme)[] PlacementCells =
    [
        (OrderDirection.Asc, NullsPosition.First, "k NULLS FIRST", "t1.k NULLS FIRST", "order by t1.k NULLS FIRST, t1.id"),
        (OrderDirection.Asc, NullsPosition.Last, "k NULLS LAST", "t1.k NULLS LAST", "order by t1.k NULLS LAST, t1.id"),
        (OrderDirection.Desc, NullsPosition.First, "k desc NULLS FIRST", "t1.k desc NULLS FIRST", "order by t1.k desc NULLS FIRST, t1.id"),
        (OrderDirection.Desc, NullsPosition.Last, "k desc NULLS LAST", "t1.k desc NULLS LAST", "order by t1.k desc NULLS LAST, t1.id"),
    ];

    /// <summary>
    /// V30: the per-parent rank emitter must carry an explicit placement into the
    /// <c>row_number() over (partition by &lt;child key&gt; order by ...)</c> window. The command is exactly
    /// the child command the split per-parent loader builds for a paged child
    /// (<c>EntityBuilderEagerLoading.BuildChildCommand</c>: the child's own order and page plus the
    /// per-parent marks), so this assertion covers the rank emitter and not the plain SELECT ordering.
    /// </summary>
    [Fact]
    public void T287NullOrdering_PerParentRank_ShouldCarryThePlacementIntoTheWindow()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var (direction, placement, window, outer, _) in PlacementCells)
        {
            var command = ctx.From<IT287RankChild>()
                .OrderBy(x => x.Key, direction, placement)
                .Limit(2)
                .Select(x => new { x.Id, x.ParentId, x.Key });
            command.PerParentPaging = true;
            command.PerParentPartition = (Expression<Func<IT287RankChild, int>>)(x => x.ParentId);

            var sql = SqlOf(ctx, command);

            sql.Should().Contain($"over (partition by parent_id order by {window})",
                $"the rank window of {direction}/{placement} must carry the requested placement, not the "
                + "direction-only key");
            sql.Should().Contain($"order by {outer}",
                $"the outer order of the surviving rows for {direction}/{placement} must carry the placement too");
            sql.Should().Contain("row_number()", "the per-parent lowering must stay the ranked derived table");
        }

        // Control: the Default placement keeps the plain direction-only window and outer rendering.
        var control = ctx.From<IT287RankChild>()
            .OrderBy(x => x.Key)
            .Limit(2)
            .Select(x => new { x.Id, x.ParentId, x.Key });
        control.PerParentPaging = true;
        control.PerParentPartition = (Expression<Func<IT287RankChild, int>>)(x => x.ParentId);

        var controlSql = SqlOf(ctx, control);
        controlSql.Should().Contain("over (partition by parent_id order by k)",
            "an unplaced rank window must stay byte-identical to the pre-change rendering");
        controlSql.Should().EndWith("order by t1.k",
            "an unplaced outer order must stay byte-identical to the pre-change rendering");
        controlSql.Should().NotContain("CASE").And.NotContain("NULLS");
    }

    /// <summary>
    /// V31: the extreme-row lowering (<c>SelectWhereMax</c> with <c>ExtremeRowTies.All</c>, i.e. the
    /// portable rank-derived path every provider takes) must carry an explicit placement into the outer
    /// <c>ORDER BY</c> rendered by <c>SqlBuilder.AppendOrderBy</c>. A command that only decorated the
    /// projection - or that dropped the placement between the derived table and the outer statement -
    /// would pass the in-memory case and silently ignore the request on SQL providers.
    /// </summary>
    [Fact]
    public void T287NullOrdering_ExtremeRowOuter_ShouldCarryThePlacement()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var (direction, placement, _, _, extreme) in PlacementCells)
        {
            var sql = SqlOf(ctx, ctx.From<IT287ExtremeProbe>()
                .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
                .OrderBy(x => x.Key, direction, placement)
                .ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Key }));

            sql.Should().Contain("rank()").And.Contain("__nextorm_rn",
                "the Ties.All extreme-row command must take the portable rank lowering whose outer ordering is under test");
            sql.Should().EndWith(extreme,
                $"the outer order of the surviving rows for {direction}/{placement} must carry the placement");
        }

        // Control: the Default placement keeps the plain direction-only outer rendering.
        var controlSql = SqlOf(ctx, ctx.From<IT287ExtremeProbe>()
            .SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .OrderBy(x => x.Key)
            .ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Key }));

        controlSql.Should().EndWith("order by t1.k, t1.id",
            "an unplaced extreme-row outer order must stay byte-identical to the pre-change rendering");
        controlSql.Should().NotContain("CASE").And.NotContain("NULLS");
    }
}
