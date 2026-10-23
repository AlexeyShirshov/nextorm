using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the ClickHouse-native functions declared on <see cref="ClickHouseFunctions"/> that have no
/// cross-provider spelling: the UTF-8 case, the RE2 replace/search/split family, the map and bitmap
/// families, the hash functions and <c>generateULID</c>. The native name is produced by the dialect's
/// <see cref="IScalarFunctions"/> renderer, so a provider that does not report the name as supported
/// rejects the call with <see cref="NotSupportedException"/> instead of emitting invalid SQL.
/// <para>
/// The array arguments are rendered through <see cref="SqlOperandTranslator.AppendArrayOrColumn"/>, so an
/// array column stays a column and a captured array is bound as a single parameter; the <c>params</c>
/// overloads flatten the compiler-generated argument array into positional arguments.
/// </para>
/// </summary>
internal static class ClickHouseNativeFunctionTranslator
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.lower_utf8), nameof(ClickHouseFunctions.upper_utf8),
        nameof(ClickHouseFunctions.trim_left), nameof(ClickHouseFunctions.trim_right),
        nameof(ClickHouseFunctions.trim_both),
        nameof(ClickHouseFunctions.replace_regexp_one), nameof(ClickHouseFunctions.replace_regexp_all),
        nameof(ClickHouseFunctions.match), nameof(ClickHouseFunctions.extract),
        nameof(ClickHouseFunctions.extract_all),
        nameof(ClickHouseFunctions.split_by_string), nameof(ClickHouseFunctions.split_by_regexp),
        nameof(ClickHouseFunctions.split_by_whitespace),
        nameof(ClickHouseFunctions.format_date_time), nameof(ClickHouseFunctions.parse_date_time),
        nameof(ClickHouseFunctions.parse_date_time_best_effort),
        nameof(ClickHouseFunctions.now), nameof(ClickHouseFunctions.today),
        nameof(ClickHouseFunctions.yesterday),
        nameof(ClickHouseFunctions.array_concat), nameof(ClickHouseFunctions.array_flatten),
        nameof(ClickHouseFunctions.array_uniq), nameof(ClickHouseFunctions.array_intersect),
        nameof(ClickHouseFunctions.array_union), nameof(ClickHouseFunctions.array_except),
        nameof(ClickHouseFunctions.array_symmetric_difference),
        nameof(ClickHouseFunctions.map), nameof(ClickHouseFunctions.map_keys),
        nameof(ClickHouseFunctions.map_values), nameof(ClickHouseFunctions.map_contains_key),
        nameof(ClickHouseFunctions.map_contains_value), nameof(ClickHouseFunctions.map_add),
        nameof(ClickHouseFunctions.map_concat), nameof(ClickHouseFunctions.map_filter),
        nameof(ClickHouseFunctions.map_apply), nameof(ClickHouseFunctions.map_all),
        nameof(ClickHouseFunctions.map_exists), nameof(ClickHouseFunctions.map_sort),
        nameof(ClickHouseFunctions.group_bitmap), nameof(ClickHouseFunctions.group_bitmap_and),
        nameof(ClickHouseFunctions.group_bitmap_or), nameof(ClickHouseFunctions.group_bitmap_xor),
        nameof(ClickHouseFunctions.sum_map), nameof(ClickHouseFunctions.sum_map_filtered),
        nameof(ClickHouseFunctions.md5), nameof(ClickHouseFunctions.sha1),
        nameof(ClickHouseFunctions.sha256), nameof(ClickHouseFunctions.sha512),
        nameof(ClickHouseFunctions.xx_hash32), nameof(ClickHouseFunctions.xx_hash64),
        nameof(ClickHouseFunctions.xxh3), nameof(ClickHouseFunctions.city_hash64),
        nameof(ClickHouseFunctions.sip_hash64), nameof(ClickHouseFunctions.sip_hash128),
        nameof(ClickHouseFunctions.murmur_hash2_32), nameof(ClickHouseFunctions.murmur_hash2_64),
        nameof(ClickHouseFunctions.murmur_hash3_32), nameof(ClickHouseFunctions.murmur_hash3_64),
        nameof(ClickHouseFunctions.murmur_hash3_128),
        nameof(ClickHouseFunctions.generate_ulid),
        nameof(ClickHouseFunctions.log2), nameof(ClickHouseFunctions.cbrt),
        nameof(ClickHouseFunctions.exp2), nameof(ClickHouseFunctions.exp10)
    };

    private static readonly HashSet<string> ParamsArrayFunctions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.array_concat), nameof(ClickHouseFunctions.array_intersect),
        nameof(ClickHouseFunctions.array_union), nameof(ClickHouseFunctions.array_except),
        nameof(ClickHouseFunctions.array_symmetric_difference), nameof(ClickHouseFunctions.map_concat)
    };

    private static readonly HashSet<string> MapLambdaFunctions = new(StringComparer.Ordinal)
    {
        nameof(ClickHouseFunctions.map_filter), nameof(ClickHouseFunctions.map_apply),
        nameof(ClickHouseFunctions.map_all), nameof(ClickHouseFunctions.map_exists)
    };

    /// <summary>
    /// The CLR scalars the ClickHouse dialect maps to a name identical to the CLR name (<c>Int16</c>,
    /// <c>Int32</c>, <c>UInt32</c>, <c>Int64</c> and <c>DateTime</c>): the mapping is real, not the
    /// dialect's generic <c>Type.Name</c> fallback for an unmapped type, so the resulting string alone
    /// cannot tell them apart and they are admitted by type. <c>float</c> is deliberately absent: the
    /// dialect maps it to <c>Float32</c>, which differs from the CLR name <c>Single</c> and is therefore
    /// already distinguishable from the fallback.
    /// </summary>
    private static readonly HashSet<Type> IdentityMappedTypes =
    [
        typeof(short), typeof(int), typeof(uint), typeof(long), typeof(DateTime)
    ];

    /// <summary>The inline <c>map(key, value, ...)</c> constructor, the only inline map operand source.</summary>
    private const string MapConstructorName = nameof(ClickHouseFunctions.map);

    /// <summary>The map-only sort arm, lowered onto the tuple-array representation.</summary>
    private const string MapSortName = nameof(ClickHouseFunctions.map_sort);

    /// <summary>Base name of the generated tuple lambda identifier of the <c>arrayZip</c> arms.</summary>
    private const string TupleIdentifierBase = "_t";

    /// <summary>Translates a ClickHouse-native call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(ClickHouseFunctions) || !Names.Contains(node.Method.Name))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.ScalarFunctions is not { } scalars || !scalars.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (ParamsArrayFunctions.Contains(name))
        {
            EmitParamsArray(visitor, node, scalars, name);
            return true;
        }

        if (name is nameof(ClickHouseFunctions.array_flatten) or nameof(ClickHouseFunctions.array_uniq))
        {
            EmitSingleArray(visitor, node, scalars, name);
            return true;
        }

        if (MapLambdaFunctions.Contains(name) || name == MapSortName)
        {
            EmitMapFunction(visitor, node, scalars, name);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.map))
        {
            EmitMapConstructor(visitor, node, scalars, name);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.sum_map_filtered))
        {
            EmitSumMapFiltered(visitor, node, scalars, name);
            return true;
        }

        EmitScalar(visitor, node, scalars, name);
        return true;
    }

    /// <summary>Renders a function over scalar (non-array) arguments.</summary>
    private static void EmitScalar(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var args = DropTrailingNulls(node.Arguments);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>Renders a <c>params</c> array function, flattening the argument array into positional arguments.</summary>
    private static void EmitParamsArray(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var items = FlattenParams(node.Arguments);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, items[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var rendered = new string[items.Count];

        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
            rendered[i] = Capture(builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, items[i]));

        builder.Append(scalars.Render(name, rendered));
    }

    /// <summary>Renders a function with a single array argument (a column or a captured array).</summary>
    private static void EmitSingleArray(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var argument = node.Arguments[0];

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArrayOrColumn(visitor, argument);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var rendered = Capture(builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, argument));
        builder.Append(scalars.Render(name, [rendered]));
    }

    /// <summary>Renders the <c>map(key, value, ...)</c> constructor from inline key/value tuples.</summary>
    private static void EmitMapConstructor(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var pairs = FlattenTuplePairs(node.Arguments);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
            {
                var pair = pairs[i];
                for (var (j, jcnt) = (0, pair.Count); j < jcnt; j++)
                    visitor.Visit(pair[j]);
            }

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new List<string>(pairs.Count * 2);
        for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
        {
            var pair = pairs[i];
            for (var (j, jcnt) = (0, pair.Count); j < jcnt; j++)
                rendered.Add(visitor.VisitToString(pair[j]));
        }

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }

    /// <summary>
    /// Renders the map helpers, which have no native server form with the CLR-shaped result the public
    /// surface declares:
    /// <c>map_filter</c> and <c>map_sort</c> iterate a zipped <c>(key, value)</c> array,
    /// <c>map_apply</c> maps the key and value arrays, and the native <c>mapAll</c>/<c>mapExists</c>
    /// predicates keep their own spelling. The map operand is exactly one operand: an inline
    /// <c>map(...)</c> constructor rendered through the pair path, or one captured dictionary bound as a
    /// single map parameter. A null operand is refused before any SQL reaches the server.
    /// </summary>
    private static void EmitMapFunction(
        BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        // map_sort has no lambda; its ordering lambda is generated over the zipped pair tuple.
        if (name == MapSortName)
        {
            var sortMap = RenderMapOperand(visitor, node.Arguments[0]);

            if (visitor.IsParamMode)
                return;

            // The generated identifier must not shadow one the operand already renders (an outer lambda
            // parameter called `_t`, for example), so the operand is scanned along with the (absent) body.
            var tuple = FreshIdentifier(null, node.Arguments[0]);
            var sortLowered = $"arraySort({tuple} -> {tuple}.1, {ZipMapOperand(sortMap)})";

            // The cast composes the declared result type and can refuse the call; the alias flag is set
            // only after that succeeded, so a refusal leaves no sticky flag behind.
            var sortSql = CastMapResult(visitor.Dialect, node, name, sortLowered);

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(sortSql);
            return;
        }

        var lambda = ExtractLambda(node.Arguments[0]);

        if (lambda.Parameters.Count != 2)
            throw new NotSupportedException($"The {name} lambda must have exactly two parameters (key, value).");

        var first = lambda.Parameters[0];
        var second = lambda.Parameters[1];

        // map_filter iterates a zipped (key, value) tuple, so its two parameters are rendered as the
        // tuple fields of one fresh lambda identifier. The rebinding is carried by the parameter
        // identity: the body's ParameterExpression nodes are mapped, never its identifier text, so
        // nested scopes and every other identifier stay untouched.
        var isFilter = name == nameof(ClickHouseFunctions.map_filter);
        var filterTuple = isFilter ? FreshIdentifier(lambda.Body, node.Arguments[1]) : null;

        // The body may reference an enclosing higher-order lambda's parameter (a nested
        // array_map(x => ... map_filter((k, v) => v > x, m))); the outer bindings are carried into the
        // body visitor so that reference still renders. An inner parameter of the same name keeps
        // winning through SQL scoping, because both are emitted as their bare identifier.
        var parameters = new Dictionary<ParameterExpression, string>();
        if (visitor.LambdaParameters is { } outer)
        {
            foreach (var entry in outer)
                parameters[entry.Key] = entry.Value;
        }

        parameters[first] = filterTuple is null ? IdentifierOr(first, "k") : filterTuple + ".1";
        parameters[second] = filterTuple is null ? IdentifierOr(second, "v") : filterTuple + ".2";

        using var bodyVisitor = new HigherOrderLambdaVisitor(visitor.Options with { DontNeedAlias = false }, parameters);
        var body = RenderLambdaBody(bodyVisitor, lambda.Body);

        var lambdaSql = filterTuple is null
            ? $"({parameters[first]}, {parameters[second]}) -> {body}"
            : $"{filterTuple} -> {body}";

        // The lambda precedes the map in the SQL, so it is rendered first and the captured-value
        // parameter order of the statement stays aligned. The parameter-only pass relies on the same
        // order to mint identical parameter names (see RenderLambdaBody).
        var map = RenderMapOperand(visitor, node.Arguments[1]);

        if (visitor.IsParamMode)
            return;

        // The two predicates accept the map itself and keep their native spelling.
        if (name is nameof(ClickHouseFunctions.map_all) or nameof(ClickHouseFunctions.map_exists))
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(scalars.Render(name, [lambdaSql, map]));
            return;
        }

        var lowered = name == nameof(ClickHouseFunctions.map_apply)
            ? $"arrayMap({lambdaSql}, mapKeys({map}), mapValues({map}))"
            : $"arrayFilter({lambdaSql}, {ZipMapOperand(map)})";

        // Compose the cast before touching the alias flag, for the same no-sticky-flag reason as map_sort.
        var mapSql = CastMapResult(visitor.Dialect, node, name, lowered);

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(mapSql);
    }

    /// <summary>
    /// Wraps a completed <c>map_filter</c>/<c>map_sort</c>/<c>map_apply</c> lowering in the typed cast of
    /// its declared result, so the server returns exactly the CLR collection element type the
    /// declaration promises. The lowering expression itself is emitted unchanged.
    /// </summary>
    private static string CastMapResult(ISqlDialect dialect, MethodCallExpression node, string name, string lowered)
    {
        var element = node.Method.ReturnType.GetElementType()
            ?? throw new NotSupportedException($"The {name} result must be a one-dimensional array (T257).");

        if (name != nameof(ClickHouseFunctions.map_apply)
            && (!TypeFacts.IsTupleLike(element) || TypeFacts.TupleArity(element) != 2))
        {
            throw new NotSupportedException(
                $"The {name} result element {element} must be a key/value tuple (T257).");
        }

        return $"cast({lowered} as {MakeMapResultTypeName(dialect, node.Method.ReturnType, resultPosition: true)})";
    }

    /// <summary>
    /// Composes the type name of a <c>map_*</c> result from its declared CLR type: the structural
    /// wrappers (array, <see cref="Nullable{T}"/>, key/value tuple) are spelled here, while every scalar
    /// leaf is named by the provider's own <see cref="ISqlDialect.MakeTypeName"/>, so no second
    /// CLR-to-provider type table exists. A leaf the provider does not map is refused before any SQL is
    /// emitted.
    /// <para>
    /// <paramref name="resultPosition"/> distinguishes the two halves of a map result. An
    /// <c>arrayMap</c> element is the declared CLR <c>TResult</c>, where a reference type admits null,
    /// while the provider's bare name (ClickHouse <c>String</c>) does not carry it, so the leaf is
    /// spelled <c>Nullable(...)</c>; this is the declared-type mapping only and does not change the
    /// materialized CLR type (<c>string?[]</c> is <c>string[]</c> at runtime). A filter/sort key or
    /// value tuple component keeps the bare name unless the CLR component is
    /// <see cref="Nullable{T}"/>, because the map keys and values themselves are non-nullable.
    /// </para>
    /// </summary>
    private static string MakeMapResultTypeName(ISqlDialect dialect, Type type, bool resultPosition)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return $"Nullable({MakeMapResultTypeName(dialect, underlying, resultPosition: false)})";

        if (type.IsArray && type.GetElementType() is { } element)
            return $"Array({MakeMapResultTypeName(dialect, element, resultPosition)})";

        if (TypeFacts.IsTupleLike(type))
        {
            if (TypeFacts.TupleArity(type) != 2)
                throw new NotSupportedException($"The map result tuple {type} must have exactly two components (T257).");

            var components = type.GetGenericArguments();
            return $"Tuple({MakeMapResultTypeName(dialect, components[0], resultPosition: false)}, {MakeMapResultTypeName(dialect, components[1], resultPosition: false)})";
        }

        var leaf = MakeMapLeafTypeName(dialect, type);

        return resultPosition && !type.IsValueType ? $"Nullable({leaf})" : leaf;
    }

    /// <summary>
    /// Names a scalar result element through the provider mapping. <c>string</c> is the one admitted
    /// reference-type leaf, and the CLR scalars whose own name already is the provider spelling are
    /// identity mappings, indistinguishable from the dialect's generic <c>Type.Name</c> fallback by the
    /// resulting string, so they are admitted by type. A type the provider maps to a name other than its
    /// CLR name (for example <c>float</c> to <c>Float32</c>) is admitted by that differing mapping. Any
    /// other leaf that only reaches the generic fallback is refused.
    /// </summary>
    private static string MakeMapLeafTypeName(ISqlDialect dialect, Type type)
    {
        var name = dialect.MakeTypeName(type);

        if (type == typeof(string) || name != type.Name || IdentityMappedTypes.Contains(type))
            return name;

        throw new NotSupportedException(
            $"The map result element type {type} is not mapped to a ClickHouse type name (T257); the cast would emit an invalid element type.");
    }

    /// <summary>
    /// Renders the map operand of a map helper. An inline <c>map(...)</c> constructor keeps the existing
    /// pair path (a captured or null pair collection is refused there); a captured dictionary is
    /// evaluated once and bound as a single map-valued parameter; anything else is rendered as an
    /// operand, so a map column keeps its column reference. A captured value that is not a dictionary is
    /// refused instead of being emitted as <c>mapKeys(&lt;scalar&gt;)</c>.
    /// </summary>
    private static string RenderMapOperand(BaseExpressionVisitor visitor, Expression expression)
    {
        var operand = TypeFacts.UnwrapConvert(expression);

        if (operand is MethodCallExpression call
            && call.Method.DeclaringType == typeof(ClickHouseFunctions)
            && call.Method.Name == MapConstructorName)
        {
            if (visitor.IsParamMode)
            {
                visitor.Visit(operand);
                return string.Empty;
            }

            return visitor.VisitToString(operand);
        }

        if (!SqlOperandTranslator.IsCapturedArray(operand))
        {
            if (visitor.IsParamMode)
            {
                visitor.Visit(operand);
                return string.Empty;
            }

            return visitor.VisitToString(operand);
        }

        // A captured dictionary is a map value, not a pair collection: it is evaluated exactly once and
        // bound as one parameter (the driver maps Dictionary<K, V> to Map(K, V)).
        var value = InValuesEvaluator.Evaluate(operand, visitor.QueryProvider);

        if (value is null)
            throw new NotSupportedException(
                $"The {TypeFacts.UnwrapConvert(expression).Type.Name} map operand must not be null; the map helpers require a map value or an inline map(...) constructor.");

        if (!IsMapValue(value))
            throw new NotSupportedException(
                $"The {value.GetType().Name} map operand must be a dictionary; the map helpers require a map value or an inline map(...) constructor.");

        var name = visitor.ParameterProvider.GetParamName();
        visitor.TryAddCapturedParameter(name, value, operand);

        return visitor.IsParamMode ? string.Empty : visitor.Dialect.MakeParam(name);
    }

    /// <summary>
    /// True when a captured value is a map: its type implements the generic
    /// <see cref="IDictionary{TKey,TValue}"/> or <see cref="IReadOnlyDictionary{TKey,TValue}"/> contract
    /// (which every <see cref="Dictionary{TKey,TValue}"/> does). A captured value of any other type is
    /// not a map and must not be rendered as one.
    /// </summary>
    private static bool IsMapValue(object value)
    {
        foreach (var contract in value.GetType().GetInterfaces())
        {
            if (!contract.IsGenericType)
                continue;

            var definition = contract.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Renders a higher-order lambda body. A conditional is composed here with the mapping-aware body
    /// visitor: the shared conditional renderer builds its test through a fresh predicate visitor that
    /// does not carry the lambda parameters, which would emit <c>case when ( &gt; 0) ...</c> and fail on
    /// the server. Any conditional in a position this arm cannot compose is refused instead of being
    /// rendered into invalid SQL.
    /// <para>
    /// Invariant: the parameter-collection pass visits exactly what the SQL pass visits, in the same
    /// order (map body first, then the map operand), so both passes mint identical parameter names. It
    /// therefore walks the body on the mapping-aware visitor itself and returns no SQL: cloning is
    /// refused in parameter mode, and this pass has no SQL to produce.
    /// </para>
    /// </summary>
    private static string RenderLambdaBody(BaseExpressionVisitor visitor, Expression body)
    {
        if (visitor.IsParamMode)
        {
            // The fail-closed guard stays in force on this pass: a genuinely unsupported shape must be
            // refused here too, never silently accepted because the pass emits no SQL.
            if (TypeFacts.UnwrapConvert(body) is not ConditionalExpression && body.Has<ConditionalExpression>())
            {
                throw new NotSupportedException(
                    "A conditional inside a higher-order map lambda is supported only as the lambda body or a branch value.");
            }

            visitor.Visit(body);
            return string.Empty;
        }

        if (TypeFacts.UnwrapConvert(body) is ConditionalExpression conditional)
        {
            var test = visitor.VisitToString(conditional.Test);
            var whenTrue = RenderLambdaBody(visitor, conditional.IfTrue);
            var whenFalse = RenderLambdaBody(visitor, conditional.IfFalse);
            var caseSql = $"{visitor.Kw("case when ")}{test}{visitor.Kw(" then ")}{whenTrue}" +
                          $"{visitor.Kw(" else ")}{whenFalse}{visitor.Kw(" end")}";

            return visitor.Dialect.MakeCase(
                caseSql, TypeFacts.IsBoolean(body.Type), visitor.IsPredicateContext, visitor.KeywordCase);
        }

        if (body.Has<ConditionalExpression>())
            throw new NotSupportedException(
                "A conditional inside a higher-order map lambda is supported only as the lambda body or a branch value.");

        return visitor.VisitToString(body);
    }

    /// <summary>Renders the zipped <c>(key, value)</c> array of the map operand.</summary>
    private static string ZipMapOperand(string map) => $"arrayZip(mapKeys({map}), mapValues({map}))";

    /// <summary>
    /// Returns a tuple lambda identifier that does not occur as an identifier in the rendered body or in
    /// the map operand, so the generated <c>.1</c>/<c>.2</c> accesses can never shadow a column, a
    /// parameter or an enclosing lambda parameter.
    /// </summary>
    private static string FreshIdentifier(Expression? body, Expression? operand = null)
    {
        var candidate = TupleIdentifierBase;
        var suffix = 1;

        while (UsesIdentifier(body, candidate) || UsesIdentifier(operand, candidate))
            candidate = TupleIdentifierBase + (++suffix).ToString(CultureInfo.InvariantCulture);

        return candidate;
    }

    private static bool UsesIdentifier(Expression? body, string name)
    {
        if (body is null)
            return false;

        var collector = new IdentifierCollector(name);
        collector.Visit(body);
        return collector.Found;
    }

    /// <summary>Records whether an identifier (a parameter or a member name) occurs in an expression.</summary>
    private sealed class IdentifierCollector(string name) : ExpressionVisitor
    {
        internal bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name == name)
                Found = true;

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.Name == name)
                Found = true;

            return base.VisitMember(node);
        }
    }

    private static string IdentifierOr(ParameterExpression parameter, string fallback)
        => string.IsNullOrEmpty(parameter.Name) ? fallback : parameter.Name!;


    /// <summary>Renders <c>sumMapFiltered(keys)(key, value)</c> with double parentheses.</summary>
    private static void EmitSumMapFiltered(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var keys = Capture(builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, node.Arguments[0]));
        var key = visitor.VisitToString(node.Arguments[1]);
        var value = visitor.VisitToString(node.Arguments[2]);
        builder.Append(scalars.Render(name, [keys, key, value]));
    }

    /// <summary>Renders <paramref name="emit"/> into the current builder and returns the emitted fragment.</summary>
    private static string Capture(System.Text.StringBuilder builder, Action emit)
    {
        var start = builder.Length;
        emit();
        var value = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        return value;
    }

    /// <summary>
    /// Flattens the compiler-generated <c>params</c> array of an array function into its element
    /// expressions. A non-inline (captured) argument array is rejected.
    /// </summary>
    private static IReadOnlyList<Expression> FlattenParams(IReadOnlyList<Expression> args)
    {
        if (args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions })
            return expressions;

        throw new NotSupportedException("The array arguments must be inline expressions, not a captured array.");
    }

    /// <summary>
    /// Flattens the compiler-generated <c>params</c> tuple array of <c>map</c> into its key/value
    /// elements. Every pair is an inline <see cref="Tuple{T1,T2}"/> construction (<c>Tuple.Create</c>);
    /// the declaring type is matched structurally, so an unrelated <c>Create</c> overload cannot be
    /// mistaken for a pair.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Expression>> FlattenTuplePairs(IReadOnlyList<Expression> args)
    {
        if (args.Count != 1 || args[0] is not NewArrayExpression { Expressions: var pairs })
            throw new NotSupportedException("The map key/value pairs must be inline tuples, not a captured array.");

        var result = new List<IReadOnlyList<Expression>>(pairs.Count);
        for (var (i, cnt) = (0, pairs.Count); i < cnt; i++)
        {
            result.Add(pairs[i] switch
            {
                MethodCallExpression pair
                    when pair.Method.DeclaringType == typeof(Tuple) && pair.Arguments.Count == 2 => pair.Arguments,
                _ => throw new NotSupportedException("The map key/value pairs must be inline tuples, not a captured array.")
            });
        }

        return result;
    }

    /// <summary>Drops trailing <c>null</c> literals, the default of the optional arguments.</summary>
    private static IReadOnlyList<Expression> DropTrailingNulls(IReadOnlyList<Expression> args)
    {
        var last = args.Count;
        while (last > 0 && args[last - 1] is ConstantExpression { Value: null })
            last--;

        if (last == args.Count)
            return args;

        var result = new Expression[last];
        for (var i = 0; i < last; i++)
            result[i] = args[i];

        return result;
    }

    private static LambdaExpression ExtractLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
            return lambda;

        throw new NotSupportedException("The map higher-order functions require an inline lambda argument.");
    }
}
