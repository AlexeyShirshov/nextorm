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

    /// <summary>
    /// The frozen D1 baseline for the already-covered numeric-scale names
    /// (<c>.pdca/T347/rv1/03-baseline/b01-numeric-scale.sql</c>). Their SQL must stay byte-identical.
    /// </summary>
    private const string NumericScaleBaseline =
        "select scale(amount) as \"S\", min_scale(amount) as \"MS\", trim_scale(amount) as \"TS\"\n"
        + " from complex_entity";

    /// <summary>The table/column shape the baseline was captured over (the D1 probe read <c>complex_entity</c>).</summary>
    [SqlTable("complex_entity")]
    internal interface INumericScaleBaselineProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("amount")]
        decimal? Amount { get; set; }
    }

    [Fact]
    [Trait("Issue", "281")]
    public void AlreadyCoveredNumericScaleFunctions_ShouldKeepTheBaselineSql()
    {
        using var ctx = PostgresTestContext.Create();
        var probe = ctx.From<INumericScaleBaselineProbe>();

        var sql = SqlOf(ctx, probe.Select(x => new
        {
            S = SqlFunctions.Postgres.scale(x.Amount),
            MS = SqlFunctions.Postgres.min_scale(x.Amount),
            TS = SqlFunctions.Postgres.trim_scale(x.Amount)
        }));

        sql.Should().Be(NumericScaleBaseline);
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLStringFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Bt1 = SqlFunctions.Postgres.btrim(x.String),
            Bt2 = SqlFunctions.Postgres.btrim(x.String, "xy"),
            BtBytes = SqlFunctions.Postgres.btrim(SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1)),
            Lt2 = SqlFunctions.Postgres.ltrim(x.String, "xy"),
            LtBytes = SqlFunctions.Postgres.ltrim(SqlFunctions.Parameter<byte[]>(2), SqlFunctions.Parameter<byte[]>(3)),
            Rt2 = SqlFunctions.Postgres.rtrim(x.String, "xy"),
            RtBytes = SqlFunctions.Postgres.rtrim(SqlFunctions.Parameter<byte[]>(4), SqlFunctions.Parameter<byte[]>(5)),
            Sw = SqlFunctions.Postgres.starts_with(x.String, "dad"),
            Pid = SqlFunctions.Postgres.parse_ident("a.b", false),
            Qi = SqlFunctions.Postgres.quote_ident("a b"),
            Ql = SqlFunctions.Postgres.quote_literal(x.String),
            Qn = SqlFunctions.Postgres.quote_nullable(x.String),
            Ta3 = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1"),
            ThInt = SqlFunctions.Postgres.to_hex(x.Int),
            ThLong = SqlFunctions.Postgres.to_hex(255L),
            ToInt = SqlFunctions.Postgres.to_oct(x.Int),
            ToLong = SqlFunctions.Postgres.to_oct(255L),
            TbInt = SqlFunctions.Postgres.to_bin(x.Int),
            TbLong = SqlFunctions.Postgres.to_bin(255L),
            Cf = SqlFunctions.Postgres.convert_from(SqlFunctions.Parameter<byte[]>(6), "UTF8"),
            Ct = SqlFunctions.Postgres.convert_to(x.String, "UTF8"),
            En = SqlFunctions.Postgres.encode(SqlFunctions.Parameter<byte[]>(7), "base64"),
            De = SqlFunctions.Postgres.decode(x.String, "hex"),
            No = SqlFunctions.Postgres.normalize(x.String, "NFD"),
            Un = SqlFunctions.Postgres.unistr("a\\0441"),
            Rm = SqlFunctions.Postgres.regexp_match(x.String, "(d)(a)"),
            Rmf = SqlFunctions.Postgres.regexp_match(x.String, "(d)(a)", "i")
        }));

        foreach (var name in new[]
        {
            "btrim", "ltrim", "rtrim", "starts_with", "parse_ident", "quote_ident", "quote_literal",
            "quote_nullable", "to_ascii", "to_hex", "to_oct", "to_bin", "convert_from", "convert_to",
            "encode", "decode", "normalize", "unistr", "regexp_match"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("btrim(somestring)");
        sql.Should().Contain("btrim(somestring, E'xy')");
        sql.Should().Contain("btrim(@norm_p0, @norm_p1)");
        sql.Should().Contain("ltrim(somestring, E'xy')");
        sql.Should().Contain("rtrim(somestring, E'xy')");
        sql.Should().Contain("starts_with(somestring, E'dad')");
        sql.Should().Contain("parse_ident(E'a.b', false)");
        sql.Should().Contain("quote_ident(E'a b')");
        sql.Should().Contain("to_ascii(somestring, E'LATIN1')");
        sql.Should().Contain("to_hex(nullableint)");
        sql.Should().Contain("to_hex(255)");
        sql.Should().Contain("to_oct(nullableint)");
        sql.Should().Contain("to_bin(nullableint)");
        sql.Should().Contain("convert_from(@norm_p6, E'UTF8')");
        sql.Should().Contain("convert_to(somestring, E'UTF8')");
        sql.Should().Contain("encode(@norm_p7, E'base64')");
        sql.Should().Contain("decode(somestring, E'hex')");
        sql.Should().Contain("normalize(somestring, NFD)");
        sql.Should().Contain("regexp_match(somestring, E'(d)(a)')");
        sql.Should().Contain("regexp_match(somestring, E'(d)(a)', E'i')");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLNumericFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Dv = SqlFunctions.Postgres.div(7m, 2m),
            Ef = SqlFunctions.Postgres.erf(0.5),
            Efc = SqlFunctions.Postgres.erfc(0.5),
            Rn = SqlFunctions.Postgres.random_normal(0.0, 1.0),
            Acosd = SqlFunctions.Postgres.acosd(0.5),
            Asind = SqlFunctions.Postgres.asind(0.5),
            Atand = SqlFunctions.Postgres.atand(0.5),
            Atan2d = SqlFunctions.Postgres.atan2d(1.5, 2.5),
            Cosd = SqlFunctions.Postgres.cosd(60.0),
            Cotd = SqlFunctions.Postgres.cotd(45.0),
            Sind = SqlFunctions.Postgres.sind(30.0),
            Tand = SqlFunctions.Postgres.tand(45.0)
        }));

        foreach (var name in new[]
        {
            "div", "erf", "erfc", "random_normal", "acosd", "asind", "atand", "atan2d",
            "cosd", "cotd", "sind", "tand"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("div(7, 2)");
        sql.Should().Contain("erf(0.5)");
        sql.Should().Contain("erfc(0.5)");
        sql.Should().Contain("random_normal(0, 1)");
        sql.Should().Contain("acosd(0.5)");
        sql.Should().Contain("asind(0.5)");
        sql.Should().Contain("atand(0.5)");
        sql.Should().Contain("atan2d(1.5, 2.5)");
        sql.Should().Contain("cosd(60)");
        sql.Should().Contain("cotd(45)");
        sql.Should().Contain("sind(30)");
        sql.Should().Contain("tand(45)");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void RefusedFunctions_ShouldFailBeforeAnySqlIsEmitted()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var casefold = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.casefold(x.String) }));
        var gamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.gamma(1.5) }));
        var tgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.tgamma(1.5) }));
        var lgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.lgamma(1.5) }));
        var toAsciiOneArg = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String) }));

        casefold.Should().Throw<NotSupportedException>().WithMessage("The casefold function is refused:*PostgreSQL 18*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("The gamma function is refused:*17.11*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("The tgamma function is refused:*17.11*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("The lgamma function is refused:*17.11*");
        toAsciiOneArg.Should().Throw<NotSupportedException>()
            .WithMessage("The one-argument to_ascii function is refused:*0A000*LATIN1/LATIN2/WIN1250*to_ascii(value, encoding)*");

        // Positive control: a sibling of the same family on the same query shape still renders, so the
        // refusals above are per name/shape and not a blanket failure of the translator.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.btrim(x.String) }))
            .Should().Contain("btrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1") }))
            .Should().Contain("to_ascii(somestring, E'LATIN1')");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erf(0.5) }))
            .Should().Contain("erf(0.5)");
    }
}
