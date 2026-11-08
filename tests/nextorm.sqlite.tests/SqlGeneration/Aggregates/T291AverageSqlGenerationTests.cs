using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite.Tests;

namespace nextorm.sqlite.tests.SqlGeneration;

/// <summary>
/// T291 (#291) SQL generation on SQLite: an integral AVG aggregates the native column, DISTINCT is
/// placed before that argument and a window AVG applies the same policy before <c>OVER</c>. Floating
/// and decimal selectors never receive an integral-widening cast. Only SQL is prepared; no database
/// is opened.
/// </summary>
public class T291AverageSqlGenerationTests
{
    private static IDataContext CreateContext()
    {
        var ctx = SqliteTestContext.Create();
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

        sql.Should().Contain("avg(i)", "an integral AVG must aggregate the native column; SQL: " + sql);
        sql.Should().NotContain("cast(", "the native argument must not be widened; SQL: " + sql);
    }

    [Fact]
    public void DistinctAverage_ShouldPlaceDistinctBeforeTheArgument()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)));

        sql.Should().Contain("avg(distinct i)", "distinct must precede the native argument; SQL: " + sql);
        sql.Should().NotContain("cast(", "the native argument must not be widened; SQL: " + sql);
    }

    [Fact]
    public void WindowAverage_ShouldApplyTheSameArgumentPolicyBeforeOver()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Sql.avg_over(x.I).Over() }));

        sql.Should().Contain("avg(i)", "the window AVG must use the same native argument; SQL: " + sql);
        sql.Should().NotContain("cast(", "the window argument must not be widened; SQL: " + sql);
        sql.IndexOf("avg(i)", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("over", StringComparison.Ordinal),
                "the argument policy is applied before OVER; SQL: " + sql);
    }

    [Fact]
    public void FloatingAndDecimalAverage_ShouldNotWiden()
    {
        using var ctx = CreateContext();
        var e = ctx.From<IT291AvgEntity>();

        var doubleSql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.D)));
        doubleSql.Should().Contain("avg(d)", "a double AVG keeps its native argument; SQL: " + doubleSql);
        doubleSql.Should().NotContain("cast(", "a double AVG never receives an integral-widening cast; SQL: " + doubleSql);

        var decimalSql = SqlOf(ctx, e.Select(x => SqlFunctions.Sql.avg(x.M)));
        decimalSql.Should().Contain("avg(m)", "a decimal AVG keeps its native argument; SQL: " + decimalSql);
        decimalSql.Should().NotContain("cast(", "a decimal AVG never receives an integral-widening cast; SQL: " + decimalSql);
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
