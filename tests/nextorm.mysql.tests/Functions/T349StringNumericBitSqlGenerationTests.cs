using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for the T349 MySQL 8.4 string, numeric, bit and bit-aggregate catalogue (no database):
/// every name renders its native spelling, the constant grammar of <c>weight_string</c> and the
/// pre-SQL validation of <c>make_set</c>/<c>export_set</c> are exact, and the whole catalogue is
/// reported supported by the MySQL capability.
/// </summary>
public class T349StringNumericBitSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.bin(5UL)))
            .Should().Contain("bin(5)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.export_set(5UL, "Y", "N")))
            .Should().Contain("export_set(5, _utf8mb4 X'59', _utf8mb4 X'4e')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.export_set(5UL, "Y", "N", "!")))
            .Should().Contain("export_set(5, _utf8mb4 X'59', _utf8mb4 X'4e', _utf8mb4 X'21')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.export_set(5UL, "Y", "N", ",", 8)))
            .Should().Contain("export_set(5, _utf8mb4 X'59', _utf8mb4 X'4e', _utf8mb4 X'2c', 8)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.hex(x.String)))
            .Should().Contain("hex(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.hex(255UL)))
            .Should().Contain("hex(255)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.hex(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("hex(@norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.make_set(5UL, "a", "b")))
            .Should().Contain("make_set(5, _utf8mb4 X'61', _utf8mb4 X'62')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.oct(8UL)))
            .Should().Contain("oct(8)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.ord(x.String)))
            .Should().Contain("ord(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.ord((string?)null)))
            .Should().Contain("ord(null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.quote(x.String)))
            .Should().Contain("quote(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.soundex("Robert")))
            .Should().Contain("soundex(_utf8mb4 X'526f62657274')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.strcmp(x.String, "a")))
            .Should().Contain("strcmp(somestring, _utf8mb4 X'61')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.strcmp(null, "a")))
            .Should().Contain("strcmp(null, _utf8mb4 X'61')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_base64(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("to_base64(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.from_base64("YWJj")))
            .Should().Contain("from_base64(_utf8mb4 X'59574a6a')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.unhex("616263")))
            .Should().Contain("unhex(_utf8mb4 X'363136323633')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.unhex((string?)null)))
            .Should().Contain("unhex(null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.weight_string(x.String)))
            .Should().Contain("weight_string(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.weight_string(
                x.String, MySqlWeightStringCast.Char, 3)))
            .Should().Contain("weight_string(somestring as CHAR(3))");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.weight_string(
                x.String, MySqlWeightStringCast.Binary, 4)))
            .Should().Contain("weight_string(somestring as BINARY(4))");
    }

    [Fact]
    public void NumericFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.conv("10", 10, 2)))
            .Should().Contain("conv(_utf8mb4 X'3130', 10, 2)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.crc32(x.String)))
            .Should().Contain("crc32(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.crc32(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("crc32(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.log2(8.5)))
            .Should().Contain("log2(8.5)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.rand()))
            .Should().Contain("rand()");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.rand(42L)))
            .Should().Contain("rand(42)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.truncate(12.345m, 2)))
            .Should().Contain("truncate(12.345, 2)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.truncate(12.345d, 1)))
            .Should().Contain("truncate(12.345, 1)");
    }

    [Fact]
    public void BitFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.bit_count(255UL)))
            .Should().Contain("bit_count(255)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.bit_count(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("bit_count(@norm_p0)");
    }

    [Fact]
    public void BitAggregateFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_and(10UL) }))
            .Should().Contain("bit_and(10)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_or(10UL) }))
            .Should().Contain("bit_or(10)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_xor(10UL) }))
            .Should().Contain("bit_xor(10)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_and(SqlFunctions.Parameter<byte[]>(0)) }))
            .Should().Contain("bit_and(@norm_p0)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_or(SqlFunctions.Parameter<byte[]>(0)) }))
            .Should().Contain("bit_or(@norm_p0)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.bit_xor(SqlFunctions.Parameter<byte[]>(0)) }))
            .Should().Contain("bit_xor(@norm_p0)");
    }

    [Fact]
    public void MakeSetExportSetAndWeightString_InvalidArguments_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var makeSet = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.make_set(5UL)));
        makeSet.Should().Throw<NotSupportedException>().WithMessage("*make_set*at least one value*");

        var tooMany = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.export_set(5UL, "Y", "N", ",", 65)));
        tooMany.Should().Throw<NotSupportedException>()
            .WithMessage("*export_set*bit count between 0 and 64*got 65*");

        var negative = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.export_set(5UL, "Y", "N", ",", -1)));
        negative.Should().Throw<NotSupportedException>()
            .WithMessage("*export_set*bit count between 0 and 64*");

        var zeroLength = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.weight_string(x.String, MySqlWeightStringCast.Char, 0)));
        zeroLength.Should().Throw<NotSupportedException>()
            .WithMessage("*weight_string*length of at least 1*");
    }

    [Fact]
    public void T349Functions_ShouldAllBeReportedSupported()
    {
        var capability = MySqlDialect.Instance.MySqlFunctions!;
        string[] names =
        [
            "bin", "export_set", "hex", "make_set", "oct", "ord", "quote", "soundex", "strcmp",
            "to_base64", "from_base64", "unhex", "weight_string",
            "conv", "crc32", "log2", "rand", "truncate",
            "bit_count", "bit_and", "bit_or", "bit_xor"
        ];

        names.Should().HaveCount(22);
        foreach (var name in names)
            capability.Supports(name).Should().BeTrue($"'{name}' is a MySQL 8.4 gate key");
    }
}
