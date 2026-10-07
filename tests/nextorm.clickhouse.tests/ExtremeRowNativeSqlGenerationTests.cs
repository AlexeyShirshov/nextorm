using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation asserts for ClickHouse's native <c>SelectWhereMax</c>/<c>SelectWhereMin</c> strategy:
/// the global <c>argMin/argMax(tuple(payload), key)</c> aggregate and the grouped form with
/// <c>group by</c>. The portable window-rank lowering (and every ineligible command) is pinned
/// separately in <see cref="SqlGenerationTests"/> and <see cref="ExtremeRowNativeSqlGenerationTests"/>.
/// </summary>
public class ExtremeRowNativeSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static string Dequoted(string sql) => sql
        .Replace("\r\n", " ")
        .Replace('\n', ' ')
        .Replace("\"", string.Empty)
        .Replace("[", string.Empty)
        .Replace("]", string.Empty)
        .Replace("`", string.Empty);

    private static string OuterSelectList(string sql)
    {
        const string marker = "select ";
        var start = sql.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return sql;
        start += marker.Length;
        var end = sql.IndexOf(" from ", start, StringComparison.Ordinal);
        return end < 0 ? sql[start..] : sql[start..end];
    }

    // --- global: argMin/argMax(tuple(payload), key) ------------------------------------------

    [Fact]
    public void SelectWhereMax_GlobalWholeRow_ShouldRenderArgMaxTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1).ToCommand()));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain("), k1)");
        norm.Should().Contain("k1 is not null");
        norm.Should().Contain("having count() > 0");
        norm.Should().Contain("tupleElement(__nextorm_extreme_tuple, 1)");
        norm.Should().NotContain("row_number()");
        norm.Should().NotContain("rank()");
        OuterSelectList(norm).Should().NotContain("__nextorm_extreme_tuple");
    }

    [Fact]
    public void SelectWhereMin_GlobalWholeRow_ShouldRenderArgMinTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.K1).ToCommand()));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain("), k1)");
        norm.Should().NotContain("argMax");
        norm.Should().Contain("k1 is not null");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_GlobalProjection_ShouldRenderArgMaxTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, x => new { x.Id, x.Payload })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain("), k1)");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().Contain("id").And.Contain("payload").And.NotContain("__nextorm_extreme_tuple");
    }

    [Fact]
    public void SelectWhereMin_GlobalProjection_ShouldRenderArgMinTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.K1, x => new { x.Id, x.Payload })));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().NotContain("argMax");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().Contain("id").And.Contain("payload");
    }

    // --- grouped: <groups>, argMin/argMax(tuple(payload), key) ... group by -------------------

    [Fact]
    public void SelectWhereMax_GroupedWholeRow_ShouldRenderArgMaxTuplePerGroup()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, ExtremeRowTies.One, x => new { x.Id }).ToCommand()));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain("), k1)");
        norm.Should().Contain("group by id");
        norm.Should().Contain("k1 is not null");
        norm.Should().NotContain("having count()");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMin_GroupedWholeRow_ShouldRenderArgMinTuplePerGroup()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.K1, ExtremeRowTies.One, x => new { x.Id }).ToCommand()));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain("group by id");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_GroupedProjection_ShouldRenderArgMaxTuplePerGroup()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, x => new { x.Id, x.Payload }, ExtremeRowTies.One, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain("group by id");
        norm.Should().NotContain("row_number()");
        OuterSelectList(norm).Should().Contain("id").And.Contain("payload").And.NotContain("__nextorm_extreme_tuple");
    }

    [Fact]
    public void SelectWhereMin_GroupedProjection_ShouldRenderArgMinTuplePerGroup()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.K1, x => new { x.Id, x.Payload }, ExtremeRowTies.One, x => new { x.Id })));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain("group by id");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("row_number()");
    }

    // --- composite keys: lexicographic tuple argument ----------------------------------------

    [Fact]
    public void SelectWhereMax_CompositeKey_ShouldPassLexicographicTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.K1, x.K2 }, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (k1, k2))");
        norm.Should().Contain("k1 is not null and k2 is not null");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMin_CompositeKey_ShouldPassLexicographicTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => new { x.K1, x.K2 }, x => new { x.Id })));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain(", (k1, k2))");
        norm.Should().Contain("k1 is not null and k2 is not null");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_CompositeGroupKey_ShouldGroupByEveryComponent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, x => new { x.G, x.Payload }, ExtremeRowTies.One, x => new { x.Id, x.K2 })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain("group by id, k2");
        norm.Should().NotContain("row_number()");
    }

    // --- null filtering / empty-global suppression -------------------------------------------

    [Fact]
    public void SelectWhereMax_ShouldFilterNullKeyComponents()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.K1, x.K2 }, x => new { x.Id })));

        norm.Should().Contain("k1 is not null and k2 is not null");
    }

    [Fact]
    public void SelectWhereMax_Global_ShouldSuppressEmptyInput()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1).ToCommand()));

        // The global aggregate over an empty filtered input yields one default row; counting the
        // filtered input and requiring it positive suppresses that row.
        norm.Should().Contain("having count() > 0");
    }

    // --- quoting and casing ------------------------------------------------------------------

    [Fact]
    public void SelectWhereMax_Grouped_ShouldBacktickNativeAliases()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<ExtremeNativeEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.K1, ExtremeRowTies.One, x => new { x.Id }).ToCommand());

        sql.Should().Contain("`k1`");
        sql.Should().Contain("`__nextorm_extreme_tuple`");
        sql.Should().Contain("group by `id`");
    }

    [Fact]
    public void SelectWhereMax_SpecialCharacterNames_ShouldEscapeNativeAliases()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IExtremeSpecialNativeEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.K, x => new { x.Payload }));

        sql.Should().Contain(@"`k\\ey`");
        sql.Should().Contain(@"`pay``load`");
        sql.Should().NotContain(@"`k\ey`");
    }

    [Fact]
    public void SelectWhereMax_Global_ShouldRespectUppercaseKeywordCase()
    {
        using var ctx = ClickHouseTestContext.CreateUppercase();
        var e = ctx.From<ExtremeNativeEntity>();

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.K1).ToCommand());

        sql.Should().Contain("HAVING COUNT() > 0");
        sql.Should().Contain("argMax(");
    }

    // --- fallback: All and ineligible shapes keep the portable window path --------------------

    [Fact]
    public void SelectWhereMax_AllTies_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, ExtremeRowTies.All).ToCommand()));

        norm.Should().Contain("rank() over (order by k1 desc)");
        norm.Should().Contain("= 1");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMax_AllTiesGrouped_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1, x => new { x.Id }, ExtremeRowTies.All, x => new { x.Id })));

        norm.Should().Contain("rank() over (partition by id order by k1 desc)");
        norm.Should().NotContain("argMax");
    }

    // --- floating keys: direction-aware NaN adaptation ----------------------------------------

    [Fact]
    public void SelectWhereMax_FloatKey_ShouldRenderArgMaxWithNaNAdaptation()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.D, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (isNaN(d) = 0, d))");
        norm.Should().Contain("d is not null");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
        norm.Should().NotContain("toFloat64");
    }

    [Fact]
    public void SelectWhereMin_FloatKey_ShouldRenderArgMinWithReversedNaNAdaptation()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.D, x => new { x.Id })));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain(", (isNaN(d), d))");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_NullableFloatKey_ShouldRenderArgMaxWithNaNAdaptation()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Dn, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (isNaN(dn) = 0, dn))");
        norm.Should().Contain("dn is not null");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_Float32Key_ShouldWidenToFloat64WithNaNAdaptation()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.F, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (isNaN(f) = 0, toFloat64(f)))");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMin_NullableFloat32Key_ShouldWidenToFloat64WithReversedNaNAdaptation()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Fn, x => new { x.Id })));

        norm.Should().Contain("argMin(tuple(");
        norm.Should().Contain(", (isNaN(fn), toFloat64(fn)))");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_CompositeIntegralThenFloat_ShouldInsertFlagBeforeFloatComponent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.K, x.D }, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (k, isNaN(d) = 0, d))");
        norm.Should().Contain("k is not null and d is not null");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_CompositeFloatThenIntegral_ShouldInsertFlagBeforeLeadingFloatComponent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.D, x.K }, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (isNaN(d) = 0, d, k))");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_ThreeComponentKeyWithTrailingFloat_ShouldAdaptOnlyTheFloatComponent()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.K, x.Id, x.D }, x => new { x.Id })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (k, id, isNaN(d) = 0, d))");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_GroupedFloatKey_ShouldRenderAdaptedKeyPerGroup()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.D, x => new { x.Id }, ExtremeRowTies.One, x => new { x.K })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (isNaN(d) = 0, d))");
        norm.Should().Contain("group by k");
        norm.Should().NotContain("having count()");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_IntegralKeyWithFloatingPayload_ShouldKeepPortableWindowLowering()
    {
        // C1: float/double payload carriers are admitted only for a floating extreme key. An integral
        // key with a mapped float/double payload must restore the pre-D150 behavior and stay portable,
        // not flip to native just because the payload type is now tuple-representable.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatingPayloadIntegralKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by k desc)");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMax_FourComponentIntegralKey_ShouldStayNative()
    {
        // C2: the arity cap applies only to keys with a floating component. A purely integral composite
        // was never capped (it renders a plain lexicographic tuple), so a 4-component integral key must
        // remain native rather than being silently demoted to portable.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.Id, x.K1, x.K2, x.Flag }, x => new { x.Payload })));

        norm.Should().Contain("argMax(tuple(");
        norm.Should().Contain(", (id, k1, k2, flag))");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_IntegralWidthKeys_ShortAndLong_ShouldStayNative()
    {
        // Branch coverage: the IsIntegralKeyType short/long (and nullable) arms had no dedicated test.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IIntegralWidthKeyNativeEntity>();

        Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.S, x => new { x.Id })))
            .Should().Contain("argMax(tuple(").And.Contain(", s)");
        Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.L, x => new { x.Id })))
            .Should().Contain("argMin(tuple(").And.Contain(", l)").And.NotContain("argMax");
        Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Sn, x => new { x.Id })))
            .Should().Contain("argMax(tuple(").And.Contain(", sn)");
        Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Ln, x => new { x.Id })))
            .Should().Contain("argMin(tuple(").And.Contain(", ln)");
    }

    [Fact]
    public void SelectWhereMax_ConverterKey_ShouldKeepPortableWindowLowering()
    {
        // Branch coverage: the key UsesConverter rejection (the existing test only covers a converter
        // payload). A converter-backed key is not a plain mapped column, so the portable lowering stays.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IConverterKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.ConvertedKey, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    // --- still-unsupported: a floating component qualifies as a key only, not as a group/expression ---

    [Fact]
    public void SelectWhereMax_FloatingGroupKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Id, x => new { x.Id }, ExtremeRowTies.One, x => new { x.D })));

        norm.Should().Contain("row_number() over (partition by d order by id desc)");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_FloatingExpressionKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.D + 1, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_FourComponentFloatingKey_ShouldKeepPortableWindowLowering()
    {
        // The frozen allowlist was proven for single/two/three-component keys only; arity > 3 is a
        // deferred shape and must keep the portable lowering even though every component is an allowed
        // direct mapped column.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IFloatNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => new { x.K, x.Id, x.D, x.Dn }, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_Float16Key_ShouldKeepPortableWindowLowering()
    {
        // Float16 is outside the approved matrix; even a direct mapped Half column must not be rendered
        // through the NaN adaptation.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IUnsupportedFloatingKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.H, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMin_DecimalKey_ShouldKeepPortableWindowLowering()
    {
        // Decimal is outside the approved matrix (neither integral nor float/double); it keeps the
        // portable lowering.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IUnsupportedFloatingKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Dec, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMin");
    }

    [Fact]
    public void SelectWhereMax_NullableDecimalKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IUnsupportedFloatingKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.DecN, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_StringKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IStringKeyNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Name, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by name desc)");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_DateTimeKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Datetime, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by dt desc)");
        norm.Should().NotContain("argMax");
    }

    [Fact]
    public void SelectWhereMax_UnsupportedPayload_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // The key is an integral direct column, but the payload carries bool/DateTime, which the
        // tuple strategy does not support, so the portable window lowering is kept.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.Int, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by nullableint desc)");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMin_UnsupportedPayload_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMin(x => x.Int, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by nullableint)");
        norm.Should().NotContain("argMin");
    }

    [Fact]
    public void SelectWhereMax_ConverterPayload_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IConverterPayloadNativeEntity>();

        // The extreme key is a supported direct mapped integral column, but a payload property carries
        // a value converter, so the whole row cannot be reconstructed as a raw tuple and the portable
        // window lowering is kept.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by k desc)");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMax_ComputedPayload_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComputedPayloadNativeEntity>();

        // A computed payload column is not a plain mapped column (its IsDirectMappedColumn is false),
        // so even with an integral direct key it keeps the portable lowering.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by k desc)");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMax_ExpressionKey_ShouldKeepPortableWindowLowering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        // A computed expression is not a direct mapped column, so it is not natively eligible even
        // though the result is integral and the payload is supported.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K1 + 1, x => new { x.Id })));

        norm.Should().Contain("row_number() over (order by");
        norm.Should().NotContain("argMax");
    }

    // --- unsupported modifiers still reject before dispatch ----------------------------------

    [Fact]
    public void SelectWhereMax_WithLimitBy_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var act = () => SqlOf(ctx, e.LimitBy(2, x => x.K1).SelectWhereMax(x => x.K1).Select(x => new { x.Id }));

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with LIMIT BY*");
    }

    [Fact]
    public void SelectWhereMax_WithPreWhere_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        var act = () => SqlOf(ctx, e.PreWhere(x => x.Id > 1).SelectWhereMax(x => x.K1).Select(x => new { x.Id }));

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with PREWHERE*");
    }

    [Fact]
    public void SelectWhereMax_WithArrayJoin_ShouldRejectBeforeNativeDispatch()
    {
        using var ctx = ClickHouseTestContext.Create();
        var a = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, a.ArrayJoin(x => x.Tags).SelectWhereMax(x => x.Id).Select(x => new { x.Id }));

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with ARRAY JOIN*");
    }

    // --- name-addressed source: no mapped payload, so the native renderer must decline, not throw -----

    [Fact]
    public void SelectWhereMax_UnmappedRawSource_ShouldFallBackToPortableLowering()
    {
        using var ctx = ClickHouseTestContext.Create();

        // A name-addressed source (From("table")) is read through TableAlias and has no mapped payload
        // metadata for the tuple renderer, so it must keep the portable window lowering instead of
        // failing the read with "requires a mapped entity source".
        var command = ctx.From("raw_source_144")
            .SelectWhereMax(t => t.GetInt32("k"), t => new { Id = t.GetInt32("id") });

        var norm = Dequoted(SqlOf(ctx, command));

        norm.Should().Contain("row_number() over (order by k desc)");
        norm.Should().NotContain("argMax");
        norm.Should().NotContain("having count()");
    }

    [Fact]
    public void SelectWhereMax_UnmappedRawSource_WithPaging_ShouldStillRejectBeforeDispatch()
    {
        using var ctx = ClickHouseTestContext.Create();

        // The compatibility guard runs before the native/portable decision, so a forbidden modifier is
        // still rejected for a name-addressed source.
        var command = ctx.From("raw_source_144").Limit(1)
            .SelectWhereMax(t => t.GetInt32("k"), t => new { Id = t.GetInt32("id") });

        var act = () => SqlOf(ctx, command);

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*SelectWhereMax/SelectWhereMin cannot be combined with paging*");
    }

    // --- alias collision: mapped columns named like the internal aliases ----------------------

    [Fact]
    public void SelectWhereMax_Global_PayloadNamedLikeSourceAlias_ShouldPickCollisionFreeAliases()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeAliasNativeEntity>();

        // Two payload columns are physically named "__nextorm_extreme_src" and
        // "__nextorm_extreme_tuple", the renderer's source-subquery and tuple alias bases. Reusing
        // either verbatim would make the aggregate or the tuple extraction reference an ambiguous name,
        // so both aliases must be extended until free.
        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K).ToCommand()));

        norm.Should().Contain("as __nextorm_extreme_src_");
        norm.Should().Contain("as __nextorm_extreme_tuple_");
        norm.Should().NotContain("as __nextorm_extreme_src ");
        norm.Should().NotContain("as __nextorm_extreme_tuple ");
        norm.Should().Contain("having count() > 0");
        norm.Should().NotContain("row_number()");
    }

    [Fact]
    public void SelectWhereMax_Grouped_GroupNamedLikeTupleAlias_ShouldPickCollisionFreeTupleAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeAliasNativeEntity>();

        var norm = Dequoted(SqlOf(ctx, e.SelectWhereMax(x => x.K, x => new { x.Id }, ExtremeRowTies.One, x => new { x.TupleLike })));

        norm.Should().Contain("group by __nextorm_extreme_tuple");
        norm.Should().Contain("as __nextorm_extreme_tuple_");
        norm.Should().Contain("as __nextorm_extreme_src_");
        norm.Should().NotContain("row_number()");
    }

    // --- parameterization: the native predicate keeps the bound parameter --------------------

    [Fact]
    public void SelectWhereMax_WithParameterizedWhere_ShouldKeepTheBoundParameterInTheNativePredicate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ExtremeNativeEntity>();

        // A runtime placeholder in the source condition must survive the native argMax dispatch as a
        // bound parameter: the native source rebuild must not inline it while re-rendering the WHERE.
        var command = Prepare(ctx, e
            .Where(x => x.K1 == SqlFunctions.Parameter<int>(0))
            .SelectWhereMax(x => x.K1).ToCommand());

        var sql = Dequoted(Normalize(command.DbCommand.CommandText));

        sql.Should().Contain("argMax(tuple(");
        sql.Should().Contain("having count() > 0");
        sql.Should().Contain("k1 = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }
}

/// <summary>A concrete (materializable) entity whose whole payload is tuple-supported.</summary>
[SqlTable("extreme_native_entity")]
public class ExtremeNativeEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("g")]
    public string? G { get; set; }

    [Column("k1")]
    public int? K1 { get; set; }

    [Column("k2")]
    public int? K2 { get; set; }

    [Column("payload")]
    public string? Payload { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }
}

/// <summary>
/// A floating-point key matrix whose payload is tuple-supported (the floating key columns are carried
/// in the payload tuple and now admissible there).
/// </summary>
[SqlTable("float_native_entity")]
public interface IFloatNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("d")]
    double D { get; set; }

    [Column("dn")]
    double? Dn { get; set; }

    [Column("f")]
    float F { get; set; }

    [Column("fn")]
    float? Fn { get; set; }

    [Column("k")]
    int? K { get; set; }

    [Column("g")]
    string? G { get; set; }

    [Column("payload")]
    string? Payload { get; set; }
}

/// <summary>A string key whose payload is tuple-supported.</summary>
[SqlTable("string_key_native_entity")]
public interface IStringKeyNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

/// <summary>
/// Key shapes outside the proven floating allowlist: a Float16 (CLR <see cref="Half"/>) and a decimal
/// key. Neither is integral nor float/double, so the native renderer must decline and leave the
/// portable lowering in place.
/// </summary>
[SqlTable("unsupported_float_native_entity")]
public interface IUnsupportedFloatingKeyNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("h")]
    Half H { get; set; }

    [Column("dec")]
    decimal Dec { get; set; }

    [Column("decn")]
    decimal? DecN { get; set; }

    [Column("payload")]
    string? Payload { get; set; }
}

/// <summary>Converts an <see cref="int"/> payload column to text, so the tuple strategy must decline.</summary>
public sealed class IntToTextConverter : ValueConverter<int, string>
{
    public override string? ConvertToProvider(int model) => model.ToString(CultureInfo.InvariantCulture);

    public override int ConvertFromProvider(string? provider) => int.Parse(provider!, CultureInfo.InvariantCulture);
}

/// <summary>
/// An integral direct key whose payload carries mapped <c>float</c>/<c>double</c> columns. The payload
/// types are tuple-representable only as carriers of a floating extreme key, so with an integral key the
/// native renderer must decline and keep the portable lowering (the pre-D150 payload behavior).
/// </summary>
[SqlTable("floating_payload_integral_key_native_entity")]
public interface IFloatingPayloadIntegralKeyNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? K { get; set; }

    [Column("f")]
    float F { get; set; }

    [Column("d")]
    double D { get; set; }
}

/// <summary>
/// Integral extreme keys of every supported width (<see cref="short"/>/<see cref="long"/> and their
/// nullable forms), none of which had a dedicated eligibility test.
/// </summary>
[SqlTable("integral_width_key_native_entity")]
public interface IIntegralWidthKeyNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("s")]
    short S { get; set; }

    [Column("l")]
    long L { get; set; }

    [Column("sn")]
    short? Sn { get; set; }

    [Column("ln")]
    long? Ln { get; set; }

    [Column("payload")]
    string? Payload { get; set; }
}

/// <summary>An integral extreme key that itself carries a value converter, so the native path declines.</summary>
[SqlTable("converter_key_native_entity")]
public interface IConverterKeyNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("converted_key")]
    [ValueConverter(typeof(IntToTextConverter))]
    int ConvertedKey { get; set; }

    [Column("payload")]
    string? Payload { get; set; }
}

/// <summary>An integral direct key with a converter-backed payload column.</summary>
[SqlTable("converter_payload_native_entity")]
public interface IConverterPayloadNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? K { get; set; }

    [Column("converted")]
    [ValueConverter(typeof(IntToTextConverter))]
    int Converted { get; set; }
}

/// <summary>An integral direct key with a computed payload column.</summary>
[SqlTable("computed_payload_native_entity")]
public interface IComputedPayloadNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k")]
    int? K { get; set; }

    [Column("computed_payload")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    int Computed { get; set; }
}

/// <summary>An entity whose mapped columns are named like the renderer's internal aliases.</summary>
[SqlTable("extreme_alias_native_entity")]
public class ExtremeAliasNativeEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Physical name deliberately equals the renderer's source-subquery alias base.</summary>
    [Column("__nextorm_extreme_src")]
    public int? SourceLike { get; set; }

    /// <summary>Physical name deliberately equals the renderer's tuple alias base.</summary>
    [Column("__nextorm_extreme_tuple")]
    public int? TupleLike { get; set; }

    [Column("k")]
    public int? K { get; set; }
}

/// <summary>A native extreme-row shape whose mapped key/payload names carry a backtick and a backslash.</summary>
[SqlTable("extreme_special_native_entity")]
public interface IExtremeSpecialNativeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("k\\ey")]
    int? K { get; set; }

    [Column("pay`load")]
    string? Payload { get; set; }
}
