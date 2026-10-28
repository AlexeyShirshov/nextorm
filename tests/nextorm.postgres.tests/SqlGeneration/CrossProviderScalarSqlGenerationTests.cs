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

    [Fact]
    [Trait("Issue", "281")]
    public void PostgreSQLSystemInfoFamily_ShouldEmitNativeSpellings()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Cs = SqlFunctions.Postgres.current_schemas(false),
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

        casefold.Should().Throw<NotSupportedException>().WithMessage("The casefold function is refused:*PostgreSQL 18*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("The gamma function is refused:*17.11*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("The tgamma function is refused:*17.11*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("The lgamma function is refused:*17.11*");
        toAsciiOneArg.Should().Throw<NotSupportedException>()
            .WithMessage("The one-argument to_ascii function is refused:*LATIN1, LATIN2, LATIN9 and WIN1250*0A000*to_ascii(value, encoding)*");
        crc32.Should().Throw<NotSupportedException>().WithMessage("The crc32 function is refused:*PostgreSQL 18*");
        crc32c.Should().Throw<NotSupportedException>().WithMessage("The crc32c function is refused:*PostgreSQL 18*");

        // The pg_sleep family returns void, so no void call can appear in a projection; the refusal is
        // asserted at the member itself (its body throws before any SQL could be emitted).
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep(0.0))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_for(TimeSpan.Zero))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_for function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_until(DateTime.UnixEpoch))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_until function is refused:*void*");

        foreach (var name in new[] { "crc32", "crc32c", "pg_sleep", "pg_sleep_for", "pg_sleep_until" })
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
}
