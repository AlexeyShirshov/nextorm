using System.Globalization;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Translates the PostgreSQL JSON/JSONB surface written through <see cref="CommonFunctions"/>: the
/// <c>json_agg</c>/<c>jsonb_agg</c> aggregates, the construction and conversion functions
/// (<c>json_build_object</c>, <c>json_array</c>/<c>jsonb_array</c>, <c>to_jsonb</c>, ...), the
/// SQL/JSON query functions (<c>json_value</c>/<c>json_query</c>/<c>json_exists</c>) and the
/// access/containment operators (<c>-&gt;</c>, <c>-&gt;&gt;</c>, <c>#&gt;</c>, <c>@&gt;</c>,
/// <c>?</c>, ...).
/// <para>
/// A JSON operand is expected to be a <c>json</c>/<c>jsonb</c> expression: a mapped column, another
/// JSON function or a parameter whose runtime value is a <c>JsonDocument</c>/<c>JsonElement</c>/
/// <c>JsonNode</c> (Npgsql binds those as <c>jsonb</c>). A text parameter can be parsed explicitly
/// with <see cref="PostgresFunctions.json_cast"/>. A path/keys operand is bound as a single array
/// parameter through <see cref="SqlOperandTranslator"/>.
/// </para>
/// <para>
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsPostgresJsonSql"/> (PostgreSQL) may use
/// these constructs; every other provider rejects them with a clear message. This is deliberately distinct
/// from <see cref="ISqlDialect.SupportsJson"/>, which only advertises native JSON <i>storage</i> (both
/// PostgreSQL and ClickHouse opt into that).
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
            case nameof(PostgresFunctions.json_array):
                EmitVariadic(visitor, "json_array", args);
                return true;
            case nameof(PostgresFunctions.jsonb_array):
                EmitJsonbArray(visitor, args);
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
            case nameof(PostgresFunctions.json) when args.Count == 1:
                EmitJsonTextConstructor(visitor, "json", args[0]);
                return true;
            case nameof(PostgresFunctions.json_scalar) when args.Count == 1:
                EmitNamedFunction(visitor, "json_scalar", args);
                return true;
            case nameof(PostgresFunctions.json_serialize) when args.Count == 1:
                EmitJsonTextConstructor(visitor, "json_serialize", args[0]);
                return true;
            case nameof(PostgresFunctions.json_extract_path) when args.Count == 2:
                EmitExtractPath(visitor, "json_extract_path", "json", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.json_extract_path_text) when args.Count == 2:
                EmitExtractPath(visitor, "json_extract_path_text", "json", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.jsonb_extract_path) when args.Count == 2:
                EmitExtractPath(visitor, "jsonb_extract_path", "jsonb", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.jsonb_extract_path_text) when args.Count == 2:
                EmitExtractPath(visitor, "jsonb_extract_path_text", "jsonb", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.jsonb_set_lax) when args.Count == 5:
                EmitJsonbSetLax(visitor, args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_exists_tz) when args.Count == 4:
                EmitJsonbPathTz(visitor, "jsonb_path_exists_tz", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_match_tz) when args.Count == 4:
                EmitJsonbPathTz(visitor, "jsonb_path_match_tz", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_query_array_tz) when args.Count == 4:
                EmitJsonbPathTz(visitor, "jsonb_path_query_array_tz", args);
                return true;
            case nameof(PostgresFunctions.jsonb_path_query_first_tz) when args.Count == 4:
                EmitJsonbPathTz(visitor, "jsonb_path_query_first_tz", args);
                return true;

            // Access operators: -> ->> #> #>>. Both operands have to carry a type: PostgreSQL cannot
            // resolve the operator family when an operand is `unknown` (SQLSTATE 42725).
            case nameof(PostgresFunctions.json_get) when args.Count == 2:
                EmitAccess(visitor, "->", args[0], args[1], KeyCastOf(args[1]));
                return true;
            case nameof(PostgresFunctions.json_get_text) when args.Count == 2:
                EmitAccess(visitor, "->>", args[0], args[1], KeyCastOf(args[1]));
                return true;
            case nameof(PostgresFunctions.json_get_path) when args.Count == 2:
                EmitAccess(visitor, "#>", args[0], args[1], JsonKeyCast.TextArray);
                return true;
            case nameof(PostgresFunctions.json_get_path_text) when args.Count == 2:
                EmitAccess(visitor, "#>>", args[0], args[1], JsonKeyCast.TextArray);
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
            case nameof(PostgresFunctions.jsonpath) when args.Count == 1:
                EmitJsonPathCast(visitor, args[0]);
                return true;

            // SQL/JSON query functions: the path operand has to be rendered as jsonpath.
            case nameof(PostgresFunctions.json_value) when args.Count == 2:
                EmitJsonPathFunction(visitor, "json_value", args);
                return true;
            case nameof(PostgresFunctions.json_query) when args.Count == 2:
                EmitJsonPathFunction(visitor, "json_query", args);
                return true;
            case nameof(PostgresFunctions.json_exists) when args.Count == 3:
                EmitJsonPathFunction(visitor, "json_exists", [args[0], args[1]]);
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

    /// <summary>
    /// Renders <c>name(arg, ...)</c> with every argument rendered as-is, refusing a non-PostgreSQL provider
    /// with a message that names the function (the #281 JSON members are gated per name at the call site).
    /// </summary>
    private static void EmitNamedFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor, sqlName);
        SqlOperandTranslator.EmitFunction(visitor, sqlName, args);
    }

    private static void EmitOperator(BaseExpressionVisitor visitor, string sqlOperator, Expression left, Expression right)
    {
        RequireJsonSupport(visitor);
        SqlOperandTranslator.EmitOperator(visitor, sqlOperator, left, right);
    }

    /// <summary>The SQL type the key/index/path operand of an access operator is cast to.</summary>
    private enum JsonKeyCast
    {
        /// <summary>A key: <c>cast(key as text)</c>.</summary>
        Text,

        /// <summary>An array index: <c>cast(index as integer)</c>.</summary>
        Integer,

        /// <summary>A path: <c>cast(path as text[])</c>.</summary>
        TextArray
    }

    /// <summary>
    /// Where the document operand of an access operator comes from. Decided structurally: no
    /// expression is compiled or evaluated here, the shape of the tree (and, for a member chain, its
    /// root) is the only evidence used.
    /// </summary>
    private enum JsonDocumentProvenance
    {
        /// <summary>A value produced by the client (a literal, a closure capture, a bound parameter).</summary>
        RawClientValue,

        /// <summary>A server-side <c>json</c>/<c>jsonb</c> expression: a mapped column or a JSON function.</summary>
        NativeServerExpression,

        /// <summary>Neither could be established; the operand is left exactly as it renders.</summary>
        Unresolved
    }

    /// <summary>
    /// Renders a PostgreSQL JSON access operator (<c>-&gt;</c>, <c>-&gt;&gt;</c>, <c>#&gt;</c>,
    /// <c>#&gt;&gt;</c>) with both operands typed. The document is cast to <c>jsonb</c> only when it is
    /// a raw client value; a mapped native <c>json</c>/<c>jsonb</c> column or an existing server-side
    /// JSON expression is preserved, because casting it would change the operator family and the
    /// result type. The key/index/path is always cast to the type the overload expects, so a null or
    /// text-literal operand is still typed.
    /// </summary>
    private static void EmitAccess(
        BaseExpressionVisitor visitor,
        string sqlOperator,
        Expression document,
        Expression key,
        JsonKeyCast keyCast)
    {
        RequireJsonSupport(visitor);

        // A literal key/path is inlined, so it contributes no parameter in either pass. The decision
        // is structural and is taken in parameter mode too, keeping the extracted parameters aligned
        // with the SQL pass.
        var inlineKey = IsInlineKey(key, keyCast);

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArgument(visitor, document);
            if (!inlineKey)
                SqlOperandTranslator.AppendArgument(visitor, key);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var builder = visitor.Builder!;
        builder.Append('(');

        AppendDocumentOperand(visitor, document);

        builder.Append(' ').Append(sqlOperator).Append(' ');
        builder.Append(visitor.Kw("cast("));

        if (inlineKey)
            AppendInlineKey(visitor, key, keyCast);
        else
            SqlOperandTranslator.AppendArgument(visitor, key);

        builder.Append(visitor.Kw(" as ")).Append(KeyCastTypeName(keyCast)).Append("))");
    }

    /// <summary>
    /// Renders the left operand of an access operator. A raw client value is wrapped in
    /// <c>cast(... as jsonb)</c>; a native column or a nested JSON expression is emitted unchanged.
    /// </summary>
    private static void AppendDocumentOperand(BaseExpressionVisitor visitor, Expression document)
    {
        switch (ClassifyDocument(visitor, document))
        {
            case JsonDocumentProvenance.RawClientValue:
                visitor.Builder!.Append(visitor.Kw("cast("));
                AppendRawJsonValue(visitor, document);
                visitor.Builder!.Append(visitor.Kw(" as jsonb)"));
                return;

            case JsonDocumentProvenance.NativeServerExpression:
                SqlOperandTranslator.AppendArgument(visitor, document);
                return;

            default:
                // Fail closed: an operand whose provenance cannot be decided keeps its own rendering.
                // A `cast(... as jsonb)` here could coerce a server expression (changing the result
                // type), and guessing the other way risks SQLSTATE 42725 on an `unknown` operand, so
                // the source has to be made explicit by the caller (json_cast).
                ReportUnresolvedDocument(visitor, document);
                SqlOperandTranslator.AppendArgument(visitor, document);
                return;
        }
    }

    /// <summary>
    /// Renders a raw client JSON value inside <c>cast(... as jsonb)</c>. A compile-time string
    /// literal is quoted here, with embedded apostrophes doubled, because the shared constant policy
    /// (<see cref="BaseExpressionVisitor"/>) writes a string constant verbatim; a captured value, an
    /// explicit <see cref="SqlFunctions.Parameter{T}(int)"/> or a JSON value keeps its own rendering,
    /// so the parameter naming/order and the plan cache are untouched.
    /// </summary>
    private static void AppendRawJsonValue(BaseExpressionVisitor visitor, Expression document)
    {
        var constant = TypeFacts.UnwrapConvert(document) as ConstantExpression;

        if (constant is null)
        {
            SqlOperandTranslator.AppendArgument(visitor, document);
            return;
        }

        if (constant.Type == typeof(string) && constant.Value is string text)
        {
            // grammar token: the raw JSON document sits in a cast(… as jsonb) expression position, so
            // the provider's ordinary literal spelling is legal here (no D6 fallback needed).
            visitor.Builder!.Append(visitor.RenderStringLiteral(
                text, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.JsonDocument));
            return;
        }

        if (constant.Value is null)
        {
            visitor.Builder!.Append(visitor.Kw("null"));
            return;
        }

        SqlOperandTranslator.AppendArgument(visitor, document);
    }

    /// <summary>
    /// Classifies the document operand without evaluating it. Raw: a constant (including a null one),
    /// an array construction, a member/field chain rooted at a constant (a closure capture) or at no
    /// receiver (a static field), and a bound client parameter. Native: a member chain rooted at the
    /// entity/projection source (the column path) and any method call, which is by construction a
    /// server-side expression (<c>json_build_object</c>, <c>json_cast</c>, a <c>SqlFunctions</c>
    /// helper, ...) and must not be coerced.
    /// </summary>
    private static JsonDocumentProvenance ClassifyDocument(BaseExpressionVisitor visitor, Expression document)
    {
        var expression = TypeFacts.UnwrapConvert(document);

        switch (expression)
        {
            case ConstantExpression:
            case NewArrayExpression:
                return JsonDocumentProvenance.RawClientValue;

            case ParameterExpression parameter:
                // A query-source parameter renders as a column; any other one is a bound client value.
                return IsQuerySourceParameter(visitor, parameter)
                    ? JsonDocumentProvenance.NativeServerExpression
                    : JsonDocumentProvenance.RawClientValue;

            case MemberExpression member:
                return ClassifyMemberChain(visitor, member);

            case MethodCallExpression call:
                // SqlFunctions.Parameter<T>(idx) is a bound client parameter that happens to be
                // expressed as a call; every other call is a server-side expression.
                return call.Method.DeclaringType == typeof(SqlFunctions)
                    && call.Method.Name == nameof(SqlFunctions.Parameter)
                        ? JsonDocumentProvenance.RawClientValue
                        : JsonDocumentProvenance.NativeServerExpression;

            default:
                return JsonDocumentProvenance.Unresolved;
        }
    }

    /// <summary>
    /// Resolves a member chain by its root only — the same test
    /// <see cref="SqlOperandTranslator.IsCapturedArray"/> uses: a root constant or no receiver is a
    /// capture, a query-source parameter is a column. Nothing in the chain is evaluated.
    /// </summary>
    private static JsonDocumentProvenance ClassifyMemberChain(BaseExpressionVisitor visitor, MemberExpression member)
    {
        var root = member;
        while (root.Expression is MemberExpression outer)
            root = outer;

        switch (root.Expression)
        {
            case null:
            case ConstantExpression:
                return JsonDocumentProvenance.RawClientValue;

            case ParameterExpression parameter:
                return IsQuerySourceParameter(visitor, parameter)
                    ? JsonDocumentProvenance.NativeServerExpression
                    : JsonDocumentProvenance.RawClientValue;

            default:
                return JsonDocumentProvenance.Unresolved;
        }
    }

    /// <summary>
    /// True when the parameter is a query source (the entity lambda parameter or a projection
    /// source), which the member translator renders as a column. Mirrors the column test in
    /// <see cref="MemberTranslator.VisitMember"/>.
    /// </summary>
    private static bool IsQuerySourceParameter(BaseExpressionVisitor visitor, ParameterExpression parameter)
        => parameter.Type == visitor.EntityType || parameter.Type.IsAssignableTo(typeof(IProjection));

    /// <summary>
    /// True for a key/index/path written as a compile-time literal: a string key, an integer index, a
    /// null key or path, or an array of string literals. Such an operand is inlined (quoted through
    /// <see cref="SqlLiteral.ToSqlStringLiteral"/>) instead of becoming a parameter.
    /// </summary>
    private static bool IsInlineKey(Expression key, JsonKeyCast keyCast)
    {
        var expression = TypeFacts.UnwrapConvert(key);

        return keyCast switch
        {
            JsonKeyCast.Integer => expression is ConstantExpression { Value: int },
            JsonKeyCast.Text => expression is ConstantExpression constant && constant.Value is null or string,
            _ => IsInlinePath(expression)
        };
    }

    private static bool IsInlinePath(Expression expression)
    {
        if (expression is ConstantExpression { Value: null })
            return true;

        return expression is NewArrayExpression { Expressions.Count: > 0 } array
            && array.Expressions.All(IsInlinePathElement);
    }

    private static bool IsInlinePathElement(Expression element)
    {
        var expression = TypeFacts.UnwrapConvert(element);
        return expression is ConstantExpression { Value: null }
            || expression is ConstantExpression { Value: string };
    }

    /// <summary>Renders an inlined literal key/index/path; <see cref="IsInlineKey"/> gates the shapes.</summary>
    private static void AppendInlineKey(BaseExpressionVisitor visitor, Expression key, JsonKeyCast keyCast)
    {
        switch (TypeFacts.UnwrapConvert(key))
        {
            case ConstantExpression { Value: null }:
                visitor.Builder!.Append(visitor.Kw("null"));
                return;

            case ConstantExpression { Value: string text }:
                // grammar token: the key sits in a cast(… as text) expression position, so the
                // provider's ordinary literal spelling is legal here (no D6 fallback needed).
                visitor.Builder!.Append(visitor.RenderStringLiteral(
                    text, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.JsonKey));
                return;

            case ConstantExpression { Value: int index }:
                visitor.Builder!.Append(Convert.ToString(index, CultureInfo.InvariantCulture));
                return;

            case NewArrayExpression { Expressions: var items } when keyCast == JsonKeyCast.TextArray:
                AppendPathArrayLiteral(visitor, items);
                return;

            default:
                // Unreachable while IsInlineKey gates the call; render the operand rather than drop it.
                SqlOperandTranslator.AppendArgument(visitor, key);
                return;
        }
    }

    /// <summary>Renders <c>array['a', 'b']</c> with every element quoted as an SQL string literal.</summary>
    private static void AppendPathArrayLiteral(BaseExpressionVisitor visitor, IReadOnlyList<Expression> items)
    {
        var builder = visitor.Builder!;
        builder.Append(visitor.Kw("array["));

        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            if (i > 0) builder.Append(", ");

            builder.Append(TypeFacts.UnwrapConvert(items[i]) is ConstantExpression { Value: string text }
                // grammar token: a path element sits in an array[…] expression position, so the
                // provider's ordinary literal spelling is legal here (no D6 fallback needed).
                ? visitor.RenderStringLiteral(
                    text, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.JsonPathElement)
                : visitor.Kw("null"));
        }

        builder.Append(']');
    }

    /// <summary>Reports an operand whose provenance could not be established (debug level, no SQL effect).</summary>
    private static void ReportUnresolvedDocument(BaseExpressionVisitor visitor, Expression document)
    {
        if (visitor.Logger is not { } logger || !logger.IsEnabled(LogLevel.Debug))
            return;

        logger.LogDebug(
            "JSON access: the document operand ({NodeType} of {Type}) has no resolvable provenance and is emitted without a jsonb cast; wrap a raw value in PostgresFunctions.json_cast.",
            document.NodeType,
            document.Type.Name);
    }

    private static JsonKeyCast KeyCastOf(Expression key)
        => TypeFacts.UnwrapConvert(key).Type == typeof(int) ? JsonKeyCast.Integer : JsonKeyCast.Text;

    private static string KeyCastTypeName(JsonKeyCast keyCast) => keyCast switch
    {
        JsonKeyCast.Integer => "integer",
        JsonKeyCast.TextArray => "text[]",
        _ => "text"
    };

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
    /// Renders the SQL/JSON JSONB array constructor. PostgreSQL has no <c>jsonb_array</c> function, so
    /// the <c>json_array(... returning jsonb)</c> form is emitted.
    /// </summary>
    private static void EmitJsonbArray(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor);

        IReadOnlyList<Expression> items = args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions }
            ? expressions
            : args;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                visitor.Visit(items[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append("json_array(");

        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            if (i > 0) builder.Append(", ");
            SqlOperandTranslator.AppendArgument(visitor, items[i]);
        }

        builder.Append(visitor.Kw(" returning jsonb)"));
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
        visitor.Builder!.Append(visitor.Kw(", cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[1]);
        visitor.Builder!.Append(visitor.Kw(" as jsonpath))"));
    }

    private static void EmitCast(BaseExpressionVisitor visitor, Expression argument)
    {
        RequireJsonSupport(visitor);

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(visitor.Kw("cast("));
        }

        SqlOperandTranslator.AppendArgument(visitor, argument);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Kw(" as jsonb)"));
    }

    /// <summary>
    /// Renders the SQL/JSON text constructors <c>json(value FORMAT JSON)</c> and
    /// <c>json_serialize(value FORMAT JSON RETURNING text)</c>. Both are parser-level constructs
    /// (no <c>pg_proc</c> row on PostgreSQL 17, measured live), and both take the argument's JSON
    /// <em>text</em>: the operand is cast to <c>text</c> and <c>FORMAT JSON</c> declares the result of
    /// that cast to be JSON, so the server parses it. <c>json_serialize</c> additionally names its text
    /// result with <c>RETURNING text</c>, which is what distinguishes it from <c>json</c>.
    /// </summary>
    private static void EmitJsonTextConstructor(BaseExpressionVisitor visitor, string sqlName, Expression value)
    {
        RequireJsonSupport(visitor, sqlName);

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArgument(visitor, value);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append(sqlName).Append('(').Append(visitor.Kw("cast("));
        SqlOperandTranslator.AppendArgument(visitor, value);
        builder.Append(visitor.Kw(" as text) format json"));

        if (sqlName == "json_serialize")
            builder.Append(visitor.Kw(" returning text"));

        builder.Append(')');
    }

    /// <summary>
    /// Renders <c>json_extract_path</c>/<c>jsonb_extract_path</c> (and the <c>_text</c> forms). The
    /// document is a JSON text, so it is cast to the function's document type; the path list is bound as
    /// one <c>text[]</c> and passed through the <c>VARIADIC</c> parameter, which PostgreSQL requires when
    /// the array arrives as a single expression (measured: the bare <c>text[]</c> form resolves to no
    /// function).
    /// </summary>
    private static void EmitExtractPath(
        BaseExpressionVisitor visitor,
        string sqlName,
        string documentType,
        Expression document,
        Expression path)
    {
        RequireJsonSupport(visitor, sqlName);

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArgument(visitor, document);
            SqlOperandTranslator.AppendArgument(visitor, path);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append(sqlName).Append('(').Append(visitor.Kw("cast("));
        SqlOperandTranslator.AppendArgument(visitor, document);
        builder.Append(visitor.Kw(" as ")).Append(documentType).Append(visitor.Kw("), variadic cast("));
        SqlOperandTranslator.AppendArgument(visitor, path);
        builder.Append(visitor.Kw(" as text[]))"));
    }

    /// <summary>
    /// Renders <c>jsonb_set_lax(jsonb, text[], jsonb, boolean, text)</c>. The document and the new value
    /// are JSON texts (cast to <c>jsonb</c>), the path is one <c>text[]</c> (not variadic: this function
    /// takes the array as one argument), and the flag/treatment are rendered as ordinary arguments so a
    /// bound parameter reaches the server (measured: the treatment parameter is accepted and validated
    /// there).
    /// </summary>
    private static void EmitJsonbSetLax(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor, "jsonb_set_lax");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArgument(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append("jsonb_set_lax(").Append(visitor.Kw("cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[0]);
        builder.Append(visitor.Kw(" as jsonb), cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[1]);
        builder.Append(visitor.Kw(" as text[]), cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[2]);
        builder.Append(visitor.Kw(" as jsonb), "));
        SqlOperandTranslator.AppendArgument(visitor, args[3]);
        builder.Append(", ");
        SqlOperandTranslator.AppendArgument(visitor, args[4]);
        builder.Append(')');
    }

    /// <summary>
    /// Renders a <c>jsonb_path_*_tz</c> function: the document and the variables are JSON texts (cast to
    /// <c>jsonb</c>), the path is cast to <c>jsonpath</c> (a text parameter is not implicitly coerced) and
    /// the <c>silent</c> flag is an ordinary boolean argument. These four-argument forms exist only with
    /// the <c>_tz</c> suffix; the plain siblings reject a time-zone-dependent <c>.datetime()</c> path.
    /// </summary>
    private static void EmitJsonbPathTz(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireJsonSupport(visitor, sqlName);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArgument(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append(sqlName).Append('(').Append(visitor.Kw("cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[0]);
        builder.Append(visitor.Kw(" as jsonb), cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[1]);
        builder.Append(visitor.Kw(" as jsonpath), cast("));
        SqlOperandTranslator.AppendArgument(visitor, args[2]);
        builder.Append(visitor.Kw(" as jsonb), "));
        SqlOperandTranslator.AppendArgument(visitor, args[3]);
        builder.Append(')');
    }

    /// <summary>
    /// Casts an argument to <c>jsonpath</c> (<c>cast(expr as jsonpath)</c>) for
    /// <see cref="PostgresFunctions.jsonpath"/> so that a text parameter or column is accepted where
    /// PostgreSQL expects a <c>jsonpath</c>.
    /// </summary>
    private static void EmitJsonPathCast(BaseExpressionVisitor visitor, Expression argument)
    {
        RequireJsonSupport(visitor);

        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(visitor.Kw("cast("));
        }

        SqlOperandTranslator.AppendArgument(visitor, argument);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Kw(" as jsonpath)"));
    }

    /// <summary>
    /// Refuses a provider without <see cref="ISqlDialect.SupportsPostgresJsonSql"/>. With a
    /// <paramref name="functionName"/> the message names the refused function, which is what the
    /// per-name provider rejection contract of #281 asserts; the family-wide wording is kept for the
    /// members enrolled before that contract.
    /// </summary>
    private static void RequireJsonSupport(BaseExpressionVisitor visitor, string? functionName = null)
    {
        if (visitor.Dialect.SupportsPostgresJsonSql)
            return;

        throw new NotSupportedException(functionName is null
            ? "JSON is not supported by this provider: the json/jsonb functions and operators require PostgreSQL."
            : $"The {functionName} function is not supported by this provider: the PostgreSQL JSON surface requires PostgreSQL.");
    }
}
