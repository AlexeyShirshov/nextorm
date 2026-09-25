using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>SQL generation for the cross-provider scalar functions on PostgreSQL (no database).</summary>
public class CrossProviderScalarSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

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
        sql.Should().Contain("lpad(somestring, 5, '0')");
        sql.Should().Contain("rpad(somestring, 5, '0')");
        sql.Should().Contain("repeat(somestring, 2)");
        sql.Should().Contain("reverse(somestring)");
        sql.Should().Contain("repeat(' ', 3)");
        sql.Should().Contain("concat_ws(',', somestring, somestring)");
        sql.Should().Contain("translate(somestring, 'ab', 'xy')");
        sql.Should().Contain("ascii(somestring)");
        sql.Should().Contain("chr(65)");
    }
}
