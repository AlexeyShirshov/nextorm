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

    [Fact]
    public void NumericAndLengthFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bl = SqlFunctions.Sql.bit_length(x.String),
            Ol = SqlFunctions.Sql.octet_length(x.String),
            Deg = SqlFunctions.Sql.degrees(1.5),
            Rad = SqlFunctions.Sql.radians(1.5),
            Pi = SqlFunctions.Sql.pi(),
            Acos = Math.Acos(0.5),
            Atan2 = Math.Atan2(1.5, 2.5)
        }));

        sql.Should().Contain("toInt32(length(somestring) * 8)",
            "bit_length converts the complete byte-bit product to the declared Int32");
        sql.Should().Contain("toInt32(length(somestring))",
            "octet_length converts the byte count to the declared Int32");
        sql.Should().Contain("degrees(1.5)");
        sql.Should().Contain("radians(1.5)");
        sql.Should().Contain("pi()");
        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("atan2(1.5, 2.5)");
    }

    [Fact]
    public void LengthFunctions_ShouldCastCompleteExpressionToInt32()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bl = SqlFunctions.Sql.bit_length(x.String),
            Ol = SqlFunctions.Sql.octet_length(x.String)
        }));

        sql.Should().Contain("toInt32(length(somestring) * 8)",
            "the conversion must wrap the complete multiplication, not only the length call");
        sql.Should().Contain("toInt32(length(somestring))",
            "the octet-length byte count is converted as well");
        sql.Should().NotMatchRegex(@"(?<!toInt32\()length\(somestring\)",
            "no bare UInt64-returning length call may reach the typed reader");

        // The public API is unchanged: both functions keep the nullable Int32 declaration.
        typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.bit_length))!
            .ReturnType.Should().Be(typeof(int?));
        typeof(CommonFunctions).GetMethod(nameof(CommonFunctions.octet_length))!
            .ReturnType.Should().Be(typeof(int?));
    }

    [Fact]
    public void LengthFunctions_PreserveSupportAndRenderParity()
    {
        var functions = ClickHouseDialect.Instance.ScalarFunctions;

        functions.Supports("bit_length").Should().BeTrue();
        functions.Supports("octet_length").Should().BeTrue();

        var bitLength = functions.Render("bit_length", ["s"]);
        var octetLength = functions.Render("octet_length", ["s"]);

        bitLength.Should().Be("toInt32(length(s) * 8)");
        octetLength.Should().Be("toInt32(length(s))");
        bitLength.Should().Contain("length(s)",
            "bit_length keeps deriving from the same byte-length expression as octet_length");
    }

    [Fact]
    public void Cot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { C = SqlFunctions.Sql.cot(1.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*cot*not supported*");
    }
}
