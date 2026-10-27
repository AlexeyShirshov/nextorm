using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the cross-provider scalar functions of <see cref="CommonFunctions"/> (the members that
/// render natively on several providers), the three PostgreSQL numeric-scale members
/// (<c>scale</c>/<c>min_scale</c>/<c>trim_scale</c>) and the PostgreSQL-only date/time, UUID and
/// binary-string scalar families of #281 through the dialect's
/// <see cref="ISqlDialect.ScalarFunctions"/>. Split out of <see cref="BuiltinFunctionTranslator"/> for
/// cohesion; the supported set and emitted SQL are unchanged. Only the enumerated names are enrolled,
/// so a same-named provider-only member that is not listed falls through to
/// <see cref="ExtendedScalarFunctionTranslator"/>.
/// </summary>
internal static class CrossProviderScalarTranslator
{
    private static readonly HashSet<string> ScalarFunctionNames = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.left), nameof(CommonFunctions.right), nameof(CommonFunctions.lpad),
        nameof(CommonFunctions.rpad), nameof(CommonFunctions.repeat), nameof(CommonFunctions.reverse),
        nameof(CommonFunctions.space), nameof(CommonFunctions.concat_ws), nameof(CommonFunctions.translate),
        nameof(CommonFunctions.ascii), nameof(CommonFunctions.@char),
        nameof(CommonFunctions.bit_length), nameof(CommonFunctions.octet_length),
        nameof(CommonFunctions.cot), nameof(CommonFunctions.degrees), nameof(CommonFunctions.radians),
        nameof(CommonFunctions.pi),
        nameof(PostgresFunctions.scale), nameof(PostgresFunctions.min_scale), nameof(PostgresFunctions.trim_scale),
        nameof(PostgresFunctions.btrim), nameof(PostgresFunctions.ltrim), nameof(PostgresFunctions.rtrim),
        nameof(PostgresFunctions.starts_with), nameof(PostgresFunctions.parse_ident),
        nameof(PostgresFunctions.quote_ident), nameof(PostgresFunctions.quote_literal),
        nameof(PostgresFunctions.quote_nullable), nameof(PostgresFunctions.to_ascii),
        nameof(PostgresFunctions.to_hex), nameof(PostgresFunctions.to_oct), nameof(PostgresFunctions.to_bin),
        nameof(PostgresFunctions.convert_from), nameof(PostgresFunctions.convert_to),
        nameof(PostgresFunctions.encode), nameof(PostgresFunctions.decode),
        nameof(PostgresFunctions.normalize), nameof(PostgresFunctions.unistr),
        nameof(PostgresFunctions.regexp_match),
        nameof(PostgresFunctions.div), nameof(PostgresFunctions.erf), nameof(PostgresFunctions.erfc),
        nameof(PostgresFunctions.random_normal),
        nameof(PostgresFunctions.acosd), nameof(PostgresFunctions.asind), nameof(PostgresFunctions.atand),
        nameof(PostgresFunctions.atan2d), nameof(PostgresFunctions.cosd), nameof(PostgresFunctions.cotd),
        nameof(PostgresFunctions.sind), nameof(PostgresFunctions.tand),
        nameof(PostgresFunctions.date_subtract), nameof(PostgresFunctions.justify_interval),
        nameof(PostgresFunctions.make_timestamptz), nameof(PostgresFunctions.clock_timestamp),
        nameof(PostgresFunctions.statement_timestamp), nameof(PostgresFunctions.transaction_timestamp),
        nameof(PostgresFunctions.timeofday), nameof(PostgresFunctions.isfinite),
        nameof(PostgresFunctions.uuid_extract_timestamp), nameof(PostgresFunctions.uuid_extract_version),
        nameof(PostgresFunctions.get_bit), nameof(PostgresFunctions.set_bit),
        nameof(PostgresFunctions.get_byte), nameof(PostgresFunctions.set_byte),
        nameof(PostgresFunctions.bit_count),
        nameof(PostgresFunctions.current_schemas), nameof(PostgresFunctions.pg_postmaster_start_time),
        nameof(PostgresFunctions.pg_current_logfile), nameof(PostgresFunctions.pg_conf_load_time),
        nameof(PostgresFunctions.pg_jit_available), nameof(PostgresFunctions.pg_my_temp_schema),
        nameof(PostgresFunctions.pg_is_other_temp_schema),
        nameof(PostgresFunctions.pg_notification_queue_usage),
        nameof(PostgresFunctions.pg_blocking_pids),
        nameof(PostgresFunctions.pg_safe_snapshot_blocking_pids),
        nameof(PostgresFunctions.pg_trigger_depth),
        nameof(PostgresFunctions.pg_collation_is_visible), nameof(PostgresFunctions.pg_conversion_is_visible),
        nameof(PostgresFunctions.pg_function_is_visible), nameof(PostgresFunctions.pg_opclass_is_visible),
        nameof(PostgresFunctions.pg_operator_is_visible), nameof(PostgresFunctions.pg_opfamily_is_visible),
        nameof(PostgresFunctions.pg_statistics_obj_is_visible), nameof(PostgresFunctions.pg_table_is_visible),
        nameof(PostgresFunctions.pg_ts_config_is_visible), nameof(PostgresFunctions.pg_ts_dict_is_visible),
        nameof(PostgresFunctions.pg_ts_parser_is_visible), nameof(PostgresFunctions.pg_ts_template_is_visible)
    };

    /// <summary>
    /// The Unicode normalization forms accepted by <c>normalize</c>. The form is a grammar keyword in
    /// PostgreSQL (<c>normalize(value, NFD)</c>; <c>normalize(value, 'NFD')</c> is a syntax error), so it
    /// must be a constant and is rendered unquoted.
    /// </summary>
    private static readonly HashSet<string> UnicodeNormalizationForms = new(StringComparer.OrdinalIgnoreCase)
    {
        "NFC", "NFD", "NFKC", "NFKD"
    };

    /// <summary>Translates a cross-provider scalar call; returns <c>false</c> when it is not one.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType is not { } declaring
            || (declaring != typeof(CommonFunctions) && declaring != typeof(PostgresFunctions))
            || !ScalarFunctionNames.Contains(node.Method.Name))
            return false;

        EmitScalarFunction(visitor, node);
        return true;
    }

    /// <summary>
    /// Renders a cross-provider scalar function through the dialect's <see cref="ISqlDialect.ScalarFunctions"/>
    /// renderer, failing with a clear message on a provider that does not support the name.
    /// </summary>
    private static void EmitScalarFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;

        ExtendedScalarFunctionTranslator.ThrowIfRefused(node);

        if (visitor.Dialect.ScalarFunctions is not { } scalars || !scalars.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        var args = name == nameof(CommonFunctions.concat_ws)
            ? ArgumentFlattener.Flatten(node.Arguments, 1)
            : node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        // normalize(value, form): the form is a grammar keyword, so it is rendered unquoted from the
        // constant instead of going through the ordinary argument emission.
        var normalizationForm = name == nameof(PostgresFunctions.normalize) && args.Count == 2
            ? RenderNormalizationForm(args[1])
            : null;

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = i == 1 && normalizationForm is not null
                ? normalizationForm
                : visitor.VisitToString(args[i]);

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>
    /// Validates and renders the <c>normalize</c> form keyword. PostgreSQL's grammar accepts only the
    /// bare tokens <c>NFC</c>/<c>NFD</c>/<c>NFKC</c>/<c>NFKD</c> in this position, so a non-constant
    /// value is refused instead of being emitted as a quoted literal or a parameter.
    /// </summary>
    private static string RenderNormalizationForm(Expression argument)
    {
        if (!SqlLiteral.TryGetConstantString(argument, out var form) || !UnicodeNormalizationForms.Contains(form))
            throw new NotSupportedException(
                "The normalize form must be a constant string among NFC, NFD, NFKC and NFKD: PostgreSQL takes the " +
                "normalization form as a grammar keyword, not as a string value, so it cannot be a parameter or an expression.");
        return form.ToUpperInvariant();
    }
}
