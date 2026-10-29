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
            TaCode = SqlFunctions.Postgres.to_ascii(x.String, 8),
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
        sql.Should().Contain("to_ascii(somestring, 8)");
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
    public void PostgreSQLDateTimeFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Ds1 = SqlFunctions.Postgres.date_subtract(x.Datetime, SqlFunctions.Parameter<TimeSpan>(0)),
            Ds3 = SqlFunctions.Postgres.date_subtract(x.Datetime, SqlFunctions.Parameter<TimeSpan>(1), "UTC"),
            Ji = SqlFunctions.Postgres.justify_interval(SqlFunctions.Parameter<TimeSpan>(2)),
            Mt6 = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5),
            Mt7 = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5, "UTC"),
            Ct = SqlFunctions.Postgres.clock_timestamp(),
            St = SqlFunctions.Postgres.statement_timestamp(),
            Tt = SqlFunctions.Postgres.transaction_timestamp(),
            Tod = SqlFunctions.Postgres.timeofday(),
            IsfD = SqlFunctions.Postgres.isfinite(SqlFunctions.Parameter<DateOnly>(3)),
            IsfTs = SqlFunctions.Postgres.isfinite(x.Datetime),
            IsfI = SqlFunctions.Postgres.isfinite(SqlFunctions.Parameter<TimeSpan>(4))
        }));

        foreach (var name in new[]
        {
            "date_subtract", "justify_interval", "make_timestamptz", "clock_timestamp",
            "statement_timestamp", "transaction_timestamp", "timeofday", "isfinite"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("date_subtract(dt, @norm_p0)");
        sql.Should().Contain("date_subtract(dt, @norm_p1, E'UTC')");
        sql.Should().Contain("justify_interval(@norm_p2)");
        sql.Should().Contain("make_timestamptz(2024, 1, 2, 3, 4, 5.5)");
        sql.Should().Contain("make_timestamptz(2024, 1, 2, 3, 4, 5.5, E'UTC')");
        sql.Should().Contain("clock_timestamp()");
        sql.Should().Contain("statement_timestamp()");
        sql.Should().Contain("transaction_timestamp()");
        sql.Should().Contain("timeofday()");
        sql.Should().Contain("isfinite(@norm_p3)");
        sql.Should().Contain("isfinite(dt)");
        sql.Should().Contain("isfinite(@norm_p4)");

        // Positive control: a sibling of the same family on the same query shape still renders.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.btrim(x.String) }))
            .Should().Contain("btrim(somestring)");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLUuidFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Ts = SqlFunctions.Postgres.uuid_extract_timestamp(SqlFunctions.Parameter<Guid>(0)),
            Ver = SqlFunctions.Postgres.uuid_extract_version(SqlFunctions.Parameter<Guid>(1))
        }));

        foreach (var name in new[] { "uuid_extract_timestamp", "uuid_extract_version" })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("uuid_extract_timestamp(@norm_p0)");
        sql.Should().Contain("uuid_extract_version(@norm_p1)");

        // Positive control: the neighbour UUID generator still renders on the same surface.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.gen_random_uuid() }))
            .Should().Contain("gen_random_uuid()");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLBinaryStringFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Gb = SqlFunctions.Postgres.get_bit(SqlFunctions.Parameter<byte[]>(0), 0L),
            Sb = SqlFunctions.Postgres.set_bit(SqlFunctions.Parameter<byte[]>(1), 0L, 1),
            Gbyte = SqlFunctions.Postgres.get_byte(SqlFunctions.Parameter<byte[]>(2), 0),
            Sbyte = SqlFunctions.Postgres.set_byte(SqlFunctions.Parameter<byte[]>(3), 0, 255),
            Bc = SqlFunctions.Postgres.bit_count(SqlFunctions.Parameter<byte[]>(4))
        }));

        foreach (var name in new[] { "get_bit", "set_bit", "get_byte", "set_byte", "bit_count" })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("get_bit(@norm_p0, 0)");
        sql.Should().Contain("set_bit(@norm_p1, 0, 1)");
        sql.Should().Contain("get_byte(@norm_p2, 0)");
        sql.Should().Contain("set_byte(@norm_p3, 0, 255)");
        sql.Should().Contain("bit_count(@norm_p4)");

        // Positive control: the sibling binary encode/decode still renders.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.decode(x.String, "hex") }))
            .Should().Contain("decode(somestring, E'hex')");
    }

    /// <summary>
    /// The PostgreSQL 17 SQL/JSON constructors and the JSON path/extraction helpers of #281 (#347 D3a).
    /// <c>json</c>/<c>json_serialize</c> are parser-level constructs, so their <c>FORMAT JSON</c> /
    /// <c>RETURNING text</c> spelling is asserted literally; the extract-path family passes its array
    /// through the <c>VARIADIC</c> parameter; <c>jsonb_set_lax</c> and the <c>_tz</c> path functions cast
    /// their document/path operands because a text parameter does not resolve to <c>jsonb</c>/<c>jsonpath</c>.
    /// These members are gated as one family by <see cref="ISqlDialect.SupportsPostgresJsonSql"/> (the
    /// JSON surface has no per-name <c>Supports</c> table).
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLJsonConstructorAndPathFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            J = SqlFunctions.Postgres.json(SqlFunctions.Parameter<string>(0)),
            S = SqlFunctions.Postgres.json_scalar(SqlFunctions.Parameter<string>(1)),
            Ser = SqlFunctions.Postgres.json_serialize(SqlFunctions.Parameter<string>(2)),
            Je = SqlFunctions.Postgres.json_extract_path(SqlFunctions.Parameter<string>(3), SqlFunctions.Parameter<string[]>(4)),
            Jet = SqlFunctions.Postgres.json_extract_path_text(SqlFunctions.Parameter<string>(5), SqlFunctions.Parameter<string[]>(6)),
            Jbe = SqlFunctions.Postgres.jsonb_extract_path(SqlFunctions.Parameter<string>(7), SqlFunctions.Parameter<string[]>(8)),
            Jbet = SqlFunctions.Postgres.jsonb_extract_path_text(SqlFunctions.Parameter<string>(9), SqlFunctions.Parameter<string[]>(10)),
            Set = SqlFunctions.Postgres.jsonb_set_lax(
                SqlFunctions.Parameter<string>(11),
                SqlFunctions.Parameter<string[]>(12),
                SqlFunctions.Parameter<string>(13),
                SqlFunctions.Parameter<bool>(14),
                SqlFunctions.Parameter<string>(15)),
            Ex = SqlFunctions.Postgres.jsonb_path_exists_tz(
                SqlFunctions.Parameter<string>(16),
                SqlFunctions.Parameter<string>(17),
                SqlFunctions.Parameter<string>(18),
                SqlFunctions.Parameter<bool>(19)),
            Ma = SqlFunctions.Postgres.jsonb_path_match_tz(
                SqlFunctions.Parameter<string>(20),
                SqlFunctions.Parameter<string>(21),
                SqlFunctions.Parameter<string>(22),
                SqlFunctions.Parameter<bool>(23)),
            Ar = SqlFunctions.Postgres.jsonb_path_query_array_tz(
                SqlFunctions.Parameter<string>(24),
                SqlFunctions.Parameter<string>(25),
                SqlFunctions.Parameter<string>(26),
                SqlFunctions.Parameter<bool>(27)),
            Fi = SqlFunctions.Postgres.jsonb_path_query_first_tz(
                SqlFunctions.Parameter<string>(28),
                SqlFunctions.Parameter<string>(29),
                SqlFunctions.Parameter<string>(30),
                SqlFunctions.Parameter<bool>(31))
        }));

        sql.Should().Contain("json(cast(@norm_p0 as text) format json)");
        sql.Should().Contain("json_scalar(@norm_p1)");
        sql.Should().Contain("json_serialize(cast(@norm_p2 as text) format json returning text)");
        sql.Should().Contain("json_extract_path(cast(@norm_p3 as json), variadic cast(@norm_p4 as text[]))");
        sql.Should().Contain("json_extract_path_text(cast(@norm_p5 as json), variadic cast(@norm_p6 as text[]))");
        sql.Should().Contain("jsonb_extract_path(cast(@norm_p7 as jsonb), variadic cast(@norm_p8 as text[]))");
        sql.Should().Contain("jsonb_extract_path_text(cast(@norm_p9 as jsonb), variadic cast(@norm_p10 as text[]))");
        sql.Should().Contain("jsonb_set_lax(cast(@norm_p11 as jsonb), cast(@norm_p12 as text[]), cast(@norm_p13 as jsonb), @norm_p14, @norm_p15)");
        sql.Should().Contain("jsonb_path_exists_tz(cast(@norm_p16 as jsonb), cast(@norm_p17 as jsonpath), cast(@norm_p18 as jsonb), @norm_p19)");
        sql.Should().Contain("jsonb_path_match_tz(cast(@norm_p20 as jsonb), cast(@norm_p21 as jsonpath), cast(@norm_p22 as jsonb), @norm_p23)");
        sql.Should().Contain("jsonb_path_query_array_tz(cast(@norm_p24 as jsonb), cast(@norm_p25 as jsonpath), cast(@norm_p26 as jsonb), @norm_p27)");
        sql.Should().Contain("jsonb_path_query_first_tz(cast(@norm_p28 as jsonb), cast(@norm_p29 as jsonpath), cast(@norm_p30 as jsonb), @norm_p31)");

        PostgresDialect.Instance.SupportsPostgresJsonSql.Should().BeTrue(
            "the SQL/JSON constructor and path surface is the PostgreSQL JSON gate");

        // Positive control: the neighbouring JSON accessor still renders on the same shape.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.jsonb_path_query_first(x.String, "$.a") }))
            .Should().Contain("jsonb_path_query_first(somestring, cast(E'$.a' as jsonpath))");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Cs = SqlFunctions.Postgres.current_schemas(false),
            Cr = SqlFunctions.Postgres.current_role(),
            Cd = SqlFunctions.Postgres.current_catalog(),
            Pst = SqlFunctions.Postgres.pg_postmaster_start_time(),
            Cl = SqlFunctions.Postgres.pg_current_logfile(),
            Clf = SqlFunctions.Postgres.pg_current_logfile("stderr"),
            Cfg = SqlFunctions.Postgres.pg_conf_load_time(),
            Jit = SqlFunctions.Postgres.pg_jit_available(),
            Mts = SqlFunctions.Postgres.pg_my_temp_schema(),
            Iots = SqlFunctions.Postgres.pg_is_other_temp_schema("0"),
            Nqu = SqlFunctions.Postgres.pg_notification_queue_usage(),
            Bp = SqlFunctions.Postgres.pg_blocking_pids(x.Int),
            Sbp = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(x.Int),
            Td = SqlFunctions.Postgres.pg_trigger_depth()
        }));

        foreach (var name in new[]
        {
            "current_schemas", "pg_postmaster_start_time", "pg_current_logfile", "pg_conf_load_time",
            "pg_jit_available", "pg_my_temp_schema", "pg_is_other_temp_schema",
            "pg_notification_queue_usage", "pg_blocking_pids", "pg_safe_snapshot_blocking_pids",
            "pg_trigger_depth"
        })
        {
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("current_schemas(false)");
        // `current_role` and `current_catalog` are SQL keywords, so they render without parentheses.
        sql.Should().Contain("current_role");
        sql.Should().Contain("current_catalog");
        sql.Should().NotContain("current_role()");
        sql.Should().NotContain("current_catalog()");
        sql.Should().Contain("pg_postmaster_start_time()");
        sql.Should().Contain("pg_current_logfile()");
        sql.Should().Contain("pg_current_logfile(E'stderr')");
        sql.Should().Contain("pg_conf_load_time()");
        sql.Should().Contain("pg_jit_available()");
        // oid result: cast to text, as pg_typeof does, because the driver cannot read oid as a string.
        sql.Should().Contain("cast(pg_my_temp_schema() as text)");
        // oid argument: cast so a text parameter resolves to the oid overload.
        sql.Should().Contain("pg_is_other_temp_schema(cast(E'0' as oid))");
        sql.Should().Contain("pg_notification_queue_usage()");
        sql.Should().Contain("pg_blocking_pids(nullableint)");
        sql.Should().Contain("pg_safe_snapshot_blocking_pids(nullableint)");
        sql.Should().Contain("pg_trigger_depth()");

        // Positive control: the neighbouring system-information member still renders on the same shape.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_typeof(x.Int) }))
            .Should().Contain("cast(pg_typeof(nullableint) as text)");
    }

    /// <summary>
    /// The catalog-visibility helpers of #281 take a catalog OID in its text form; the renderer casts
    /// the argument, because a <c>text</c> parameter does not resolve to the <c>oid</c> overload.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLCatalogVisibilityFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C0 = SqlFunctions.Postgres.pg_collation_is_visible(SqlFunctions.Parameter<string>(0)),
            C1 = SqlFunctions.Postgres.pg_conversion_is_visible(SqlFunctions.Parameter<string>(0)),
            C2 = SqlFunctions.Postgres.pg_function_is_visible(SqlFunctions.Parameter<string>(0)),
            C3 = SqlFunctions.Postgres.pg_opclass_is_visible(SqlFunctions.Parameter<string>(0)),
            C4 = SqlFunctions.Postgres.pg_operator_is_visible(SqlFunctions.Parameter<string>(0)),
            C5 = SqlFunctions.Postgres.pg_opfamily_is_visible(SqlFunctions.Parameter<string>(0)),
            C6 = SqlFunctions.Postgres.pg_statistics_obj_is_visible(SqlFunctions.Parameter<string>(0)),
            C7 = SqlFunctions.Postgres.pg_table_is_visible(SqlFunctions.Parameter<string>(0)),
            C8 = SqlFunctions.Postgres.pg_ts_config_is_visible(SqlFunctions.Parameter<string>(0)),
            C9 = SqlFunctions.Postgres.pg_ts_dict_is_visible(SqlFunctions.Parameter<string>(0)),
            C10 = SqlFunctions.Postgres.pg_ts_parser_is_visible(SqlFunctions.Parameter<string>(0)),
            C11 = SqlFunctions.Postgres.pg_ts_template_is_visible(SqlFunctions.Parameter<string>(0)),
        }));

        foreach (var name in new[]
        {
            "pg_collation_is_visible", "pg_conversion_is_visible", "pg_function_is_visible",
            "pg_opclass_is_visible", "pg_operator_is_visible", "pg_opfamily_is_visible",
            "pg_statistics_obj_is_visible", "pg_table_is_visible", "pg_ts_config_is_visible",
            "pg_ts_dict_is_visible", "pg_ts_parser_is_visible", "pg_ts_template_is_visible"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("pg_collation_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_conversion_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_function_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_opclass_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_operator_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_opfamily_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_statistics_obj_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_table_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_ts_config_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_ts_dict_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_ts_parser_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_ts_template_is_visible(cast(@norm_p0 as oid))");

        // Positive control: the neighbouring session-information member still renders on the same shape.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_trigger_depth() }))
            .Should().Contain("pg_trigger_depth()");
    }

    /// <summary>
    /// The function-definition helpers of #281. The argument is an <c>oid</c>; a text parameter does not
    /// resolve to the <c>oid</c> overload, so it is cast.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLFunctionDefinitionFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Fa = SqlFunctions.Postgres.pg_get_function_arguments(SqlFunctions.Parameter<string>(0)),
            Fia = SqlFunctions.Postgres.pg_get_function_identity_arguments(SqlFunctions.Parameter<string>(1)),
            Fr = SqlFunctions.Postgres.pg_get_function_result(SqlFunctions.Parameter<string>(2)),
            Fd = SqlFunctions.Postgres.pg_get_functiondef(SqlFunctions.Parameter<string>(3))
        }));

        foreach (var name in new[]
        {
            "pg_get_function_arguments", "pg_get_function_identity_arguments",
            "pg_get_function_result", "pg_get_functiondef"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("pg_get_function_arguments(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_get_function_identity_arguments(cast(@norm_p1 as oid))");
        sql.Should().Contain("pg_get_function_result(cast(@norm_p2 as oid))");
        sql.Should().Contain("pg_get_functiondef(cast(@norm_p3 as oid))");

        // The pg_node_tree pair carries no SQL at all: the members refuse before any command is built.
        PostgresDialect.Instance.ScalarFunctions.Supports("pg_get_expr").Should().BeFalse();
        PostgresDialect.Instance.ScalarFunctions.Supports("pg_get_constraintdef").Should().BeFalse();
    }

    /// <summary>
    /// The remaining catalog-definition helpers of #281 (index, partition, rule, statistics, trigger).
    /// Both the one-argument and the boolean-column form are asserted for the three multi-arity names.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLCatalogDefinitionFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Id1 = SqlFunctions.Postgres.pg_get_indexdef(SqlFunctions.Parameter<string>(0)),
            Id3 = SqlFunctions.Postgres.pg_get_indexdef(
                SqlFunctions.Parameter<string>(1), 2, true),
            Pcd = SqlFunctions.Postgres.pg_get_partition_constraintdef(SqlFunctions.Parameter<string>(2)),
            Pkd = SqlFunctions.Postgres.pg_get_partkeydef(SqlFunctions.Parameter<string>(3)),
            Rd1 = SqlFunctions.Postgres.pg_get_ruledef(SqlFunctions.Parameter<string>(4)),
            Rd2 = SqlFunctions.Postgres.pg_get_ruledef(SqlFunctions.Parameter<string>(5), true),
            Sodb = SqlFunctions.Postgres.pg_get_statisticsobjdef(SqlFunctions.Parameter<string>(6)),
            Td1 = SqlFunctions.Postgres.pg_get_triggerdef(SqlFunctions.Parameter<string>(7)),
            Td2 = SqlFunctions.Postgres.pg_get_triggerdef(SqlFunctions.Parameter<string>(8), true)
        }));

        foreach (var name in new[]
        {
            "pg_get_indexdef", "pg_get_partition_constraintdef", "pg_get_partkeydef",
            "pg_get_ruledef", "pg_get_statisticsobjdef", "pg_get_triggerdef"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("pg_get_indexdef(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_get_indexdef(cast(@norm_p1 as oid), 2, true)");
        sql.Should().Contain("pg_get_partition_constraintdef(cast(@norm_p2 as oid))");
        sql.Should().Contain("pg_get_partkeydef(cast(@norm_p3 as oid))");
        sql.Should().Contain("pg_get_ruledef(cast(@norm_p4 as oid))");
        sql.Should().Contain("pg_get_ruledef(cast(@norm_p5 as oid), true)");
        sql.Should().Contain("pg_get_statisticsobjdef(cast(@norm_p6 as oid))");
        sql.Should().Contain("pg_get_triggerdef(cast(@norm_p7 as oid))");
        sql.Should().Contain("pg_get_triggerdef(cast(@norm_p8 as oid), true)");
    }

    /// <summary>
    /// The view/role/snapshot helpers of #281. <c>to_regclass</c> accepts both a view name and a numeric
    /// OID (the text-to-<c>regclass</c> cast resolves a name only), <c>regrole</c> accepts both, and the
    /// snapshot/xid8 results are read as text.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLViewRoleAndSnapshotFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Ub = SqlFunctions.Postgres.pg_get_userbyid(SqlFunctions.Parameter<string>(0)),
            Vd1 = SqlFunctions.Postgres.pg_get_viewdef(SqlFunctions.Parameter<string>(1)),
            Vd2 = SqlFunctions.Postgres.pg_get_viewdef(SqlFunctions.Parameter<string>(2), true),
            Vd3 = SqlFunctions.Postgres.pg_get_viewdef(SqlFunctions.Parameter<string>(3), 2),
            Hr2 = SqlFunctions.Postgres.pg_has_role(SqlFunctions.Parameter<string>(4), "USAGE"),
            Hr3 = SqlFunctions.Postgres.pg_has_role(
                SqlFunctions.Parameter<string>(5), SqlFunctions.Parameter<string>(6), "USAGE"),
            Cs = SqlFunctions.Postgres.pg_current_snapshot(),
            Cx = SqlFunctions.Postgres.pg_current_xact_id(),
            Cxa = SqlFunctions.Postgres.pg_current_xact_id_if_assigned()
        }));

        foreach (var name in new[]
        {
            "pg_get_userbyid", "pg_get_viewdef", "pg_has_role",
            "pg_current_snapshot", "pg_current_xact_id", "pg_current_xact_id_if_assigned"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("pg_get_userbyid(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_get_viewdef(to_regclass(@norm_p1))");
        sql.Should().Contain("pg_get_viewdef(to_regclass(@norm_p2), true)");
        sql.Should().Contain("pg_get_viewdef(to_regclass(@norm_p3), 2)");
        sql.Should().Contain("pg_has_role(cast(@norm_p4 as regrole), E'USAGE')");
        sql.Should().Contain("pg_has_role(cast(@norm_p5 as regrole), cast(@norm_p6 as regrole), E'USAGE')");
        sql.Should().Contain("cast(pg_current_snapshot() as text)");
        sql.Should().Contain("cast(pg_current_xact_id() as text)");
        sql.Should().Contain("cast(pg_current_xact_id_if_assigned() as text)");
    }

    /// <summary>
    /// The snapshot-inspection and transaction-status helpers of #281: <c>pg_snapshot</c> and <c>xid8</c>
    /// arguments are cast from the textual form the producing members return.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSnapshotInspectionFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Sxmax = SqlFunctions.Postgres.pg_snapshot_xmax(SqlFunctions.Parameter<string>(0)),
            Sxmin = SqlFunctions.Postgres.pg_snapshot_xmin(SqlFunctions.Parameter<string>(1)),
            Vis = SqlFunctions.Postgres.pg_visible_in_snapshot(
                SqlFunctions.Parameter<string>(2), SqlFunctions.Parameter<string>(3)),
            Xct = SqlFunctions.Postgres.pg_xact_commit_timestamp("7"),
            Xs = SqlFunctions.Postgres.pg_xact_status(SqlFunctions.Parameter<string>(4))
        }));

        foreach (var name in new[]
        {
            "pg_snapshot_xmax", "pg_snapshot_xmin", "pg_visible_in_snapshot",
            "pg_xact_commit_timestamp", "pg_xact_status"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("cast(pg_snapshot_xmax(cast(@norm_p0 as pg_snapshot)) as text)");
        sql.Should().Contain("cast(pg_snapshot_xmin(cast(@norm_p1 as pg_snapshot)) as text)");
        sql.Should().Contain("pg_visible_in_snapshot(cast(@norm_p2 as xid8), cast(@norm_p3 as pg_snapshot))");
        sql.Should().Contain("pg_xact_commit_timestamp(cast(E'7' as xid))");
        sql.Should().Contain("pg_xact_status(cast(@norm_p4 as xid8))");
    }

    /// <summary>
    /// The encoding, catalog-format and index-property helpers of #281. Three render shapes are
    /// asserted: a plain call, an <c>oid</c>/<c>regtype</c> argument cast (a text parameter does not
    /// resolve to those overloads) and the <c>regtype</c> result cast to text.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLEncodingAndCatalogFormatFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Tiv = SqlFunctions.Postgres.pg_type_is_visible(SqlFunctions.Parameter<string>(0)),
            Cte = SqlFunctions.Postgres.pg_char_to_encoding("UTF8"),
            Etc = SqlFunctions.Postgres.pg_encoding_to_char(6),
            Bt = SqlFunctions.Postgres.pg_basetype(SqlFunctions.Parameter<string>(1)),
            Do = SqlFunctions.Postgres.pg_describe_object(
                SqlFunctions.Parameter<string>(2), SqlFunctions.Parameter<string>(3), 0),
            Tl = SqlFunctions.Postgres.pg_tablespace_location(SqlFunctions.Parameter<string>(4)),
            Sgf = SqlFunctions.Postgres.pg_settings_get_flags("work_mem"),
            Ihp = SqlFunctions.Postgres.pg_index_has_property("pg_class_oid_index", "clusterable"),
            Iahp = SqlFunctions.Postgres.pg_indexam_has_property(SqlFunctions.Parameter<string>(5), "can_order"),
            Ichp = SqlFunctions.Postgres.pg_index_column_has_property("pg_class_oid_index", 1, "orderable"),
            Iiv = SqlFunctions.Postgres.pg_input_is_valid("123", "integer"),
            Gss = SqlFunctions.Postgres.pg_get_serial_sequence("pg_class", "oid")
        }));

        foreach (var name in new[]
        {
            "pg_type_is_visible", "pg_char_to_encoding", "pg_encoding_to_char",
            "pg_basetype", "pg_describe_object", "pg_tablespace_location",
            "pg_settings_get_flags", "pg_index_has_property", "pg_indexam_has_property",
            "pg_index_column_has_property", "pg_input_is_valid", "pg_get_serial_sequence"
        })
        {
            sql.Should().Contain($"{name}(");
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        sql.Should().Contain("pg_type_is_visible(cast(@norm_p0 as oid))");
        sql.Should().Contain("pg_char_to_encoding(E'UTF8')");
        sql.Should().Contain("pg_encoding_to_char(6)");
        sql.Should().Contain("cast(pg_basetype(cast(@norm_p1 as regtype)) as text)");
        sql.Should().Contain("pg_describe_object(cast(@norm_p2 as oid), cast(@norm_p3 as oid), 0)");
        sql.Should().Contain("pg_tablespace_location(cast(@norm_p4 as oid))");
        sql.Should().Contain("pg_settings_get_flags(E'work_mem')");
        sql.Should().Contain("pg_index_has_property(E'pg_class_oid_index', E'clusterable')");
        sql.Should().Contain("pg_indexam_has_property(cast(@norm_p5 as oid), E'can_order')");
        sql.Should().Contain("pg_index_column_has_property(E'pg_class_oid_index', 1, E'orderable')");
        sql.Should().Contain("pg_input_is_valid(E'123', E'integer')");
        sql.Should().Contain("pg_get_serial_sequence(E'pg_class', E'oid')");

        // Positive control: the neighbouring visibility helper still renders on the same shape.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_table_is_visible("1259") }))
            .Should().Contain("pg_table_is_visible(cast(E'1259' as oid))");
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
        var crc32 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.crc32(SqlFunctions.Parameter<byte[]>(8)) }));
        var crc32c = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.crc32c(SqlFunctions.Parameter<byte[]>(9)) }));
        var getExpr2 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_expr("x", "0") }));
        var getExpr3 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_expr("x", "0", true) }));
        var getConDef1 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_constraintdef("0") }));
        var getConDef2 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_constraintdef("0", true) }));

        casefold.Should().Throw<NotSupportedException>().WithMessage("The casefold function is refused:*PostgreSQL 18*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("The gamma function is refused:*17.11*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("The tgamma function is refused:*17.11*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("The lgamma function is refused:*17.11*");
        toAsciiOneArg.Should().Throw<NotSupportedException>()
            .WithMessage("The one-argument to_ascii function is refused:*LATIN1, LATIN2, LATIN9 and WIN1250*0A000*to_ascii(value, encoding)*");
        crc32.Should().Throw<NotSupportedException>().WithMessage("The crc32 function is refused:*PostgreSQL 18*");
        crc32c.Should().Throw<NotSupportedException>().WithMessage("The crc32c function is refused:*PostgreSQL 18*");

        // The pg_get_expr / pg_get_constraintdef pair must refuse with its own reason *inside* a query
        // expression too: the member body never runs there, so the refusal has to be reachable from the
        // translator (ExtendedScalarFunctionTranslator.Refused), not only from a direct C# call.
        getExpr2.Should().Throw<NotSupportedException>().WithMessage("The pg_get_expr function is refused:*pg_node_tree*");
        getExpr3.Should().Throw<NotSupportedException>().WithMessage("The pg_get_expr function is refused:*pg_node_tree*");
        getConDef1.Should().Throw<NotSupportedException>()
            .WithMessage("The pg_get_constraintdef function is refused:*pg_node_tree*");
        getConDef2.Should().Throw<NotSupportedException>()
            .WithMessage("The pg_get_constraintdef function is refused:*pg_node_tree*");

        // The pg_sleep family returns void, so no void call can appear in a projection; the refusal is
        // asserted at the member itself (its body throws before any SQL could be emitted).
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep(0.0))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_for(TimeSpan.Zero))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_for function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_until(DateTime.UnixEpoch))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_until function is refused:*void*");

        foreach (var name in new[]
                 {
                     "crc32", "crc32c", "pg_sleep", "pg_sleep_for", "pg_sleep_until",
                     "pg_get_expr", "pg_get_constraintdef"
                 })
            PostgresDialect.Instance.ScalarFunctions.Supports(name).Should().BeFalse($"'{name}' is refused on every provider");

        // Positive control: a sibling of the same family on the same query shape still renders, so the
        // refusals above are per name/shape and not a blanket failure of the translator.
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.btrim(x.String) }))
            .Should().Contain("btrim(somestring)");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1") }))
            .Should().Contain("to_ascii(somestring, E'LATIN1')");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erf(0.5) }))
            .Should().Contain("erf(0.5)");
    }

    /// <summary>
    /// The control-file and keyword readers of #281 as FROM sources. Three render shapes are asserted:
    /// a bare call whose columns all map (<c>pg_control_system</c>, <c>pg_control_init</c>), a
    /// projection that casts only the unreadable column types (<c>pg_lsn</c>, <c>oid</c>, the internal
    /// <c>"char"</c>) and the column names the row interfaces map. The live shape is asserted in
    /// <c>PostgresFunctionsTests.SystemInfoControlAndKeywordReaders_ShouldExposeRowsAndClrTypes</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoControlAndKeywordReaders_ShouldEmitCallAndProjection()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var name in new[]
        {
            "pg_control_system", "pg_control_init", "pg_control_recovery", "pg_control_checkpoint",
            "pg_get_keywords"
        })
        {
            PostgresDialect.Instance.SupportsTableFunction(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        var system = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_system())
            .Select(r => new { r.PgControlVersion, r.CatalogVersionNo, r.PgControlLastModified }));
        // Every column of the record maps, so the call is emitted unchanged (no projection wrapper).
        system.Should().Contain("from pg_control_system() as \"t1\"");
        system.Should().Contain("select pg_control_version as \"PgControlVersion\", catalog_version_no as \"CatalogVersionNo\"");

        var init = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_init())
            .Select(r => new { r.MaxDataAlignment, r.Float8PassByValue, r.DataPageChecksumVersion }));
        init.Should().Contain("from pg_control_init() as \"t1\"");
        init.Should().Contain("select max_data_alignment as \"MaxDataAlignment\", float8_pass_by_value as \"Float8PassByValue\"");

        var recovery = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_recovery())
            .Select(r => new { r.MinRecoveryEndLsn, r.BackupEndLsn, r.EndOfBackupRecordRequired }));
        recovery.Should().Contain(
            "from (select min_recovery_end_lsn::text as min_recovery_end_lsn, min_recovery_end_timeline,"
            + " backup_start_lsn::text as backup_start_lsn, backup_end_lsn::text as backup_end_lsn,"
            + " end_of_backup_record_required\n from pg_control_recovery()) as \"t1\"");
        recovery.Should().Contain("select min_recovery_end_lsn as \"MinRecoveryEndLsn\"");

        var checkpoint = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_checkpoint())
            .Select(r => new { r.CheckpointLsn, r.NextOid, r.OldestXid, r.CheckpointTime }));
        checkpoint.Should().Contain("from (select checkpoint_lsn::text as checkpoint_lsn");
        checkpoint.Should().Contain("next_oid::text as next_oid");
        checkpoint.Should().Contain("oldest_xid_dbid::text as oldest_xid_dbid");
        checkpoint.Should().Contain("oldest_multi_dbid::text as oldest_multi_dbid");
        checkpoint.Should().Contain("newest_commit_ts_xid, checkpoint_time\n from pg_control_checkpoint()) as \"t1\"");
        checkpoint.Should().Contain("select checkpoint_lsn as \"CheckpointLsn\", next_oid as \"NextOid\", oldest_xid as \"OldestXid\"");

        var keywords = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_keywords())
            .Select(r => new { r.Word, r.CatCode, r.BareLabel }));
        keywords.Should().Contain(
            "from (select word, catcode::text as catcode, barelabel, catdesc, baredesc\n from pg_get_keywords()) as \"t1\"");
        keywords.Should().Contain("select word, catcode, barelabel");
    }

    /// <summary>
    /// The single-column and catalog-inspection readers of #281 as FROM sources. The single-column
    /// functions restore the function-named column and cast the <c>oid</c>/<c>xid8</c> value to text;
    /// the two record functions keep their documented columns, with the <c>regclass</c> columns of
    /// <c>pg_get_catalog_foreign_keys</c> cast to text and its <c>text[]</c> columns left intact.
    /// The live shape is asserted in
    /// <c>PostgresFunctionsTests.SystemInfoSingleColumnAndCatalogReaders_ShouldExposeRowsAndClrTypes</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoSingleColumnAndCatalogReaders_ShouldEmitCallAndProjection()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var name in new[]
        {
            "pg_listening_channels", "pg_snapshot_xip", "pg_tablespace_databases",
            "pg_get_catalog_foreign_keys", "pg_options_to_table"
        })
        {
            PostgresDialect.Instance.SupportsTableFunction(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        var channels = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_listening_channels())
            .Select(r => new { r.Channel }));
        channels.Should().Contain("from (select pg_listening_channels\n from pg_listening_channels()) as \"t1\"");
        channels.Should().Contain("select pg_listening_channels as \"Channel\"");

        var xip = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_snapshot_xip("10:20:10,12"))
            .Select(r => new { r.Xid }));
        xip.Should().Contain(
            "from (select pg_snapshot_xip::text as pg_snapshot_xip\n from pg_snapshot_xip(cast(E'10:20:10,12' as pg_snapshot))) as \"t1\"");
        xip.Should().Contain("select pg_snapshot_xip as \"Xid\"");

        var databases = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_tablespace_databases("1663"))
            .Select(r => new { r.DatabaseOid }));
        databases.Should().Contain(
            "from (select pg_tablespace_databases::text as pg_tablespace_databases\n from pg_tablespace_databases(cast(E'1663' as oid))) as \"t1\"");
        databases.Should().Contain("select pg_tablespace_databases as \"DatabaseOid\"");

        var foreignKeys = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_catalog_foreign_keys())
            .Select(r => new { r.FkTable, r.FkColumns, r.PkTable, r.PkColumns, r.IsArray, r.IsOptional }));
        foreignKeys.Should().Contain(
            "from (select fktable::text as fktable, fkcols, pktable::text as pktable, pkcols, is_array, is_opt"
            + "\n from pg_get_catalog_foreign_keys()) as \"t1\"");
        foreignKeys.Should().Contain("select fktable, fkcols as \"FkColumns\", pktable, pkcols as \"PkColumns\"");

        var options = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_options_to_table(SqlFunctions.Parameter<string[]>(0)))
            .Select(r => new { r.OptionName, r.OptionValue }));
        // Both columns are text, so the call needs no projection wrapper; the text[] argument is a
        // single array parameter (a constant array would be spread into separate arguments).
        options.Should().Contain("from pg_options_to_table(@norm_p0) as \"t1\"");
        options.Should().Contain("select option_name as \"OptionName\", option_value as \"OptionValue\"");
    }
    /// <summary>
    /// The WAL-summary, multixact, object-address and transaction-status readers of #281 as FROM
    /// sources: the <c>pg_lsn</c>/<c>oid</c> projections, the <c>xid</c> argument cast that makes the
    /// text/unsigned parameter resolve, and the bare-call shape of the two functions whose columns all
    /// map. The live shape is asserted in
    /// <c>PostgresFunctionsTests.SystemInfoWalAndXactReaders_ShouldExposeRowsAndClrTypes</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoWalAndXactReaders_ShouldEmitCallAndProjection()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var name in new[]
        {
            "pg_available_wal_summaries", "pg_get_multixact_members", "pg_get_object_address",
            "pg_get_wal_summarizer_state", "pg_last_committed_xact"
        })
        {
            PostgresDialect.Instance.SupportsTableFunction(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        var summaries = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_available_wal_summaries())
            .Select(r => new { r.TimelineId, r.StartLsn, r.EndLsn }));
        summaries.Should().Contain(
            "from (select tli, start_lsn::text as start_lsn, end_lsn::text as end_lsn"
            + "\n from pg_available_wal_summaries()) as \"t1\"");
        summaries.Should().Contain("select tli as \"TimelineId\", start_lsn as \"StartLsn\", end_lsn as \"EndLsn\"");

        var members = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_multixact_members(SqlFunctions.Parameter<uint>(0)))
            .Select(r => new { r.Xid, r.Mode }));
        members.Should().Contain(
            "from (select * from pg_get_multixact_members(cast(cast(@norm_p0 as text) as xid))) as \"t1\"");
        // The projection stays unaliased where the column name already matches the member name
        // case-insensitively (the same convention as pg_get_keywords' "select word, catcode, barelabel").
        members.Should().Contain("select xid, mode");

        var address = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_object_address(
                SqlFunctions.Parameter<string>(0),
                SqlFunctions.Parameter<string[]>(1),
                SqlFunctions.Parameter<string[]>(2)))
            .Select(r => new { r.ClassId, r.ObjId, r.ObjSubId }));
        address.Should().Contain(
            "from (select classid::text as classid, objid::text as objid, objsubid"
            + "\n from pg_get_object_address(@norm_p0, cast(@norm_p1 as text[]), cast(@norm_p2 as text[]))) as \"t1\"");
        address.Should().Contain("select classid, objid, objsubid");

        var summarizer = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_wal_summarizer_state())
            .Select(r => new { r.SummarizedTli, r.SummarizedLsn, r.PendingLsn, r.SummarizerPid }));
        summarizer.Should().Contain(
            "from (select summarized_tli, summarized_lsn::text as summarized_lsn,"
            + " pending_lsn::text as pending_lsn, summarizer_pid"
            + "\n from pg_get_wal_summarizer_state()) as \"t1\"");
        summarizer.Should().Contain(
            "select summarized_tli as \"SummarizedTli\", summarized_lsn as \"SummarizedLsn\","
            + " pending_lsn as \"PendingLsn\", summarizer_pid as \"SummarizerPid\"");

        var committed = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_last_committed_xact())
            .Select(r => new { r.Xid, r.Timestamp, r.RoIdent }));
        committed.Should().Contain(
            "from (select xid, timestamp, roident::text as roident"
            + "\n from pg_last_committed_xact()) as \"t1\"");
        committed.Should().Contain("select xid, timestamp, roident\n");
    }

    /// <summary>
    /// The object-identity and input-error readers of #281 as FROM sources: both OID arguments travel as
    /// <c>oid</c> (a text parameter does not resolve to the <c>oid</c> overloads), the identity columns are
    /// all text and the error-info reader keeps its bare call because every column and argument is already
    /// mappable. The live shapes are asserted in
    /// <c>PostgresFunctionsTests.SystemInfoObjectIdentityAndInputReaders_ShouldExposeRowsAndClrTypes</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoObjectIdentityAndInputReaders_ShouldEmitCallAndProjection()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var name in new[]
        {
            "pg_identify_object", "pg_identify_object_as_address", "pg_input_error_info"
        })
        {
            PostgresDialect.Instance.SupportsTableFunction(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        var identity = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_identify_object(
                SqlFunctions.Parameter<string>(0),
                SqlFunctions.Parameter<string>(1),
                SqlFunctions.Parameter<int>(2)))
            .Select(r => new { r.Type, r.Schema, r.Name, r.Identity }));
        identity.Should().Contain(
            "from (select * from pg_identify_object(cast(@norm_p0 as oid), cast(@norm_p1 as oid), @norm_p2)) as \"t1\"");
        // The projection stays unaliased where the column name already matches the member name
        // case-insensitively (the same convention as pg_get_keywords' "select word, catcode, barelabel").
        identity.Should().Contain("select type, schema, name, identity");

        var address = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_identify_object_as_address(
                SqlFunctions.Parameter<string>(0),
                SqlFunctions.Parameter<string>(1),
                SqlFunctions.Parameter<int>(2)))
            .Select(r => new { r.Type, r.ObjectNames, r.ObjectArgs }));
        address.Should().Contain(
            "from (select * from pg_identify_object_as_address(cast(@norm_p0 as oid), cast(@norm_p1 as oid), @norm_p2)) as \"t1\"");
        address.Should().Contain("select type, object_names as \"ObjectNames\", object_args as \"ObjectArgs\"");

        var info = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_input_error_info(
                SqlFunctions.Parameter<string>(0),
                SqlFunctions.Parameter<string>(1)))
            .Select(r => new { r.Message, r.Detail, r.Hint, r.SqlErrorCode }));
        info.Should().Contain("from pg_input_error_info(@norm_p0, @norm_p1) as \"t1\"");
        info.Should().Contain("select message, detail, hint, sql_error_code as \"SqlErrorCode\"");
    }

    /// <summary>
    /// The WAL-summary and transaction-origin readers of #281 as FROM sources: the summary's three
    /// <c>oid</c> columns are projected as text (the row mapper has no <c>oid</c> accessor) while the
    /// <c>int2</c>/<c>int8</c>/<c>bool</c> columns stay untouched, the <c>pg_lsn</c> bounds and the
    /// <c>xid</c> argument travel in their native types, and the origin's <c>roident</c> is projected as
    /// text. The live shapes are asserted in
    /// <c>PostgresFunctionsTests.SystemInfoWalSummaryAndXactOriginReaders_ShouldExposeRowsAndClrTypes</c>.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoWalSummaryAndXactOriginReaders_ShouldEmitCallAndProjection()
    {
        using var ctx = PostgresTestContext.Create();

        foreach (var name in new[] { "pg_wal_summary_contents", "pg_xact_commit_timestamp_origin" })
        {
            PostgresDialect.Instance.SupportsTableFunction(name).Should().BeTrue($"'{name}' is delivered on PostgreSQL");
        }

        var summary = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_wal_summary_contents(
                SqlFunctions.Parameter<long>(0),
                SqlFunctions.Parameter<string>(1),
                SqlFunctions.Parameter<string>(2)))
            .Select(r => new { r.RelFileNode, r.RelTablespace, r.RelDatabase, r.RelForkNumber, r.RelBlockNumber, r.IsLimitBlock }));
        summary.Should().Contain(
            "from (select relfilenode::text as relfilenode, reltablespace::text as reltablespace,"
            + " reldatabase::text as reldatabase, relforknumber, relblocknumber, is_limit_block"
            + "\n from pg_wal_summary_contents(@norm_p0, cast(@norm_p1 as pg_lsn), cast(@norm_p2 as pg_lsn))) as \"t1\"");
        summary.Should().Contain(
            "select relfilenode, reltablespace, reldatabase, relforknumber, relblocknumber, is_limit_block as \"IsLimitBlock\"");

        var origin = SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_xact_commit_timestamp_origin(SqlFunctions.Parameter<uint>(0)))
            .Select(r => new { r.Timestamp, r.RoIdent }));
        origin.Should().Contain(
            "from (select timestamp, roident::text as roident"
            + "\n from pg_xact_commit_timestamp_origin(cast(cast(@norm_p0 as text) as xid))) as \"t1\"");
        origin.Should().Contain("select timestamp, roident\n");
    }
}
