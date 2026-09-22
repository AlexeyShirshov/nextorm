using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the string-JSON surface of <see cref="ClickHouseFunctions"/> (<c>json_extract_string</c>,
/// <c>json_extract_int</c>, <c>json_extract_float</c>, <c>json_extract_bool</c>, <c>json_extract_raw</c>,
/// <c>json_has</c>, <c>json_length</c>, <c>json_type</c>, the flat-JSON fast path
/// <c>visit_param_extract_string</c>/<c>_int</c>/<c>_float</c>/<c>_bool</c>/<c>_raw</c>, the JSONPath
/// scalars <c>json_value</c>/<c>json_query</c>/<c>json_exists</c> and the native-JSON
/// <c>json_all_paths</c>/<c>json_all_paths_with_types</c>/<c>to_json_string</c> functions), where JSON is
/// stored in a plain text column or a native <c>JSON</c> column. The native-JSON calls
/// (<c>json_all_paths</c>/<c>json_all_paths_with_types</c>/<c>to_json_string</c>) require a native
/// <c>JSON</c>-valued argument, so a <c>String</c> column must be cast in SQL (<c>CAST(col AS JSON)</c>).
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsJsonExtract"/> (ClickHouse) may use
/// these constructs; every other provider rejects them with a clear message.
/// </summary>
internal static class JsonExtractSqlTranslator
{
    /// <summary>
    /// Translates a string-JSON extractor call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
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
            default:
                return false;
        }
    }

    /// <summary>
    /// Renders a JSON function through <see cref="ISqlDialect.MakeJsonExtract"/>, gated by
    /// <see cref="ISqlDialect.SupportsJsonExtract"/>. <paramref name="jsonPath"/> and
    /// <paramref name="nativeJson"/> only select the failure message.
    /// </summary>
    private static void EmitFunction(BaseExpressionVisitor visitor, MethodCallExpression node, string name, bool jsonPath = false, bool nativeJson = false)
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
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeJsonExtract(name, rendered));
    }
}
