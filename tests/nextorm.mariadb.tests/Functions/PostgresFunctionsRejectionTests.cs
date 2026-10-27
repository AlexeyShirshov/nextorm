using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// The PostgreSQL numeric-scale functions of #281 (<see cref="SqlFunctions.Postgres"/>) have no
/// MariaDB equivalent, so the MariaDB dialect must reject each name instead of rendering PostgreSQL
/// SQL. MariaDB inherits the MySQL dialect; this is the MariaDB dialect path, never a MySQL run.
/// </summary>
public class PostgresFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var scale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.scale(12.3400m) }));
        var minScale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.min_scale(12.3400m) }));
        var trimScale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.trim_scale(12.3400m) }));

        scale.Should().Throw<NotSupportedException>().WithMessage("*scale*not supported*");
        minScale.Should().Throw<NotSupportedException>().WithMessage("*min_scale*not supported*");
        trimScale.Should().Throw<NotSupportedException>().WithMessage("*trim_scale*not supported*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldNotBeAdvertised()
    {
        var scalars = MariaDbDialect.Instance.ScalarFunctions;

        scalars.Supports("scale").Should().BeFalse();
        scalars.Supports("min_scale").Should().BeFalse();
        scalars.Supports("trim_scale").Should().BeFalse();
    }

    /// <summary>
    /// The residual string family of #281 is PostgreSQL-only: every name must be refused during
    /// preparation with a message naming it, never rendered as PostgreSQL SQL against MariaDB.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void StringFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("btrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.btrim(x.String) }))),
            ("ltrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.ltrim(x.String, "xy") }))),
            ("rtrim", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.rtrim(x.String, "xy") }))),
            ("starts_with", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.starts_with(x.String, "d") }))),
            ("parse_ident", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.parse_ident("a.b", false) }))),
            ("quote_ident", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_ident("a b") }))),
            ("quote_literal", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_literal(x.String) }))),
            ("quote_nullable", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.quote_nullable(x.String) }))),
            ("to_ascii", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, "LATIN1") }))),
            ("to_ascii", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String, 8) }))),
            ("to_hex", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_hex(x.Int) }))),
            ("to_oct", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_oct(x.Int) }))),
            ("to_bin", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_bin(x.Int) }))),
            ("convert_from", () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.Postgres.convert_from(SqlFunctions.Postgres.convert_to(x.String, "UTF8"), "UTF8")
            }))),
            ("convert_to", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.convert_to(x.String, "UTF8") }))),
            ("encode", () => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.Postgres.encode(SqlFunctions.Postgres.convert_to(x.String, "UTF8"), "hex")
            }))),
            ("decode", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.decode(x.String, "hex") }))),
            ("normalize", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.normalize(x.String, "NFC") }))),
            ("unistr", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.unistr(x.String) }))),
            ("regexp_match", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.regexp_match(x.String, "a") })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>The residual numeric family of #281 is PostgreSQL-only as well.</summary>
    [Fact]
    [Trait("Issue", "281")]
    public void NumericFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("div", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.div(7m, 2m) }))),
            ("erf", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erf(0.5) }))),
            ("erfc", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.erfc(0.5) }))),
            ("random_normal", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.random_normal(0.0, 1.0) }))),
            ("acosd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.acosd(0.5) }))),
            ("asind", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.asind(0.5) }))),
            ("atand", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.atand(0.5) }))),
            ("atan2d", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.atan2d(1.0, 1.0) }))),
            ("cosd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.cosd(60.0) }))),
            ("cotd", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.cotd(45.0) }))),
            ("sind", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.sind(30.0) }))),
            ("tand", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.tand(45.0) })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>The refused members keep their technical reason on MariaDB too.</summary>
    [Fact]
    [Trait("Issue", "281")]
    public void RefusedMembers_ShouldCarryTheirReason()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var casefold = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.casefold(x.String) }));
        var gamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.gamma(1.5) }));
        var tgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.tgamma(1.5) }));
        var lgamma = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.lgamma(1.5) }));
        var toAsciiOneArg = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.to_ascii(x.String) }));
        var crc32 = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.crc32(SqlFunctions.Parameter<byte[]>(8)) }));
        var crc32c = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.crc32c(SqlFunctions.Parameter<byte[]>(9)) }));

        casefold.Should().Throw<NotSupportedException>().WithMessage("*casefold*refused*");
        gamma.Should().Throw<NotSupportedException>().WithMessage("*gamma*refused*");
        tgamma.Should().Throw<NotSupportedException>().WithMessage("*tgamma*refused*");
        lgamma.Should().Throw<NotSupportedException>().WithMessage("*lgamma*refused*");
        toAsciiOneArg.Should().Throw<NotSupportedException>().WithMessage("*to_ascii*refused*0A000*");
        crc32.Should().Throw<NotSupportedException>().WithMessage("The crc32 function is refused:*PostgreSQL 18*");
        crc32c.Should().Throw<NotSupportedException>().WithMessage("The crc32c function is refused:*PostgreSQL 18*");

        // The pg_sleep family returns void, so no void call can appear in a projection; the refusal is a
        // member-level throw (its body throws before any SQL could be emitted) and is provider-independent.
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep(0.0))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_for(TimeSpan.Zero))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_for function is refused:*void*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_sleep_until(DateTime.UnixEpoch))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_sleep_until function is refused:*void*");
    }

    /// <summary>
    /// The residual PostgreSQL date/time, UUID and binary-string families of #281 are PostgreSQL-only
    /// on this provider as well: each enrolled name must be refused during preparation with a message
    /// naming it, never rendered as PostgreSQL SQL.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void DateTimeUuidAndBinaryFamilies_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("date_subtract", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.date_subtract(x.Datetime, TimeSpan.FromDays(1)) }))),
            ("date_subtract", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.date_subtract(x.Datetime, TimeSpan.FromDays(1), "UTC") }))),
            ("justify_interval", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.justify_interval(TimeSpan.FromHours(25)) }))),
            ("make_timestamptz", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5) }))),
            ("make_timestamptz", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.make_timestamptz(2024, 1, 2, 3, 4, 5.5, "UTC") }))),
            ("clock_timestamp", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.clock_timestamp() }))),
            ("statement_timestamp", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.statement_timestamp() }))),
            ("transaction_timestamp", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.transaction_timestamp() }))),
            ("timeofday", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.timeofday() }))),
            ("isfinite", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.isfinite(x.Datetime) }))),
            ("isfinite", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.isfinite(TimeSpan.FromDays(1)) }))),
            ("uuid_extract_timestamp", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.uuid_extract_timestamp(Guid.Empty) }))),
            ("uuid_extract_version", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.uuid_extract_version(Guid.Empty) }))),
            ("get_bit", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.get_bit(SqlFunctions.Parameter<byte[]>(0), 0L) }))),
            ("set_bit", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.set_bit(SqlFunctions.Parameter<byte[]>(1), 0L, 1) }))),
            ("get_byte", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.get_byte(SqlFunctions.Parameter<byte[]>(2), 0) }))),
            ("set_byte", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.set_byte(SqlFunctions.Parameter<byte[]>(3), 0, 255) }))),
            ("bit_count", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.bit_count(SqlFunctions.Parameter<byte[]>(4)) })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void ResidualFamilies_ShouldNotBeAdvertised()
    {
        var scalars = MariaDbDialect.Instance.ScalarFunctions;

        foreach (var name in new[]
        {
            "btrim", "ltrim", "rtrim", "starts_with", "parse_ident", "quote_ident", "quote_literal",
            "quote_nullable", "to_ascii", "to_hex", "to_oct", "to_bin", "convert_from", "convert_to",
            "encode", "decode", "normalize", "unistr", "regexp_match",
            "div", "random_normal", "acosd", "asind", "atand", "atan2d", "cosd", "cotd", "sind", "tand",
            "date_subtract", "justify_interval", "make_timestamptz", "clock_timestamp", "statement_timestamp",
            "transaction_timestamp", "timeofday", "isfinite", "uuid_extract_timestamp",
            "uuid_extract_version", "get_bit", "set_bit", "get_byte", "set_byte", "bit_count",
            "current_schemas", "pg_postmaster_start_time", "pg_current_logfile",
            "pg_conf_load_time", "pg_jit_available", "pg_my_temp_schema",
            "pg_is_other_temp_schema", "pg_notification_queue_usage", "pg_blocking_pids",
            "pg_safe_snapshot_blocking_pids", "pg_trigger_depth"
        })
        {
            scalars.Supports(name).Should().BeFalse($"'{name}' is PostgreSQL-only");
        }
    }
    /// <summary>
    /// The PostgreSQL system-information members of #281 (session identity, log and temp-schema
    /// helpers, the blocking-pid arrays and the trigger depth) are PostgreSQL-only: each enrolled
    /// name is refused during preparation with a message naming it.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void SystemInfoFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("current_schemas", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.current_schemas(true) }))),
            ("pg_postmaster_start_time", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_postmaster_start_time() }))),
            ("pg_current_logfile", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_current_logfile("stderr") }))),
            ("pg_conf_load_time", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_conf_load_time() }))),
            ("pg_jit_available", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_jit_available() }))),
            ("pg_my_temp_schema", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_my_temp_schema() }))),
            ("pg_is_other_temp_schema", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_is_other_temp_schema("0") }))),
            ("pg_notification_queue_usage", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_notification_queue_usage() }))),
            ("pg_blocking_pids", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_blocking_pids(x.Int) }))),
            ("pg_safe_snapshot_blocking_pids", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(x.Int) }))),
            ("pg_trigger_depth", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_trigger_depth() })))
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
}
