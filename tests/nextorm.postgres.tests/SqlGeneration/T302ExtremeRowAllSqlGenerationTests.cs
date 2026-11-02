using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres.Tests;

namespace NextORM.Postgres.Tests.SqlGeneration;

public class T302ExtremeRowAllSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    private static DbPreparedQueryCommand<T> Prepared<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    [Fact]
    public void Max_All_Global()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var sql = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToCommand());
        var norm = sql.ToLowerInvariant();

        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("from t302_extreme_all");
        norm.Should().Contain("join");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        norm.Should().Contain("is not null");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(2);
    }

    [Fact]
    public void Min_All_Global()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var sql = SqlOf(ctx, e.SelectWhereMin(x => x.Score, ExtremeRowTies.All).ToCommand());
        var norm = sql.ToLowerInvariant();

        norm.Should().Contain("min(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("from t302_extreme_all");
        norm.Should().Contain("join");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        norm.Should().Contain("is not null");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(2);
    }
    [Fact]
    public void Projections_All_Global_Entity_Dto_Anonymous_Record_Scalar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var entitySql = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();
        var dtoSql = SqlOf(ctx, e.SelectWhereMax(x => x.Score,
            x => new T302ExtremeAllDto { Id = x.Id, Cat = x.Cat }, ExtremeRowTies.All)).ToLowerInvariant();
        var anonSql = SqlOf(ctx, e.SelectWhereMax(x => x.Score,
            x => new { x.Id, x.Cat }, ExtremeRowTies.All)).ToLowerInvariant();
        var recordSql = SqlOf(ctx, e.SelectWhereMax(x => x.Score,
            x => new T302ExtremeAllRecord(x.Id, x.Cat), ExtremeRowTies.All)).ToLowerInvariant();
        var scalarSql = SqlOf(ctx, e.SelectWhereMax(x => x.Score,
            x => x.Cat, ExtremeRowTies.All)).ToLowerInvariant();

        foreach (var norm in new[] { entitySql, dtoSql, anonSql, recordSql, scalarSql })
        {
            norm.Should().Contain("max(");
            norm.Should().Contain("__nextorm_ext_1_0");
            norm.Should().Contain("from t302_extreme_all");
            norm.Should().Contain("join");
            norm.Should().NotContain("rank(");
            norm.Should().NotContain("row_number(");
            norm.Should().Contain("is not null");
        }

        dtoSql.Should().Contain("cat");
        anonSql.Should().Contain("cat");
        recordSql.Should().Contain("cat");
        scalarSql.Should().Contain("cat");
    }

    [Fact]
    public void NullablePayload_Distinct_OrderBy_All_Global()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All)
            .Distinct()
            .OrderBy(x => x.Cat)
            .ToCommand()).ToLowerInvariant();

        norm.Should().Contain("distinct");
        norm.Should().Contain("order by");
        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("join");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
    }

    [Fact]
    public void HiddenAliasCollision_All_Global()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllCollisionEntity>();

        var norm = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("max(");
        norm.Should().Contain("join");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");

        // A physical column named like the lowering alias and the derived aggregate column must stay
        // distinguishable: at least two distinct qualifiers carry the alias name.
        var qualifiers = System.Text.RegularExpressions.Regex
            .Matches(norm, "([a-z0-9_\\[\\]`\"]+)\\.[\\[`\"]*__nextorm_ext_1_0")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Count();
        qualifiers.Should().BeGreaterThanOrEqualTo(2);
    }
    [Fact]
    public void Max_All_Grouped_SingleNullableKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => x.Cat).ToCommand()).ToLowerInvariant();

        // Aggregate E: max(score) as __nextorm_ext_1_0, group key cat as __nextorm_ext_1_1, GROUP BY cat;
        // O joins the aggregate back on the null-safe OR-form of the nullable group key and the value alias.
        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().Contain("is null");
        norm.Should().Contain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        norm.Should().NotContain("partition by");
        // The user condition C is duplicated into E and O deliberately.
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(2);
    }

    [Fact]
    public void Min_All_Grouped_SingleNonNullableKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>();

        var norm = SqlOf(ctx, e.SelectWhereMin(x => x.Score, ExtremeRowTies.All, x => x.Id).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("min(");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        // A non-nullable group key joins with plain equality - no null-safe OR arm.
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
    }

    [Fact]
    public void Max_All_Grouped_CompositeKey_WithNullableComponent()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => new { x.Cat, x.Id }).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        norm.Should().Contain("__nextorm_ext_1_2");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        // Only the nullable component contributes the OR-form.
        norm.Should().Contain(" or ");
        norm.Should().Contain("is null");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
    }

    [Fact]
    public void Max_All_Grouped_CompositeNonNullableKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        // Composite non-nullable group key (grp, id): the aggregate E emits both group aliases and
        // groups by both columns; O joins the aggregate back on both keys with plain equality, so no
        // null-safe OR arm appears anywhere.
        var norm = SqlOf(ctx,
                e.SelectWhereMax(x => x.Score, ExtremeRowTies.All, x => new { x.Grp, x.Id }).ToCommand())
            .ToLowerInvariant();

        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        norm.Should().Contain("__nextorm_ext_1_2");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");

        // GROUP BY names both key columns, not only the first.
        var groupBy = norm.Substring(norm.IndexOf("group by", StringComparison.Ordinal));
        groupBy = groupBy.Substring(0, groupBy.IndexOf(')'));
        groupBy.Should().Contain("grp").And.Contain("id");

        // The join-back matches both keys against their own aliases - and the value alias.
        var onClause = norm.Substring(norm.IndexOf(" on ", norm.IndexOf("join", StringComparison.Ordinal), StringComparison.Ordinal));
        System.Text.RegularExpressions.Regex
            .IsMatch(onClause, "grp\\s*=\\s*[a-z0-9_\\[\\]`\"]+\\.[\\[`\"]*__nextorm_ext_1_1")
            .Should().BeTrue("the first composite key must be joined back on __nextorm_ext_1_1");
        System.Text.RegularExpressions.Regex
            .IsMatch(onClause, "\\.id\\s*=\\s*[a-z0-9_\\[\\]`\"]+\\.[\\[`\"]*__nextorm_ext_1_2")
            .Should().BeTrue("the second composite key must be joined back on __nextorm_ext_1_2");
        onClause.Should().Contain("__nextorm_ext_1_0");
    }

    [Fact]
    public void Projection_Distinct_OrderBy_All_Grouped()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(
                x => x.Score,
                x => new T302ExtremeAllDto { Id = x.Id, Cat = x.Cat },
                ExtremeRowTies.All,
                x => x.Cat)
            .Distinct()
            .OrderBy(x => x.Cat)).ToLowerInvariant();

        norm.Should().Contain("distinct");
        norm.Should().Contain("order by");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(2);
    }

    [Fact]
    public void MappedColumns_HiddenAlias_All_Grouped()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllCollisionEntity>();

        // The value key's physical column is itself named like the lowering's aggregate alias; the
        // group key is non-nullable, so the join is plain equality apart from that collision.
        var norm = SqlOf(ctx, e.SelectWhereMax(x => x.Collide, ExtremeRowTies.All, x => x.Id).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("max(");
        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_1");
        norm.Should().Contain("group by");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        var qualifiers = System.Text.RegularExpressions.Regex
            .Matches(norm, "([a-z0-9_\\[\\]`\"]+)\\.[\\[`\"]*__nextorm_ext_1_0")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Count();
        qualifiers.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Max_All_Composite_Global_Arity2()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Score, x.Sub }, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();

        // E_1 aggregates v1 inside C; E_2 aggregates v2 inside the v1 prefix E_1 selected, so the
        // result is lexicographic ((9,2) beats (8,100)) instead of component-wise.
        norm.Should().Contain("max(score)");
        norm.Should().Contain("max(sub)");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");

        // Every stage output carries an explicit alias, and the next stage / O reads that alias
        // through a qualified reference - never the physical column name.
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_1_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v2").Should().BeTrue();

        norm.Should().Contain("is not null");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        // C is rendered into E_1, E_2 and O deliberately (n + 1 occurrences).
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public void Min_All_Composite_Global_Arity2()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMin(x => new { x.Score, x.Sub }, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("min(score)");
        norm.Should().Contain("min(sub)");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        norm.Should().Contain("is not null");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public void Min_All_Composite_Global_Arity3_Staged_Aliased_PrefixAggregation()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMin(x => new { x.Score, x.Sub, x.Flag }, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();

        // Prefix-only aggregation with MIN: each component is aggregated exactly once, by its own stage,
        // and the surviving prefix is carried forward as an explicitly aliased pass-through.
        (norm.Split(new[] { "min(score)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "min(sub)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "min(flag)" }, StringSplitOptions.None).Length - 1).Should().Be(1);

        foreach (var alias in new[]
                 {
                     "__nextorm_ext_1_v1",
                     "__nextorm_ext_2_v1", "__nextorm_ext_2_v2",
                     "__nextorm_ext_3_v1", "__nextorm_ext_3_v2", "__nextorm_ext_3_v3",
                 })
        {
            System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}" + alias).Should().BeTrue($"{alias} must be declared explicitly");
        }

        // O joins the final stage back on all three value aliases.
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v3").Should().BeTrue();

        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        // E_1, E_2, E_3 and O each render C (n + 1 occurrences).
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(4);
    }

    [Fact]
    public void Min_All_Composite_Grouped_Arity3_CompositeKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        // Composite non-nullable group key (grp, id): every stage carries both group aliases, groups by
        // them plus the surviving prefix, and joins back with plain equality.
        var norm = SqlOf(ctx, e.SelectWhereMin(x => new { x.Score, x.Sub, x.Flag }, ExtremeRowTies.All, x => new { x.Grp, x.Id }).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_1_g1");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_3_g0");
        norm.Should().Contain("__nextorm_ext_3_g1");
        norm.Should().Contain("__nextorm_ext_3_v1");
        norm.Should().Contain("__nextorm_ext_3_v2");
        norm.Should().Contain("__nextorm_ext_3_v3");
        (norm.Split(new[] { "min(score)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "min(sub)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "min(flag)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        var groupBy = norm.Substring(norm.IndexOf("group by", StringComparison.Ordinal));
        groupBy = groupBy.Substring(0, groupBy.IndexOf(')'));
        groupBy.Should().Contain("grp").And.Contain("id");
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(4);
    }

    [Fact]
    public void Max_All_Composite_Global_Arity3_Staged_Aliased_PrefixAggregation()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Score, x.Sub, x.Flag }, ExtremeRowTies.All).ToCommand()).ToLowerInvariant();

        // Prefix-only aggregation: each component is aggregated exactly once, by its own stage, and the
        // surviving prefix is carried forward as an explicitly aliased pass-through.
        (norm.Split(new[] { "max(score)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "max(sub)" }, StringSplitOptions.None).Length - 1).Should().Be(1);
        (norm.Split(new[] { "max(flag)" }, StringSplitOptions.None).Length - 1).Should().Be(1);

        foreach (var alias in new[]
                 {
                     "__nextorm_ext_1_v1",
                     "__nextorm_ext_2_v1", "__nextorm_ext_2_v2",
                     "__nextorm_ext_3_v1", "__nextorm_ext_3_v2", "__nextorm_ext_3_v3",
                 })
        {
            System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}" + alias).Should().BeTrue($"{alias} must be declared explicitly");
        }

        // O joins the final stage back on all three value aliases.
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_3_v3").Should().BeTrue();

        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        // E_1, E_2, E_3 and O each render C (n + 1 occurrences).
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(4);
    }

    [Fact]
    public void Max_All_Composite_Grouped_Arity2_NonNullableKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Score, x.Sub }, ExtremeRowTies.All, x => x.Grp).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("max(score)");
        norm.Should().Contain("max(sub)");
        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_2_g0");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        // A non-nullable group key joins with plain equality - no null-safe OR arm.
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public void Min_All_Composite_Grouped_Arity2_NonNullableKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMin(x => new { x.Score, x.Sub }, ExtremeRowTies.All, x => x.Grp).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("min(score)");
        norm.Should().Contain("min(sub)");
        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_2_g0");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public void Max_All_Composite_Grouped_Arity3_CompositeKey()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        // Composite non-nullable group key (grp, id): every stage carries both group aliases, groups by
        // them plus the surviving prefix, and joins back with plain equality.
        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Score, x.Sub, x.Flag }, ExtremeRowTies.All, x => new { x.Grp, x.Id }).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_1_g1");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_3_g0");
        norm.Should().Contain("__nextorm_ext_3_g1");
        norm.Should().Contain("__nextorm_ext_3_v1");
        norm.Should().Contain("__nextorm_ext_3_v2");
        norm.Should().Contain("__nextorm_ext_3_v3");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        var groupBy = norm.Substring(norm.IndexOf("group by", StringComparison.Ordinal));
        groupBy = groupBy.Substring(0, groupBy.IndexOf(')'));
        groupBy.Should().Contain("grp").And.Contain("id");
        norm.Should().NotContain(" or ");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(4);
    }

    [Fact]
    public void Max_All_Composite_Grouped_NullableKey_OrForm()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Score, x.Id }, ExtremeRowTies.All, x => x.Cat).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_2_g0");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        // One null-safe OR arm per join level: E_2 and O both match the nullable group key.
        (norm.Split(new[] { " or " }, StringSplitOptions.None).Length - 1).Should().Be(2);
        norm.Should().Contain("is null");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }

    [Fact]
    public void MappedColumns_HiddenAliasCollision_All_Composite()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeAllCollisionEntity>();

        // The first value component's physical column is itself named like an S1 lowering alias; the
        // S3 stage aliases stay distinct and the join-back reads those aliases, never the collision name.
        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.Collide, x.Score }, ExtremeRowTies.All, x => x.Id).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("__nextorm_ext_1_0");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        norm.Should().Contain("is not null");
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
    }

    [Fact]
    public void Projection_Distinct_OrderBy_All_Composite()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T302ExtremeCompositeEntity>().Where(x => x.Score > 5);

        var norm = SqlOf(ctx, e.SelectWhereMax(
                x => new { x.Score, x.Sub },
                x => new T302ExtremeAllDto { Id = x.Id, Cat = x.Cat },
                ExtremeRowTies.All,
                x => x.Grp)
            .Distinct()
            .OrderBy(x => x.Cat)).ToLowerInvariant();

        norm.Should().Contain("distinct");
        norm.Should().Contain("order by");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().Contain("__nextorm_ext_2_g0");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        // A projected command registers its projection source before the join source, so the stage
        // aliases must resolve by parameter identity: every back-reference keeps its table qualifier.
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_1_g0").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v1").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "\\.{1}.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
        (norm.Split(new[] { "score > 5" }, StringSplitOptions.None).Length - 1).Should().Be(3);
    }
    [Fact]
    public void Max_All_Composite_Global_ConvertedValueKey()
    {
        using var ctx = PostgresTestContext.Create();
        // The value component carries a registered ValueConverter (enum -> text). The constant it is
        // compared with is bound in the provider representation, so the predicate never compares the
        // text column with the enum's numeric form.
        var e = ctx.From<T302ExtremeConvertedEntity>().Where(x => x.State == T302ExtremeConvertedState.Active);

        var prepared = Prepared(ctx, e.SelectWhereMax(x => new { x.State, x.Sub }, ExtremeRowTies.All).ToCommand());
        var norm = Normalize(prepared.DbCommand.CommandText).ToLowerInvariant();

        // Every stage aggregates/reads the physical (provider) storage column and the join-back reads
        // the stage alias of that same storage.
        norm.Should().Contain("max(state)");
        norm.Should().Contain("max(sub)");
        norm.Should().Contain("__nextorm_ext_1_v1");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        System.Text.RegularExpressions.Regex.IsMatch(norm, "as\\s*.{0,3}__nextorm_ext_2_v2").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(norm, "state\\s*=\\s*[a-z0-9_\\[\\]`\"]+\\.[\\[`\"]*__nextorm_ext_2_v1").Should().BeTrue();
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");

        // C is rendered into E_1, E_2 and O (n + 1); each occurrence binds the converted provider value.
        prepared.DbCommand.Parameters.Count.Should().Be(3);
        prepared.DbCommand.Parameters.Cast<DbParameter>().Should().OnlyContain(p => (string?)p.Value == "Active");
    }

    [Fact]
    public void Max_All_Composite_Grouped_ConvertedGroupKey()
    {
        using var ctx = PostgresTestContext.Create();
        // The group key is a converted enum (stored as text): the stages group by and join back on the
        // physical column, and the non-nullable model enum keeps the plain equality form.
        var e = ctx.From<T302ExtremeConvertedEntity>();

        var norm = SqlOf(ctx, e.SelectWhereMax(x => new { x.State, x.Sub }, ExtremeRowTies.All, x => x.Grp).ToCommand()).ToLowerInvariant();

        norm.Should().Contain("max(state)");
        norm.Should().Contain("max(sub)");
        norm.Should().Contain("__nextorm_ext_1_g0");
        norm.Should().Contain("__nextorm_ext_2_g0");
        norm.Should().Contain("__nextorm_ext_2_v1");
        norm.Should().Contain("__nextorm_ext_2_v2");
        norm.Should().Contain("group by");
        norm.Should().Contain("join");
        norm.Should().NotContain(" or ");
        System.Text.RegularExpressions.Regex.IsMatch(norm, "grp\\s*=\\s*[a-z0-9_\\[\\]`\"]+\\.[\\[`\"]*__nextorm_ext_2_g0").Should().BeTrue();
        norm.Should().NotContain("rank(");
        norm.Should().NotContain("row_number(");
    }
}

[SqlTable("t302_extreme_all")]
public sealed class T302ExtremeAllEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("score")] public int? Score { get; set; }
    [Column("cat")] public string? Cat { get; set; }
}

public sealed class T302ExtremeAllDto
{
    public int Id { get; set; }
    public string? Cat { get; set; }
}

public sealed record T302ExtremeAllRecord(int Id, string? Cat);

[SqlTable("t302_extreme_composite")]
public sealed class T302ExtremeCompositeEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("grp")] public int Grp { get; set; }
    [Column("score")] public int? Score { get; set; }
    [Column("sub")] public long Sub { get; set; }
    [Column("flag")] public int Flag { get; set; }
    [Column("cat")] public string? Cat { get; set; }
}

[SqlTable("t302_extreme_all")]
public sealed class T302ExtremeAllCollisionEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("score")] public int? Score { get; set; }
    [Column("__nextorm_ext_1_0")] public int? Collide { get; set; }
}

public enum T302ExtremeConvertedState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("t302_extreme_converted")]
public sealed class T302ExtremeConvertedEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<T302ExtremeConvertedState>))]
    public T302ExtremeConvertedState State { get; set; }
    [Column("sub")] public int Sub { get; set; }
    [Column("grp")]
    [ValueConverter(typeof(EnumToStringConverter<T302ExtremeConvertedState>))]
    public T302ExtremeConvertedState Grp { get; set; }
    [Column("payload")] public int Payload { get; set; }
}
