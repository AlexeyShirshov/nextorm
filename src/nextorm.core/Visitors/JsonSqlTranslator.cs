using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the PostgreSQL JSON/JSONB surface written through <see cref="CommonFunctions"/>: the
/// <c>json_agg</c>/<c>jsonb_agg</c> aggregates, the construction and conversion functions
/// (<c>json_build_object</c>, <c>to_jsonb</c>, ...) and the access/containment operators
/// (<c>-&gt;</c>, <c>-&gt;&gt;</c>, <c>#&gt;</c>, <c>@&gt;</c>, <c>?</c>, ...).
/// <para>
/// A JSON operand is expected to be a <c>json</c>/<c>jsonb</c> expression: a mapped column, another
/// JSON function or a parameter whose runtime value is a <c>JsonDocument</c>/<c>JsonElement</c>/
/// <c>JsonNode</c> (Npgsql binds those as <c>jsonb</c>). A text parameter can be parsed explicitly
/// with <see cref="PostgresFunctions.json_cast"/>. A path/keys operand is bound as a single array
/// parameter through <see cref="SqlOperandTranslator"/>.
/// </para>
/// <para>
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsJson"/> (PostgreSQL) may use these
/// constructs; every other provider rejects them with a clear message.
/// </para>
/// </summary>
internal static class JsonSqlTranslator
{
    /// <summary>
    /// Translates a JSON function/operator call. Returns <c>false</c> when the call is not part of the
    /// JSON surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        // The surface is PostgreSQL-only; other provider surfaces may reuse a method name (for example
        // ClickHouseFunctions.json_exists), so match on the declaring type too.
        if (node.Method.DeclaringType != typeof(PostgresFunctions))
            return false;

        var args = node.Arguments;

        switch (node.Method.Name)
        {
            // Aggregates: turn rows into a JSON array / object.
            case nameof(PostgresFunctions.json_agg) when args.Count == 1:
                EmitFunction(visitor, "json_agg", args);
                return true;
            case nameof(PostgresFunctions.jsonb_agg) when args.Count == 1:
                EmitFunction(visitor, "jsonb_agg", args);
                return true;
            case nameof(PostgresFunctions.json_object_agg) when args.Count == 2:
                EmitFunction(visitor, "json_object_agg", args);
                return true;
            case nameof(PostgresFunctions.jsonb_object_agg) when args.Count == 2:
                EmitFunction(visitor, "jsonb_object_agg", args);
                return true;

            // Construction and conversion.
            case nameof(PostgresFunctions.json_build_object):
                EmitVariadic(visitor, "json_build_object", args);
                return true;
            case nameof(PostgresFunctions.jsonb_build_object):
                EmitVariadic(visitor, "jsonb_build_object", args);
                return true;
            case nameof(PostgresFunctions.json_build_array):
                EmitVariadic(visitor, "json_build_array", args);
                return true;
            case nameof(PostgresFunctions.jsonb_build_array):
                EmitVariadic(visitor, "jsonb_build_array", args);
                return true;
            case nameof(PostgresFunctions.to_json) when args.Count == 1:
                EmitFunction(visitor, "to_json", args);
                return true;
            case nameof(PostgresFunctions.to_jsonb) when args.Count == 1:
                EmitFunction(visitor, "to_jsonb", args);
                return true;
            case nameof(PostgresFunctions.json_cast) when args.Count == 1:
                EmitCast(visitor, args[0]);
                return true;

            // Access operators: -> ->> #> #>>.
            case nameof(PostgresFunctions.json_get) when args.Count == 2:
                EmitOperator(visitor, "->", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_get_text) when args.Count == 2:
                EmitOperator(visitor, "->>", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_get_path) when args.Count == 2:
                EmitOperator(visitor, "#>", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_get_path_text) when args.Count == 2:
                EmitOperator(visitor, "#>>", args[0], args[1]);
                return true;

            // Containment/existence predicates: @> ? ?| ?&.
            case nameof(PostgresFunctions.json_contains) when args.Count == 2:
                EmitOperator(visitor, "@>", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_exists) when args.Count == 2:
                EmitOperator(visitor, "?", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_exists_any) when args.Count == 2:
                EmitOperator(visitor, "?|", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_exists_all) when args.Count == 2:
                EmitOperator(visitor, "?&", args[0], args[1]);
                return true;

            // Introspection.
            case nameof(PostgresFunctions.json_array_length) when args.Count == 1:
                EmitFunction(visitor, "json_array_length", args);
                return true;
            case nameof(PostgresFunctions.jsonb_array_length) when args.Count == 1:
                EmitFunction(visitor, "jsonb_array_length", args);
                return true;
            case nameof(PostgresFunctions.json_typeof) when args.Count == 1:
                EmitFunction(visitor, "json_typeof", args);
                return true;
            case nameof(PostgresFunctions.jsonb_typeof) when args.Count == 1:
                EmitFunction(visitor, "jsonb_typeof", args);
                return true;
            case nameof(PostgresFunctions.jsonb_set) when args.Count is 3 or 4:
                EmitFunction(visitor, "jsonb_set", args);
                return true;
            case nameof(PostgresFunctions.jsonb_insert) when args.Count is 3 or 4:
                EmitFunction(visitor, "jsonb_insert", args);
                return true;
            case nameof(PostgresFunctions.jsonb_strip_nulls) when args.Count == 1:
                EmitFunction(visitor, "jsonb_strip_nulls", args);
                return true;
            case nameof(PostgresFunctions.jsonb_pretty) when args.Count == 1:
                EmitFunction(visitor, "jsonb_pretty", args);
                return true;
            case nameof(PostgresFunctions.jsonb_delete) when args.Count == 2:
                EmitOperator(visitor, "-", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.row_to_json) when args.Count == 1:
                EmitFunction(visitor, "row_to_json", args);
                return true;
            case nameof(PostgresFunctions.array_to_json) when args.Count == 1:
                EmitFunction(visitor, "array_to_json", args);
                return true;
            case nameof(PostgresFunctions.json_concat) when args.Count == 2:
                EmitOperator(visitor, "||", args[0], args[1]);
                return true;

            // JSONPath: the path operand has to be rendered as jsonpath.
            case nameof(PostgresFunctions.jsonb_path_exists) when args.Count == 2:
                EmitJsonPathFunction(visitor, "jsonb_path_exists", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_match) when args.Count == 2:
                EmitJsonPathFunction(visitor, "jsonb_path_match", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_query_first) when args.Count == 2:
                EmitJsonPathFunction(visitor, "jsonb_path_query_first", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_query_array) when args.Count == 2:
                EmitJsonPathFunction(visitor, "jsonb_path_query_array", args);
                return true;

            default:
                return false;
        }
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor);
        SqlOperandTranslator.EmitFunction(visitor, sqlName, args);
    }

    private static void EmitOperator(BaseExpressionVisitor visitor, string sqlOperator, Expression left, Expression right)
    {
        RequireJsonSupport(visitor);
        SqlOperandTranslator.EmitOperator(visitor, sqlOperator, left, right);
    }

    /// <summary>
    /// Renders a <c>params object?[]</c> argument list. The C# compiler wraps the arguments of a
    /// <c>params</c> call in a single <see cref="NewArrayExpression"/>, which is flattened back into
    /// the individual arguments.
    /// </summary>
    private static void EmitVariadic(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor);

        IReadOnlyList<Expression> items = args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions }
            ? expressions
            : args;

        SqlOperandTranslator.EmitFunction(visitor, sqlName, items);
    }

    /// <summary>
    /// Renders a two-argument JSONPath function, casting the path operand to <c>jsonpath</c>
    /// (<c>cast(expr as jsonpath)</c>) so that a text parameter or column is accepted.
    /// </summary>
    private static void EmitJsonPathFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor);

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArgument(visitor, args[0]);
            SqlOperandTranslator.AppendArgument(visitor, args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(sqlName).Append('(');
        SqlOperandTranslator.AppendArgument(visitor, args[0]);
        visitor.Builder!.Append(", cast(");
        SqlOperandTranslator.AppendArgument(visitor, args[1]);
        visitor.Builder!.Append(" as jsonpath))");
    }

    private static void EmitCast(BaseExpressionVisitor visitor, Expression argument)
    {
        RequireJsonSupport(visitor);

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append("cast(");
        }

        SqlOperandTranslator.AppendArgument(visitor, argument);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(" as jsonb)");
    }

    private static void RequireJsonSupport(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsJson)
            throw new NotSupportedException(
                "JSON is not supported by this provider: the json/jsonb functions and operators require PostgreSQL.");
    }
}
