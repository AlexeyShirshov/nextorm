using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.SqlServer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Postgres.Tests;

/// <summary>SQL generation for the cross-provider scalar functions on PostgreSQL (no database).</summary>
public class CrossProviderScalarSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [SqlTable("numeric_scale_probe")]
    internal interface INumericScaleProbe
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("amount")]
        decimal? Amount { get; set; }
    }

    [Fact]
    public void ScalarFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.Sql.left(x.String, 3),
            R = SqlFunctions.Sql.right(x.String, 3),
            Lp = SqlFunctions.Sql.lpad(x.String, 5, "0"),
            Rp = SqlFunctions.Sql.rpad(x.String, 5, "0"),
            Rep = SqlFunctions.Sql.repeat(x.String, 2),
            Rev = SqlFunctions.Sql.reverse(x.String),
            Sp = SqlFunctions.Sql.space(3),
            Cw = SqlFunctions.Sql.concat_ws(",", x.String, x.String),
            Tr = SqlFunctions.Sql.translate(x.String, "ab", "xy"),
            Asc = SqlFunctions.Sql.ascii(x.String),
            Ch = SqlFunctions.Sql.@char(65)
        }));

        sql.Should().Contain("left(somestring, 3)");
        sql.Should().Contain("right(somestring, 3)");
        sql.Should().Contain("lpad(somestring, 5, E'0')");
        sql.Should().Contain("rpad(somestring, 5, E'0')");
        sql.Should().Contain("repeat(somestring, 2)");
        sql.Should().Contain("reverse(somestring)");
        sql.Should().Contain("repeat(' ', 3)");
        sql.Should().Contain("concat_ws(E',', somestring, somestring)");
        sql.Should().Contain("translate(somestring, E'ab', E'xy')");
        sql.Should().Contain("ascii(somestring)");
        sql.Should().Contain("chr(65)");
    }

    [Fact]
    public void NumericAndLengthFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bl = SqlFunctions.Sql.bit_length(x.String),
            Ol = SqlFunctions.Sql.octet_length(x.String),
            Cot = SqlFunctions.Sql.cot(1.5),
            Deg = SqlFunctions.Sql.degrees(1.5),
            Rad = SqlFunctions.Sql.radians(1.5),
            Pi = SqlFunctions.Sql.pi(),
            Acos = Math.Acos(0.5),
            Asin = Math.Asin(0.5),
            Atan = Math.Atan(0.5),
            Atan2 = Math.Atan2(1.5, 2.5)
        }));

        sql.Should().Contain("bit_length(somestring)");
        sql.Should().Contain("octet_length(somestring)");
        sql.Should().Contain("cot(1.5)");
        sql.Should().Contain("degrees(1.5)");
        sql.Should().Contain("radians(1.5)");
        sql.Should().Contain("pi()");
        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("asin(0.5)");
        sql.Should().Contain("atan(0.5)");
        sql.Should().Contain("atan2(1.5, 2.5)");
    }

    [Fact]
    public void NumericScaleFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var scale = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.scale(12.3400m) }));
        var minScale = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.min_scale(12.3400m) }));
        var trimScale = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.trim_scale(12.3400m) }));

        scale.Should().Contain("scale(12.3400)");
        minScale.Should().Contain("min_scale(12.3400)");
        trimScale.Should().Contain("trim_scale(12.3400)");
        trimScale.Should().NotContain("round(");
        trimScale.Should().NotContain("cast(");
    }

    [Fact]
    public void NumericScaleFunctions_ShouldSupportParameterAndColumnOperands()
    {
        using var ctx = PostgresTestContext.Create();
        var probe = ctx.From<INumericScaleProbe>();

        var column = SqlOf(ctx, probe.Select(x => new { V = SqlFunctions.Postgres.trim_scale(x.Amount) }));
        var parameter = SqlOf(ctx, probe.Select(x => new { V = SqlFunctions.Postgres.scale(SqlFunctions.Parameter<decimal>(0)) }));

        column.Should().Contain("trim_scale(amount)");
        parameter.Should().Contain("scale(@norm_p0)");
    }

    [Fact]
    public void NumericScaleFunctions_ShouldBeAdvertisedPerName()
    {
        var postgres = PostgresDialect.Instance.ScalarFunctions;
        postgres.Supports("scale").Should().BeTrue();
        postgres.Supports("min_scale").Should().BeTrue();
        postgres.Supports("trim_scale").Should().BeTrue();
        postgres.Supports("numeric_scale").Should().BeFalse();

        var sqlServer = SqlServerDialect.Instance.ScalarFunctions;
        sqlServer.Supports("scale").Should().BeFalse();
        sqlServer.Supports("min_scale").Should().BeFalse();
        sqlServer.Supports("trim_scale").Should().BeFalse();

        var clickHouse = ClickHouseDialect.Instance.ScalarFunctions;
        clickHouse.Supports("scale").Should().BeFalse();
        clickHouse.Supports("min_scale").Should().BeFalse();
        clickHouse.Supports("trim_scale").Should().BeFalse();
    }
}
