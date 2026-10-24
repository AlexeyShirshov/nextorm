using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the string-JSON surface of <see cref="ClickHouseFunctions"/> (<c>json_extract_string</c>,
/// <c>json_extract_int</c>, <c>json_extract_float</c>, <c>json_extract_bool</c>, <c>json_extract_raw</c>,
/// <c>json_extract_keys</c>, <c>json_extract_array_raw</c>, <c>json_extract_keys_and_values</c>,
/// <c>json_has</c>, <c>json_length</c>, <c>json_type</c>, the flat-JSON fast path
/// <c>visit_param_extract_string</c>/<c>_int</c>/<c>_float</c>/<c>_bool</c>/<c>_raw</c>, the JSONPath
/// scalars <c>json_value</c>/<c>json_query</c>/<c>json_exists</c>, the native-JSON
/// <c>json_all_paths</c>/<c>json_all_paths_with_types</c>/<c>to_json_string</c> functions, and the F09
/// additions <c>json_merge_patch</c>, <c>json_array_length</c>,
/// <c>json_dynamic_paths</c>/<c>json_dynamic_paths_with_types</c>,
/// <c>json_shared_data_paths</c>/<c>json_shared_data_paths_with_types</c>, <c>json_key</c>,
/// <c>json_extract_keys_and_values_raw</c>, <c>json_extract_uint</c>, <c>is_valid_json</c> and the
/// <c>simple_json_extract_*</c> family), where JSON is
/// stored in a plain text column or a native <c>JSON</c> column. The native-JSON calls
/// (<c>json_all_paths</c>/<c>json_all_paths_with_types</c>/<c>to_json_string</c>) require a native
/// <c>JSON</c>-valued argument, so a <c>String</c> column must be cast in SQL (<c>CAST(col AS JSON)</c>).
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsJsonExtract"/> (ClickHouse) may use
/// these constructs; every other provider rejects them with a clear message.
/// </summary>
internal static class JsonExtractSqlTranslator
{
    /// <summary>
    /// Members refused for a demonstrated server-version limitation. The refusal is raised before the
    /// dialect gate, so every provider gets the precise technical reason instead of SQL the pinned
    /// ClickHouse server would reject. <c>prettyPrintJSON</c> arrived in 26.4 and
    /// <c>JSONExtractUIntOrDefault</c> does not exist at all; both absences are verified against
    /// <c>system.functions</c> and by the <c>UNKNOWN_FUNCTION</c> failure (F09 live cell).
    /// </summary>
    private static readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.pretty_print_json)] =
            "The pretty_print_json function is refused: prettyPrintJSON was introduced in ClickHouse 26.4 " +
            "and does not exist on the pinned 25.8.33.6 server (absent from system.functions; the name is " +
            "rejected with UNKNOWN_FUNCTION), so no SQL that executes on the target server can be emitted " +
            "and no documented equivalent composition exists.",
        [nameof(ClickHouseFunctions.json_extract_uint_or_default)] =
            "The json_extract_uint_or_default function is refused: no JSONExtractUIntOrDefault function " +
            "exists in ClickHouse 25.8.33.6 (absent from system.functions; the name is rejected with " +
            "UNKNOWN_FUNCTION). JSONExtractUInt already returns the vendor default 0 for a missing path, a " +
            "wrong value type or malformed JSON and NULL for a NULL input, so a default argument would " +
            "invent a semantic the server does not define. Use json_extract_uint and coalesce explicitly."
    };

    /// <summary>
    /// Translates a string-JSON extractor call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(ClickHouseFunctions))
            return false;

        if (Refused.TryGetValue(node.Method.Name, out var refusal))
            throw new NotSupportedException(refusal);

        switch (node.Method.Name)
        {
            case nameof(ClickHouseFunctions.json_extract_string) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_string");
                return true;
            case nameof(ClickHouseFunctions.json_extract_int) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_int");
                return true;
            case nameof(ClickHouseFunctions.json_extract_float) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_float");
                return true;
            case nameof(ClickHouseFunctions.json_extract_bool) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_bool");
                return true;
            case nameof(ClickHouseFunctions.json_extract_raw) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_raw");
                return true;
            case nameof(ClickHouseFunctions.json_extract_keys) when node.Arguments.Count is 1 or 2:
                EmitFunction(visitor, node, "json_extract_keys");
                return true;
            case nameof(ClickHouseFunctions.json_extract_array_raw) when node.Arguments.Count is 1 or 2:
                EmitFunction(visitor, node, "json_extract_array_raw");
                return true;
            case nameof(ClickHouseFunctions.json_extract_keys_and_values) when node.Arguments.Count is 1 or 2:
                EmitFunction(visitor, node, "json_extract_keys_and_values", valueType: node.Method.GetGenericArguments()[0]);
                return true;
            case nameof(ClickHouseFunctions.json_has) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_has");
                return true;
            case nameof(ClickHouseFunctions.json_length) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_length");
                return true;
            case nameof(ClickHouseFunctions.json_type) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_type");
                return true;
            case nameof(ClickHouseFunctions.json_value) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_value", jsonPath: true);
                return true;
            case nameof(ClickHouseFunctions.json_query) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_query", jsonPath: true);
                return true;
            case nameof(ClickHouseFunctions.json_exists) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_exists", jsonPath: true);
                return true;
            case nameof(ClickHouseFunctions.json_all_paths) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_all_paths", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.json_all_paths_with_types) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_all_paths_with_types", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.to_json_string) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "to_json_string", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.visit_param_extract_string) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "visit_param_extract_string");
                return true;
            case nameof(ClickHouseFunctions.visit_param_extract_int) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "visit_param_extract_int");
                return true;
            case nameof(ClickHouseFunctions.visit_param_extract_float) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "visit_param_extract_float");
                return true;
            case nameof(ClickHouseFunctions.visit_param_extract_bool) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "visit_param_extract_bool");
                return true;
            case nameof(ClickHouseFunctions.visit_param_extract_raw) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "visit_param_extract_raw");
                return true;
            case nameof(ClickHouseFunctions.json_merge_patch) when node.Arguments.Count is 2 or 3:
                EmitFunction(visitor, node, "json_merge_patch");
                return true;
            case nameof(ClickHouseFunctions.json_array_length) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_array_length");
                return true;
            case nameof(ClickHouseFunctions.json_dynamic_paths) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_dynamic_paths", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.json_dynamic_paths_with_types) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_dynamic_paths_with_types", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.json_shared_data_paths) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_shared_data_paths", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.json_shared_data_paths_with_types) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "json_shared_data_paths_with_types", nativeJson: true);
                return true;
            case nameof(ClickHouseFunctions.json_key) when node.Arguments.Count is 1 or 2:
                EmitFunction(visitor, node, "json_key");
                return true;
            case nameof(ClickHouseFunctions.json_extract_keys_and_values_raw) when node.Arguments.Count is 1 or 2:
                EmitFunction(visitor, node, "json_extract_keys_and_values_raw");
                return true;
            case nameof(ClickHouseFunctions.json_extract_uint) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "json_extract_uint");
                return true;
            case nameof(ClickHouseFunctions.is_valid_json) when node.Arguments.Count == 1:
                EmitFunction(visitor, node, "is_valid_json");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_raw) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_raw");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_string) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_string");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_int) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_int");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_uint) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_uint");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_float) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_float");
                return true;
            case nameof(ClickHouseFunctions.simple_json_extract_bool) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "simple_json_extract_bool");
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Renders a JSON function through <see cref="ISqlDialect.MakeJsonExtract"/>, gated by
    /// <see cref="ISqlDialect.SupportsJsonExtract"/>. <paramref name="jsonPath"/> and
    /// <paramref name="nativeJson"/> only select the failure message.
    /// </summary>
    private static void EmitFunction(BaseExpressionVisitor visitor, MethodCallExpression node, string name, bool jsonPath = false, bool nativeJson = false, Type? valueType = null)
    {
        if (!visitor.Dialect.SupportsJsonExtract)
            throw new NotSupportedException(nativeJson
                ? "The native-JSON functions (json_all_paths/json_all_paths_with_types/to_json_string) are not supported by this provider."
                : jsonPath
                    ? "The JSONPath functions (json_value/json_query/json_exists) are not supported by this provider."
                    : "The JSONExtract* JSON-as-text functions are not supported by this provider.");

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count + (valueType is null ? 0 : 1)];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        if (valueType is not null)
        {
            if (Nullable.GetUnderlyingType(valueType) is not null)
                throw new NotSupportedException(
                    "The JSONExtractKeysAndValues value type must be a non-nullable ClickHouse type.");

            rendered[^1] = $"'{visitor.Dialect.MakeTypeName(valueType)}'";
        }

        visitor.Builder!.Append(visitor.Dialect.MakeJsonExtract(name, rendered));
    }
}
