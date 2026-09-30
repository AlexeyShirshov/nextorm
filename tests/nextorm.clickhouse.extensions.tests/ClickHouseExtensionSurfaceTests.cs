using FluentAssertions;
using NextORM.Core;
using NextORM.ClickHouse;

namespace NextORM.ClickHouse.Extensions.Tests;

/// <summary>
/// External-consumer surface tests for the ClickHouse fluent extensions. This project has no
/// <c>InternalsVisibleTo</c>, so every call binds to a public extension method rather than to the
/// internal instance method it wraps. That makes these tests the only entry-point coverage of the
/// 58 public extension methods in <c>ClickHouseEntityBuilderExtensions.cs</c>.
/// </summary>
public class ClickHouseExtensionSurfaceTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    // ---- ClickHouseEntityBuilderExtensions (26 methods) ----

    [Fact]
    public void Final_ShouldAppendModifier()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>().Final().Select(x => new { x.Id }))
            .Should().Contain("from complex_entity final");
    }

    [Fact]
    public void Settings_ShouldAppendTrailingClause()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>().Settings(("max_threads", "2")).Select(x => new { x.Id }))
            .Should().Contain(" settings max_threads = 2");
    }

    [Fact]
    public void PreWhere_ShouldEmitBeforeWhere()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .PreWhere(x => x.Id > 1L)
            .Where(x => x.Boolean == true)
            .Select(x => new { x.Id }));

        var preWhereAt = sql.IndexOf(" prewhere ", StringComparison.Ordinal);
        var whereAt = sql.IndexOf(" where ", StringComparison.Ordinal);

        preWhereAt.Should().BeGreaterThan(-1);
        whereAt.Should().BeGreaterThan(preWhereAt);
    }

    [Fact]
    public void ArrayJoin_ShouldRenderArrayJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var a = ctx.From<IArrayEntity>();

        SqlOf(ctx, a.ArrayJoin(x => x.Tags).Select(x => new { x.Id }))
            .Should().Contain(" array join tags");
    }

    [Fact]
    public void LeftArrayJoin_ShouldRenderLeftArrayJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var a = ctx.From<IArrayEntity>();

        SqlOf(ctx, a.LeftArrayJoin(x => x.Tags).Select(x => new { x.Id }))
            .Should().Contain(" left array join tags");
    }

    [Fact]
    public void ArrayJoinElement_ShouldRenderElementAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.ArrayJoinElement(x => x.Tags).Select(p => new { p.Item1.Id, Tag = p.Element }));

        sql.Should().Contain("array join tags as __nextorm_aj_element");
        sql.Should().Contain("__nextorm_aj_element as `Tag`");
    }

    [Fact]
    public void LeftArrayJoinElement_ShouldRenderLeftElementAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.LeftArrayJoinElement(x => x.Nums).Select(p => new { p.Item1.Id }))
            .Should().Contain("left array join nums as __nextorm_aj_element");
    }

    [Fact]
    public void LimitBy_ShouldAppendClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.LimitBy(2, x => x.Int).Select(x => new { x.Int }))
            .Should().Contain("limit 2 by nullableint");
    }

    [Fact]
    public void LimitBy_WithOffsetAndOrderBy_ShouldOrderThenLimitBy()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .OrderBy(x => x.Id)
            .LimitBy(3, 1, x => new { x.Int, x.Boolean })
            .Select(x => new { x.Id }))
            .Should().Contain("limit 1, 3 by nullableint, b");
    }

    [Fact]
    public void WithTotals_ShouldAppendModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupBy(x => x.Int)
            .WithTotals()
            .Select(x => new { x.Int }))
            .Should().Contain("with totals");
    }

    [Fact]
    public void Sample_ShouldAppendRatio()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>(o => o.Sample(0.1)).Select(x => new { x.Id }))
            .Should().Contain("from complex_entity sample 0.1");
    }

    [Fact]
    public void Sample_ShouldAppendRatioAndOffset()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>(o => o.Sample(0.1, 0.5)).Select(x => new { x.Id }))
            .Should().Contain("from complex_entity sample 0.1 offset 0.5");
    }

    [Fact]
    public void Global_ShouldRenderGlobalModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id, j => j.Global())
            .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" global left join ");
    }

    [Fact]
    public void WithStrictness_ShouldRenderClickHouseModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx,
            simple.LeftJoin(complex, (s, c) => s.Id == c.Id, j => j.WithStrictness(JoinStrictness.Any))
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left any join ");

        SqlOf(ctx,
            simple.Join(complex, (s, c) => s.Id == c.Id, j => j.WithStrictness(JoinStrictness.All))
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" all join ");

        SqlOf(ctx,
            simple.LeftJoin(complex, (s, c) => s.Id == c.Id, j => j.WithStrictness(JoinStrictness.Asof))
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left asof join ");
    }

    [Fact]
    public void SemiJoin_ShouldRenderLeftSemiJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.SemiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }));

        sql.Should().Contain(" left semi join ").And.Contain(" on ");
    }

    [Fact]
    public void AntiJoin_ShouldRenderLeftAntiJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, simple.AntiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left anti join ").And.Contain(" on ");
    }

    [Fact]
    public void PasteJoin_ShouldRenderPasteJoinWithoutOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.PasteJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" paste join ").And.NotContain(" on ");
        sql.Should().Contain("somestring");
    }

    [Fact]
    public void SemiAntiPasteJoin_FromQueryCommand_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complexQuery = (QueryCommand<IComplexEntity>)ctx.From<IComplexEntity>().Where(c => c.Id > 0);

        SqlOf(ctx, simple.SemiJoin(complexQuery, (s, c) => s.Id == (int)c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, simple.AntiJoin(complexQuery, (s, c) => s.Id == (int)c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, simple.PasteJoin(complexQuery).Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" paste join ").And.Contain("somestring");
    }

    [Fact]
    public void SemiAntiPasteJoin_OnNamedTable_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        var t1 = ctx.From("simple_entity");
        var t2 = ctx.From("complex_entity");

        SqlOf(ctx, t1.SemiJoin(t2, (a, b) => a.GetInt64("id") == b.GetInt64("id")).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, t1.AntiJoin(t2, (a, b) => a.GetInt64("id") == b.GetInt64("id")).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, t1.PasteJoin(t2).Select(p => new { A = p.Item1.GetInt32("id"), B = p.Item2.GetInt64("id") }))
            .Should().Contain(" paste join ");
    }

    [Fact]
    public void SemiAntiPasteJoin_OnNamedTableMixed_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        var t1 = ctx.From("simple_entity");
        var e1 = ctx.From<ISimpleEntity>();
        var e2 = ctx.From<IComplexEntity>();

        SqlOf(ctx, t1.SemiJoin(e1, (a, b) => a.GetInt64("id") == b.Id).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, t1.AntiJoin(e2, (a, b) => a.GetInt64("id") == b.Id).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, t1.PasteJoin(e2).Select(p => new { A = p.Item1.GetInt32("id"), B = p.Item2.Id }))
            .Should().Contain(" paste join ");
    }

    // ---- ClickHouseJoinedEntityBuilderExtensions (32 methods) ----

    [Fact]
    public void SemiAntiPaste_OnJoinedLhs_ShouldRenderAtEveryArity()
    {
        using var ctx = ClickHouseTestContext.Create();
        var s = ctx.From<ISimpleEntity>();
        var c = ctx.From<IComplexEntity>();

        var a2 = s.Join(c, (p, x) => p.Id == x.Id);
        var a3 = a2.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a4 = a3.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a5 = a4.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a6 = a5.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a7 = a6.Join(s, (p, x) => p.Item1.Id == x.Id);

        SqlOf(ctx, a2.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a3.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a4.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a5.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a6.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a7.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");

        SqlOf(ctx, a2.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a3.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a4.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a5.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a6.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a7.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");

        SqlOf(ctx, a2.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a3.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a4.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a5.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a6.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a7.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
    }

    [Fact]
    public void ArrayJoin_OnJoinedLhs_ShouldRenderAtEveryArity()
    {
        using var ctx = ClickHouseTestContext.Create();
        var arr = ctx.From<IArrayEntity>();
        var c = ctx.From<IComplexEntity>();

        var a2 = arr.Join(c, (p, x) => p.Id == x.Id);
        var a3 = a2.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a4 = a3.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a5 = a4.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a6 = a5.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a7 = a6.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a8 = a7.Join(arr, (p, x) => p.Item1.Id == x.Id);

        SqlOf(ctx, a2.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a3.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a4.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a5.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a6.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a7.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
        SqlOf(ctx, a8.ArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("array join t1.tags");
    }

    [Fact]
    public void LeftArrayJoin_OnJoinedLhs_ShouldRenderAtEveryArity()
    {
        using var ctx = ClickHouseTestContext.Create();
        var arr = ctx.From<IArrayEntity>();
        var c = ctx.From<IComplexEntity>();

        var a2 = arr.Join(c, (p, x) => p.Id == x.Id);
        var a3 = a2.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a4 = a3.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a5 = a4.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a6 = a5.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a7 = a6.Join(arr, (p, x) => p.Item1.Id == x.Id);
        var a8 = a7.Join(arr, (p, x) => p.Item1.Id == x.Id);

        SqlOf(ctx, a2.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a3.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a4.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a5.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a6.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a7.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
        SqlOf(ctx, a8.LeftArrayJoin(p => p.Item1.Tags).Select(p => new { p.Item1.Id })).Should().Contain("left array join t1.tags");
    }
}
