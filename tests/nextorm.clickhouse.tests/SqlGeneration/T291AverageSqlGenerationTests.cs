using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse.Tests;
using NextORM.Core;

namespace nextorm.clickhouse.tests.SqlGeneration;

/// <summary>
/// T291 (#291) SQL generation on ClickHouse: an integral AVG aggregates the native column, DISTINCT is
/// placed before that argument and a window AVG applies the same policy before <c>OVER</c>. Floating
/// and decimal selectors never receive an integral-widening cast. Only SQL is prepared; no database
/// is opened.
/// </summary>
public class T291AverageSqlGenerationTests
{
    private static IDataContext CreateContext()
    {
        var ctx = ClickHouseTestContext.Create();
        ctx.From<IT291AvgEntity>();
        return ctx;
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void IntegerAverage_ShouldKeepTheNativeArgument()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.I)));

        sql.Should().Contain("if(count(i)=0, cast(null as Nullable(Float64)), avg(i))",
            "an integral AVG aggregates the native column and is guarded by the empty-input eligibility check; SQL: " + sql);
        sql.Should().NotContain("cast(i", "the native argument must not be widened; SQL: " + sql);
    }

    [Fact]
    public void DistinctAverage_ShouldPlaceDistinctBeforeTheArgument()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)));

        sql.Should().Contain("if(count(i)=0, cast(null as Nullable(Float64)), avg(distinct i))",
            "the eligibility check uses the undecorated argument and distinct precedes the aggregate argument; SQL: " + sql);
        sql.Should().NotContain("cast(i", "the native argument must not be widened; SQL: " + sql);
    }

    [Fact]
    public void WindowAverage_ShouldApplyTheSameArgumentPolicyBeforeOver()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Sql.avg_over(x.I).Over() }));

        sql.Should().Contain("if(count(i) over ()=0, cast(null as Nullable(Float64)), avg(i) over ())",
            "the window AVG repeats the identical window clause in the eligibility check; SQL: " + sql);
        sql.Should().NotContain("cast(i", "the window argument must not be widened; SQL: " + sql);
    }

    [Fact]
    public void FilteredAverage_ShouldUseTheEligibilityCheckedAvgIf()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.I, () => x.Id > 0)));

        sql.Should().Contain(
            "if(countIf(isNotNull(i) AND (id > 0))=0, cast(null as Nullable(Float64)), avgIf(i, (id > 0)))",
            "the filtered AVG counts qualifying non-null rows through the -If combinator; SQL: " + sql);
    }

    [Fact]
    public void FloatingAndDecimalAverage_ShouldNotWiden()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var doubleSql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.D)));
        doubleSql.Should().Contain("if(count(d)=0, cast(null as Nullable(Float64)), avg(d))",
            "a double AVG keeps its native argument inside the eligibility check; SQL: " + doubleSql);
        doubleSql.Should().NotContain("cast(d", "a double AVG never receives an integral-widening cast; SQL: " + doubleSql);

        var decimalSql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.M)));
        decimalSql.Should().Contain("if(count(m)=0, cast(null as Nullable(Float64)), avg(m))",
            "a decimal AVG keeps its native argument inside the eligibility check; SQL: " + decimalSql);
        decimalSql.Should().NotContain("cast(m", "a decimal AVG never receives an integral-widening cast; SQL: " + decimalSql);
    }
}

[SqlTable("t291_avg")]
public interface IT291AvgEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("i")]
    int I { get; set; }

    [Column("d")]
    double D { get; set; }

    [Column("m")]
    decimal M { get; set; }
}
