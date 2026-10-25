using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

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
/// T287 (issue #287): MySQL has no <c>NULLS FIRST</c>/<c>NULLS LAST</c>, so an explicit placement is
/// emulated with an ascending <c>CASE</c> rank prefix followed by the key (the key alone carries the
/// requested direction). The <c>Default</c> placement stays byte-identical to the captured baseline, index
/// keys are resolved (never <c>CASE WHEN 1 IS NULL</c>) and DISTINCT fails closed. No database is opened.
/// </summary>
public class T287NullOrderingTests
{
    private const string DefaultAscSql = "select id, k as `Key`\n from t287_null_probe\n order by k";
    private const string DefaultDescSql = "select id, k as `Key`\n from t287_null_probe\n order by k desc";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void T287NullOrdering_CaseEmulation_ShouldRenderAnAscendingPrefixForAllFourCells()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.First).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.Last).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.First).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k desc");
        SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id, x.Key }))
            .Should().EndWith("order by CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k desc");

        var sql = SqlOf(ctx, e.OrderBy(x => x.Key, OrderDirection.Desc, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        sql.Should().NotContain("NULLS", "the provider keeps the portable CASE emulation");
    }

    [Fact]
    public void T287NullOrdering_ParameterizedKey_ShouldBindBothCaseOccurrences()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();
        var factor = -1000003;

        // The key is a captured local, so it is bound as a parameter instead of being inlined. The CASE
        // emulation emits the key twice (the IS NULL rank test and the sort key), so the rendered
        // statement must reference the parameter twice while the command still carries it once.
        var prepared = Prepare(ctx, e.OrderBy(x => x.Key * factor, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().EndWith("order by CASE WHEN (k * @factor) IS NULL THEN 0 ELSE 1 END ASC, (k * @factor)",
            "the CASE emulation must repeat the parameterized key in both physical sort positions");
        (sql.Split("@factor").Length - 1).Should().Be(2, "the key occurs twice in the CASE form");
        sql.Should().NotContain("-1000003", "the captured key value must be bound, never inlined");

        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToList();
        parameters.Should().ContainSingle(p => p.ParameterName == "factor");
        parameters.Single(p => p.ParameterName == "factor").Value.Should().Be(factor);
    }

    [Fact]
    public void T287NullOrdering_SupportsNullsOrdering_ShouldBeFalse()
    {
        using var ctx = MySqlTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsNullsOrdering.Should().BeFalse();
    }

    [Fact]
    public void T287NullOrdering_DefaultPlacement_ShouldKeepTheBaselineSqlBytes()
    {
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var first = SqlOf(ctx, e.OrderBy(2, NullsPosition.First).Select(x => new { x.Id, x.Key }));
        var descLast = SqlOf(ctx, e.OrderBy(2, OrderDirection.Desc, NullsPosition.Last).Select(x => new { x.Id, x.Key }));

        first.Should().EndWith("order by CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k");
        descLast.Should().EndWith("order by CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k desc");
        first.Should().NotContain("CASE WHEN 1 IS NULL", "the ordinal must be resolved to the selected key");
    }

    [Fact]
    public void T287NullOrdering_ThenByWithMixedPlacements_ShouldRenderBothEmulatedKeys()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var sql = SqlOf(ctx, e
            .OrderBy(x => x.Key, NullsPosition.Last)
            .ThenBy(x => x.Id, OrderDirection.Desc, NullsPosition.First)
            .Select(x => new { x.Id, x.Key }));

        sql.Should().EndWith(
            "order by CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k, CASE WHEN id IS NULL THEN 0 ELSE 1 END ASC, id desc");
    }

    [Fact]
    public void T287NullOrdering_InvalidPlacement_ShouldFailBeforeAnySqlIsBuilt()
    {
        using var ctx = MySqlTestContext.Create();
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
    public void T287NullOrdering_Paging_ShouldKeepTheEmulationAndThePage()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        var sql = SqlOf(ctx, e
            .OrderBy(x => x.Key, NullsPosition.First)
            .Select(x => new { x.Id, x.Key })
            .Limit(2)
            .Offset(1));

        sql.Should().Contain("order by CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k")
            .And.Contain("limit 2 offset 1");
    }

    [Fact]
    public void T287NullOrdering_Distinct_ShouldFailClosedBecauseTheCaseRepeatsTheKey()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT287NullProbe>();

        // Control: the default placement is still legal on DISTINCT.
        SqlOf(ctx, e.Distinct().OrderBy(x => x.Key).Select(x => new { x.Key }))
            .Should().Contain("select distinct")
            .And.EndWith("order by k");

        string? sql = null;
        FluentActions.Invoking(() => sql = SqlOf(ctx, e.Distinct().OrderBy(x => x.Key, NullsPosition.First).Select(x => new { x.Key })))
            .Should().Throw<BuildSqlCommandException>().WithMessage("*DISTINCT*");

        sql.Should().BeNull("the refusal must happen before any SQL string is produced");
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
        (OrderDirection.Asc, NullsPosition.First, "CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k", "CASE WHEN t1.k IS NULL THEN 0 ELSE 1 END ASC, t1.k", "order by CASE WHEN t1.k IS NULL THEN 0 ELSE 1 END ASC, t1.k, t1.id"),
        (OrderDirection.Asc, NullsPosition.Last, "CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k", "CASE WHEN t1.k IS NULL THEN 1 ELSE 0 END ASC, t1.k", "order by CASE WHEN t1.k IS NULL THEN 1 ELSE 0 END ASC, t1.k, t1.id"),
        (OrderDirection.Desc, NullsPosition.First, "CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k desc", "CASE WHEN t1.k IS NULL THEN 0 ELSE 1 END ASC, t1.k desc", "order by CASE WHEN t1.k IS NULL THEN 0 ELSE 1 END ASC, t1.k desc, t1.id"),
        (OrderDirection.Desc, NullsPosition.Last, "CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k desc", "CASE WHEN t1.k IS NULL THEN 1 ELSE 0 END ASC, t1.k desc", "order by CASE WHEN t1.k IS NULL THEN 1 ELSE 0 END ASC, t1.k desc, t1.id"),
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
        using var ctx = MySqlTestContext.Create();

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
        using var ctx = MySqlTestContext.Create();

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
