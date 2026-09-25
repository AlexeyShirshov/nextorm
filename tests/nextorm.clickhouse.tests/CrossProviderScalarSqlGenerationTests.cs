using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>SQL generation for the cross-provider scalar functions on ClickHouse (no database).</summary>
public class CrossProviderScalarSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void ScalarFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = ClickHouseTestContext.Create();
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

        sql.Should().Contain("leftUTF8(somestring, 3)");
        sql.Should().Contain("rightUTF8(somestring, 3)");
        sql.Should().Contain("leftPadUTF8(somestring, 5, '0')");
        sql.Should().Contain("rightPadUTF8(somestring, 5, '0')");
        sql.Should().Contain("repeat(somestring, 2)");
        sql.Should().Contain("reverseUTF8(somestring)");
        sql.Should().Contain("space(3)");
        sql.Should().Contain("concatWithSeparator(',', somestring, somestring)");
        sql.Should().Contain("translate(somestring, 'ab', 'xy')");
        sql.Should().Contain("ascii(somestring)");
        sql.Should().Contain("char(65)");
    }
}
