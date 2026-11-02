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

[SqlTable("t302_extreme_all")]
public sealed class T302ExtremeAllCollisionEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Column("score")] public int? Score { get; set; }
    [Column("__nextorm_ext_1_0")] public int? Collide { get; set; }
}
