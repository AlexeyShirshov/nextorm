using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite.Tests;

namespace NextORM.Sqlite.Tests.SqlGeneration;

public class T302ExtremeRowAllSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    [Fact]
    public void Max_All_Global()
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
}

[SqlTable("t302_extreme_all")]
public sealed class T302ExtremeAllCollisionEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("score")] public int? Score { get; set; }
    [Column("__nextorm_ext_1_0")] public int? Collide { get; set; }
}
