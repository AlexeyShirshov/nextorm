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

        // The pg_get_expr / pg_get_constraintdef pair is refused at the member itself: PostgreSQL needs
        // a pg_node_tree value there and no CLR value of that type exists.
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_get_expr("x", "0"))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_get_expr function is refused:*pg_node_tree*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_get_expr("x", "0", true))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_get_expr function is refused:*pg_node_tree*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_get_constraintdef("0"))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_get_constraintdef function is refused:*pg_node_tree*");
        FluentActions.Invoking(() => SqlFunctions.Postgres.pg_get_constraintdef("0", true))
            .Should().Throw<NotSupportedException>().WithMessage("The pg_get_constraintdef function is refused:*pg_node_tree*");

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
            "pg_safe_snapshot_blocking_pids", "pg_trigger_depth",
            "pg_collation_is_visible",
            "pg_conversion_is_visible",
            "pg_function_is_visible",
            "pg_opclass_is_visible",
            "pg_operator_is_visible",
            "pg_opfamily_is_visible",
            "pg_statistics_obj_is_visible",
            "pg_table_is_visible",
            "pg_ts_config_is_visible",
            "pg_ts_dict_is_visible",
            "pg_ts_parser_is_visible",
            "pg_ts_template_is_visible",
            "pg_type_is_visible",
            "pg_char_to_encoding",
            "pg_encoding_to_char",
            "pg_basetype",
            "pg_describe_object",
            "pg_tablespace_location",
            "pg_settings_get_flags",
            "pg_index_has_property",
            "pg_indexam_has_property",
            "pg_index_column_has_property",
            "pg_input_is_valid",
            "pg_get_serial_sequence"
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

    /// <summary>
    /// The PostgreSQL catalog-visibility helpers of #281 are PostgreSQL-only on this provider as well:
    /// each enrolled name is refused during preparation with a message naming it, never rendered as
    /// PostgreSQL SQL.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void CatalogVisibilityFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_collation_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_collation_is_visible("0") }))),
            ("pg_conversion_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_conversion_is_visible("0") }))),
            ("pg_function_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_function_is_visible("0") }))),
            ("pg_opclass_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_opclass_is_visible("0") }))),
            ("pg_operator_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_operator_is_visible("0") }))),
            ("pg_opfamily_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_opfamily_is_visible("0") }))),
            ("pg_statistics_obj_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_statistics_obj_is_visible("0") }))),
            ("pg_table_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_table_is_visible("0") }))),
            ("pg_ts_config_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_ts_config_is_visible("0") }))),
            ("pg_ts_dict_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_ts_dict_is_visible("0") }))),
            ("pg_ts_parser_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_ts_parser_is_visible("0") }))),
            ("pg_ts_template_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_ts_template_is_visible("0") }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>
    /// The PostgreSQL encoding, catalog-format and index-property helpers of #281 are PostgreSQL-only on
    /// this provider as well: each enrolled name is refused during preparation with a message naming it.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void EncodingAndCatalogFormatFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_type_is_visible", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_type_is_visible("0") }))),
            ("pg_char_to_encoding", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_char_to_encoding("UTF8") }))),
            ("pg_encoding_to_char", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_encoding_to_char(6) }))),
            ("pg_basetype", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_basetype("integer") }))),
            ("pg_describe_object", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_describe_object("0", "0", 0) }))),
            ("pg_tablespace_location", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_tablespace_location("0") }))),
            ("pg_settings_get_flags", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_settings_get_flags("work_mem") }))),
            ("pg_index_has_property", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_index_has_property("pg_class_oid_index", "clusterable") }))),
            ("pg_indexam_has_property", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_indexam_has_property("0", "can_order") }))),
            ("pg_index_column_has_property", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_index_column_has_property("pg_class_oid_index", 1, "orderable") }))),
            ("pg_input_is_valid", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_input_is_valid("123", "integer") }))),
            ("pg_get_serial_sequence", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_serial_sequence("pg_class", "oid") }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
    /// <summary>
    /// The PostgreSQL function-definition helpers of #281 are PostgreSQL-only on this provider as well:
    /// each enrolled name is refused during preparation with a message naming it.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void FunctionDefinitionFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_get_function_arguments", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_function_arguments("1") }))),
            ("pg_get_function_identity_arguments", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_function_identity_arguments("1") }))),
            ("pg_get_function_result", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_function_result("1") }))),
            ("pg_get_functiondef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_functiondef("1") }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>
    /// The PostgreSQL catalog-definition helpers of #281 (index, partition, rule, statistics, trigger)
    /// are PostgreSQL-only on this provider as well.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void CatalogDefinitionFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_get_indexdef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_indexdef("1") }))),
            ("pg_get_partition_constraintdef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_partition_constraintdef("1") }))),
            ("pg_get_partkeydef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_partkeydef("1") }))),
            ("pg_get_ruledef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_ruledef("1") }))),
            ("pg_get_statisticsobjdef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_statisticsobjdef("1") }))),
            ("pg_get_triggerdef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_triggerdef("1") }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>
    /// The PostgreSQL view, role and snapshot helpers of #281 are PostgreSQL-only on this provider too.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void ViewRoleAndSnapshotFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_get_userbyid", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_userbyid("1") }))),
            ("pg_get_viewdef", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_get_viewdef("v") }))),
            ("pg_has_role", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_has_role("r", "USAGE") }))),
            ("pg_current_snapshot", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_current_snapshot() }))),
            ("pg_current_xact_id", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_current_xact_id() }))),
            ("pg_current_xact_id_if_assigned", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_current_xact_id_if_assigned() }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>
    /// The PostgreSQL snapshot-inspection and transaction-status helpers of #281 are PostgreSQL-only on
    /// this provider too.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void SnapshotInspectionFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_snapshot_xmax", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_snapshot_xmax("1:1:") }))),
            ("pg_snapshot_xmin", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_snapshot_xmin("1:1:") }))),
            ("pg_visible_in_snapshot", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_visible_in_snapshot("1", "1:1:") }))),
            ("pg_xact_commit_timestamp", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_xact_commit_timestamp("7") }))),
            ("pg_xact_status", () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.pg_xact_status("1") }))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }

    /// <summary>
    /// The PostgreSQL system-information table-valued functions of #281 (#347 D2c, block E) are
    /// PostgreSQL-only query sources: this provider refuses each name during preparation with a message
    /// naming it, instead of rendering PostgreSQL SQL.
    /// </summary>
    [Fact]
    [Trait("Issue", "281")]
    public void SystemInfoTvfControlAndKeywordFamily_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();

        var cases = new (string Name, Action Act)[]
        {
            ("pg_control_system", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_system()).Select(r => r.PgControlVersion))),
            ("pg_control_init", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_init()).Select(r => r.MaxDataAlignment))),
            ("pg_control_recovery", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_recovery()).Select(r => r.MinRecoveryEndLsn))),
            ("pg_control_checkpoint", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_control_checkpoint()).Select(r => r.CheckpointLsn))),
            ("pg_get_keywords", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_keywords()).Select(r => r.Word))),
            ("pg_listening_channels", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_listening_channels()).Select(r => r.Channel))),
            ("pg_snapshot_xip", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_snapshot_xip("10:20:")).Select(r => r.Xid))),
            ("pg_tablespace_databases", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_tablespace_databases("1663")).Select(r => r.DatabaseOid))),
            ("pg_get_catalog_foreign_keys", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_catalog_foreign_keys()).Select(r => r.FkTable))),
            ("pg_options_to_table", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_options_to_table(new[] { "fillfactor=70" })).Select(r => r.OptionName))),
            ("pg_available_wal_summaries", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_available_wal_summaries()).Select(r => r.TimelineId))),
            ("pg_get_multixact_members", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_multixact_members(SqlFunctions.Parameter<uint>(0))).Select(r => r.Xid))),
            ("pg_get_object_address", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_object_address(SqlFunctions.Parameter<string>(1), SqlFunctions.Parameter<string[]>(2), SqlFunctions.Parameter<string[]>(3))).Select(r => r.ClassId))),
            ("pg_get_wal_summarizer_state", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_get_wal_summarizer_state()).Select(r => r.SummarizedTli))),
            ("pg_last_committed_xact", () => SqlOf(ctx, ctx.From(() => SqlFunctions.Postgres.pg_last_committed_xact()).Select(r => r.Xid))),
        };

        foreach (var (name, act) in cases)
            act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
}
