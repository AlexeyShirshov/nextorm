using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation for the cross-provider scalar functions on SQL Server (no database).</summary>
public class CrossProviderScalarSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void ScalarFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
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
        sql.Should().Contain("right(replicate('0', 5) + somestring, 5)");
        sql.Should().Contain("left(somestring + replicate('0', 5), 5)");
        sql.Should().Contain("replicate(somestring, 2)");
        sql.Should().Contain("reverse(somestring)");
        sql.Should().Contain("space(3)");
        sql.Should().Contain("concat_ws(',', somestring, somestring)");
        sql.Should().Contain("translate(somestring, 'ab', 'xy')");
        sql.Should().Contain("ascii(somestring)");
        sql.Should().Contain("char(65)");
    }

    [Fact]
    public void NumericAndLengthFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
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
            Atan2 = Math.Atan2(1.5, 2.5)
        }));

        sql.Should().Contain("datalength(somestring) * 8");
        sql.Should().Contain("datalength(somestring)");
        sql.Should().Contain("cot(1.5)");
        sql.Should().Contain("degrees(cast(1.5 as float))");
        sql.Should().Contain("radians(cast(1.5 as float))");
        sql.Should().Contain("pi()");
        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("atn2(1.5, 2.5)");
    }
}
