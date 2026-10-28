using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the array surface written through <see cref="CommonFunctions"/>: the PostgreSQL
/// <c>any</c>/<c>all</c> quantifiers over an array and the common array functions and operators, plus
/// the ClickHouse array functions over array columns (<see cref="ClickHouseFunctions"/>).
/// <para>
/// An array operand is always bound as a single parameter (the whole array), never expanded into a
/// value list, so the SQL text does not depend on the number of elements and the plan stays
/// cacheable. A runtime array is supplied either through <see cref="SqlFunctions.Parameter{T}(int)"/> or as a
/// captured array; see <see cref="SqlOperandTranslator"/> for the operand rendering.
/// </para>
/// <para>
/// The PostgreSQL surface requires a dialect that opts in with <see cref="ISqlDialect.SupportsArrays"/>
/// (PostgreSQL); the ClickHouse array functions require <see cref="ISqlDialect.SupportsArrayFunctions"/>
/// and <c>arrayJoin</c> additionally requires <see cref="ISqlDialect.SupportsArrayJoin"/>. Every other
/// provider rejects them with a clear message.
/// </para>
/// </summary>
internal static class ArraySqlTranslator
{
    /// <summary>
    /// Translates <c>SqlFunctions.Sql.any(...)</c>/<c>SqlFunctions.Sql.all(...)</c> when the operand is an array,
    /// in both the value form (<c>any(array)</c>) and the predicate form
    /// (<c>any(column, array)</c>). Returns <c>false</c> for the subquery form, which is handled by
    /// <see cref="NormSqlTranslator"/>.
    /// </summary>
    internal static bool TryTranslateAnyAll(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var keyword = node.Method.Name switch
        {
            nameof(CommonFunctions.any) => "any",
            nameof(CommonFunctions.all) => "all",
            _ => null
        };

        if (keyword is null)
            return false;

        var args = node.Arguments;

        // any(column, array): a complete predicate rendering `column = any(@array)`.
        if (args.Count == 2 && SqlOperandTranslator.IsArray(args[1]))
        {
            RequireArraySupport(visitor);

            if (!visitor.IsParamMode)
                visitor.Builder!.Append('(');

            SqlOperandTranslator.AppendArgument(visitor, args[0]);

            if (!visitor.IsParamMode)
                visitor.Builder!.Append(" = ").Append(keyword).Append('(');

            SqlOperandTranslator.AppendArrayOperand(visitor, args[1]);

            if (!visitor.IsParamMode)
                visitor.Builder!.Append("))");

            return true;
        }

        // any(array): the quantifier as a value, used on the right of a comparison.
        if (args.Count == 1 && SqlOperandTranslator.IsArray(args[0]))
        {
            RequireArraySupport(visitor);

            if (visitor.IsParamMode)
            {
                SqlOperandTranslator.AppendArrayOperand(visitor, args[0]);
                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(keyword).Append('(');
            SqlOperandTranslator.AppendArrayOperand(visitor, args[0]);
            visitor.Builder!.Append(')');

            return true;
        }

        return false;
    }

    /// <summary>
    /// Translates the array functions and operators of <see cref="CommonFunctions"/> (for example
    /// <c>cardinality</c>, <c>array_length</c>, <c>array_position</c> and the <c>@&gt;</c>/<c>&amp;&amp;</c>
    /// operators). Returns <c>false</c> when the call is not an array function.
    /// </summary>
    internal static bool TryTranslateFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;

        if (node.Method.DeclaringType == typeof(ClickHouseFunctions))
            return TryTranslateClickHouseArray(visitor, node, args);

        switch (node.Method.Name)
        {
            case nameof(PostgresFunctions.cardinality) when args.Count == 1:
                EmitFunction(visitor, "cardinality", args);
                return true;
            case nameof(PostgresFunctions.array_length) when args.Count == 2:
                EmitFunction(visitor, "array_length", args);
                return true;
            case nameof(PostgresFunctions.array_ndims) when args.Count == 1:
                EmitFunction(visitor, "array_ndims", args);
                return true;
            case nameof(PostgresFunctions.array_lower) when args.Count == 2:
                EmitFunction(visitor, "array_lower", args);
                return true;
            case nameof(PostgresFunctions.array_upper) when args.Count == 2:
                EmitFunction(visitor, "array_upper", args);
                return true;
            case nameof(PostgresFunctions.array_position) when args.Count == 2:
                EmitFunction(visitor, "array_position", args);
                return true;
            case nameof(PostgresFunctions.array_to_string) when args.Count == 2:
                EmitFunction(visitor, "array_to_string", args);
                return true;
            case nameof(PostgresFunctions.array_append) when args.Count == 2:
                EmitFunction(visitor, "array_append", args);
                return true;
            case nameof(PostgresFunctions.array_prepend) when args.Count == 2:
                EmitFunction(visitor, "array_prepend", args);
                return true;
            case nameof(PostgresFunctions.array_cat) when args.Count == 2:
                EmitFunction(visitor, "array_cat", args);
                return true;
            case nameof(PostgresFunctions.array_remove) when args.Count == 2:
                EmitFunction(visitor, "array_remove", args);
                return true;
            case nameof(PostgresFunctions.array_replace) when args.Count == 3:
                EmitFunction(visitor, "array_replace", args);
                return true;
            case nameof(PostgresFunctions.array_fill) when args.Count == 2:
                EmitFunction(visitor, "array_fill", args);
                return true;
            case nameof(PostgresFunctions.array_dims) when args.Count == 1:
                EmitFunction(visitor, "array_dims", args);
                return true;
            case nameof(PostgresFunctions.array_positions) when args.Count == 2:
                EmitFunction(visitor, "array_positions", args);
                return true;
            case nameof(PostgresFunctions.array_reverse) when args.Count == 1:
                EmitGuardedFunction(visitor, "array_reverse", args);
                return true;
            case nameof(PostgresFunctions.array_sort) when args.Count == 1:
                EmitGuardedFunction(visitor, "array_sort", args);
                return true;
            case nameof(PostgresFunctions.array_shuffle) when args.Count == 1:
                EmitFunction(visitor, "array_shuffle", args);
                return true;
            case nameof(PostgresFunctions.array_sample) when args.Count == 2:
                EmitFunction(visitor, "array_sample", args);
                return true;
            case nameof(PostgresFunctions.trim_array) when args.Count == 2:
                EmitFunction(visitor, "trim_array", args);
                return true;
            case nameof(PostgresFunctions.string_to_array) when args.Count == 2:
                EmitFunction(visitor, "string_to_array", args);
                return true;
            case nameof(PostgresFunctions.array_contains) when args.Count == 2:
                EmitOperator(visitor, "@>", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.array_contained_by) when args.Count == 2:
                EmitOperator(visitor, "<@", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.array_overlaps) when args.Count == 2:
                EmitOperator(visitor, "&&", args[0], args[1]);
                return true;
            case nameof(PostgresFunctions.array_concat) when args.Count == 2:
                EmitOperator(visitor, "||", args[0], args[1]);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Translates the ClickHouse array functions of <see cref="ClickHouseFunctions"/> over array
    /// columns (for example <c>length</c>, <c>has</c>, <c>indexOf</c> and <c>arrayJoin</c>). Returns
    /// <c>false</c> when the call is not one of them.
    /// </summary>
    private static bool TryTranslateClickHouseArray(BaseExpressionVisitor visitor, MethodCallExpression node, IReadOnlyList<Expression> args)
    {
        if (TryTranslateHigherOrderArray(visitor, node, args))
            return true;

        if (TryTranslateArrayFamilyArm(visitor, node, args))
            return true;

        string sqlName;
        var isArrayJoin = false;

        switch (node.Method.Name)
        {
            case nameof(ClickHouseFunctions.array_join) when args.Count == 1:
                sqlName = "arrayJoin";
                isArrayJoin = true;
                break;
            case nameof(ClickHouseFunctions.length) when args.Count == 1:
                sqlName = "length";
                break;
            case nameof(ClickHouseFunctions.has) when args.Count == 2:
                sqlName = "has";
                break;
            case nameof(ClickHouseFunctions.index_of) when args.Count == 2:
                sqlName = "indexOf";
                break;
            case nameof(ClickHouseFunctions.has_any) when args.Count == 2:
                sqlName = "hasAny";
                break;
            case nameof(ClickHouseFunctions.has_all) when args.Count == 2:
                sqlName = "hasAll";
                break;
            case nameof(ClickHouseFunctions.starts_with) when args.Count == 2:
                sqlName = "startsWith";
                break;
            case nameof(ClickHouseFunctions.ends_with) when args.Count == 2:
                sqlName = "endsWith";
                break;
            case nameof(ClickHouseFunctions.has_substr) when args.Count == 2:
                sqlName = "hasSubstr";
                break;
            case nameof(ClickHouseFunctions.array_string_concat) when args.Count == 2:
                sqlName = "arrayStringConcat";

                // The default (null) delimiter means ClickHouse's own default (the empty string):
                // omit the argument rather than rendering an explicit null.
                if (args[1] is ConstantExpression { Value: null })
                    args = [args[0]];

                break;
            case nameof(ClickHouseFunctions.split_by_char) when args.Count == 2:
                sqlName = "splitByChar";
                break;
            case nameof(ClickHouseFunctions.array_sort) when args.Count == 1:
                sqlName = "arraySort";
                break;
            case nameof(ClickHouseFunctions.array_reverse) when args.Count == 1:
                sqlName = "arrayReverse";
                break;
            case nameof(ClickHouseFunctions.array_distinct) when args.Count == 1:
                sqlName = "arrayDistinct";
                break;
            case nameof(ClickHouseFunctions.range) when args.Count is 1 or 2 or 3:
                sqlName = "range";
                break;
            case nameof(ClickHouseFunctions.array_enumerate) when args.Count == 1:
                sqlName = "arrayEnumerate";
                break;
            case nameof(ClickHouseFunctions.array_cum_sum) when args.Count == 1:
                sqlName = "arrayCumSum";
                break;
            case nameof(ClickHouseFunctions.array_cum_sum_non_negative) when args.Count == 1:
                sqlName = "arrayCumSumNonNegative";
                break;
            case nameof(ClickHouseFunctions.array_slice) when args.Count is 2 or 3:
                sqlName = "arraySlice";
                break;
            case nameof(ClickHouseFunctions.array_push_back) when args.Count == 2:
                sqlName = "arrayPushBack";
                break;
            default:
                return false;
        }

        if (isArrayJoin)
            RequireArrayJoin(visitor);
        else
            RequireArrayFunctions(visitor);

        EmitArrayFunction(visitor, sqlName, node.Method.Name, node.Method.ReturnType, args);
        return true;
    }

    /// <summary>
    /// Translates the F05 array family (issue #286): the element/aggregate/set helpers, the
    /// structure-preserving helpers and the version-gap members. Returns <c>false</c> when the call
    /// is not one of them. Every arm is gated by <see cref="ISqlDialect.SupportsArrayFunctions"/>
    /// (ClickHouse), and the two CLR-shape guards run before that gate so their precise reason wins on
    /// every provider.
    /// </summary>
    private static bool TryTranslateArrayFamilyArm(
        BaseExpressionVisitor visitor, MethodCallExpression node, IReadOnlyList<Expression> args)
    {
        var name = node.Method.Name;

        if (name == nameof(ClickHouseFunctions.array_fold) && args.Count == 3)
        {
            RequireArrayFunctions(visitor);
            EmitArrayFold(visitor, node, args);
            return true;
        }

        // A declared long/long[] result can carry only integral arithmetic: the vendor's floating-point
        // and decimal aggregation shapes are refused instead of being truncated into it.
        if (name == nameof(ClickHouseFunctions.array_difference))
            RequireIntegralElement(node, name, allowUnsigned64: true);
        else if (name == nameof(ClickHouseFunctions.array_sum))
            RequireIntegralElement(node, name, allowUnsigned64: false);
        else if (name == nameof(ClickHouseFunctions.array_cum_sum_non_negative))
            RequireIntegralElement(node, name, allowUnsigned64: false);

        // The native names are absent from the pinned 25.8 server; each arm renders the vendor
        // definition from functions the pinned server has, so the result is version-independent.
        if (name is nameof(ClickHouseFunctions.array_top_k) or nameof(ClickHouseFunctions.array_bottom_k)
            or nameof(ClickHouseFunctions.array_remove) or nameof(ClickHouseFunctions.array_transpose))
        {
            RequireArrayFunctions(visitor);
            EmitVersionGapArm(visitor, node, name);
            return true;
        }

        var sqlName = name switch
        {
            nameof(ClickHouseFunctions.array_element) when args.Count == 2 => "arrayElement",
            nameof(ClickHouseFunctions.array_enumerate_dense) when args.Count == 1 => "arrayEnumerateDense",
            nameof(ClickHouseFunctions.array_zip) when args.Count == 2 => "arrayZip",
            nameof(ClickHouseFunctions.array_difference) when args.Count == 1 => "arrayDifference",
            nameof(ClickHouseFunctions.array_avg) when args.Count == 1 => "arrayAvg",
            nameof(ClickHouseFunctions.array_sum) when args.Count == 1 => "arraySum",
            nameof(ClickHouseFunctions.array_min) when args.Count == 1 => "arrayMin",
            nameof(ClickHouseFunctions.array_max) when args.Count == 1 => "arrayMax",
            nameof(ClickHouseFunctions.array_product) when args.Count == 1 => "arrayProduct",
            nameof(ClickHouseFunctions.array_random_sample) when args.Count == 2 => "arrayRandomSample",
            nameof(ClickHouseFunctions.array_compact) when args.Count == 1 => "arrayCompact",
            nameof(ClickHouseFunctions.array_pop_back) when args.Count == 1 => "arrayPopBack",
            nameof(ClickHouseFunctions.array_pop_front) when args.Count == 1 => "arrayPopFront",
            nameof(ClickHouseFunctions.array_push_front) when args.Count == 2 => "arrayPushFront",
            nameof(ClickHouseFunctions.array_resize) when args.Count is 2 or 3 => "arrayResize",
            nameof(ClickHouseFunctions.array_rotate_left) when args.Count == 2 => "arrayRotateLeft",
            nameof(ClickHouseFunctions.array_rotate_right) when args.Count == 2 => "arrayRotateRight",
            nameof(ClickHouseFunctions.array_shift_left) when args.Count is 2 or 3 => "arrayShiftLeft",
            nameof(ClickHouseFunctions.array_shift_right) when args.Count is 2 or 3 => "arrayShiftRight",
            nameof(ClickHouseFunctions.array_reverse_sort) when args.Count == 1 => "arrayReverseSort",
            nameof(ClickHouseFunctions.array_shingles) when args.Count == 2 => "arrayShingles",
            nameof(ClickHouseFunctions.count_equal) when args.Count == 2 => "countEqual",
            nameof(ClickHouseFunctions.array_with_constant) when args.Count == 2 => "arrayWithConstant",
            nameof(ClickHouseFunctions.array_jaccard_index) when args.Count == 2 => "arrayJaccardIndex",
            nameof(ClickHouseFunctions.array_similarity) when args.Count == 4 => "arraySimilarity",
            _ => null
        };

        if (sqlName is not null)
        {
            RequireArrayFunctions(visitor);
            EmitArrayFunction(visitor, sqlName, name, node.Method.ReturnType, args);
            return true;
        }

        if (name == nameof(ClickHouseFunctions.array_partial_sort) && args.Count == 2)
        {
            RequireArrayFunctions(visitor);
            EmitArrayExpression(visitor, node, rendered => $"arrayPartialSort({rendered[1]}, {rendered[0]})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Renders the members whose native name arrived after the pinned ClickHouse 25.8 server. Each
    /// definition is the contract the vendor documents for that name, composed only of functions the
    /// pinned server has, so the member is version-independent instead of a capability refusal; the
    /// live suite asserts the server's <c>UNKNOWN_FUNCTION</c> for the native name itself.
    /// </summary>
    private static void EmitVersionGapArm(
        BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        switch (name)
        {
            case nameof(ClickHouseFunctions.array_top_k):
                EmitArrayExpression(visitor, node, rendered =>
                {
                    var x = FreshLambdaName("_x", rendered);
                    return $"arraySlice(arrayReverseSort(arrayFilter({x} -> isNotNull({x}), {rendered[0]})), 1, {rendered[1]})";
                });
                return;

            case nameof(ClickHouseFunctions.array_bottom_k):
                EmitArrayExpression(visitor, node, rendered =>
                {
                    var x = FreshLambdaName("_x", rendered);
                    return $"arraySlice(arraySort(arrayFilter({x} -> isNotNull({x}), {rendered[0]})), 1, {rendered[1]})";
                });
                return;

            // NULLs are equal to NULL, which a plain `x != element` cannot express (its NULL result
            // would keep them); coalesce folds the null-against-null case into "equal".
            case nameof(ClickHouseFunctions.array_remove):
                EmitArrayExpression(visitor, node, rendered =>
                {
                    var x = FreshLambdaName("_x", rendered);
                    var element = rendered[1];
                    return $"arrayFilter({x} -> not coalesce({x} = {element}, isNull({x}) AND isNull({element})), {rendered[0]})";
                });
                return;

            case nameof(ClickHouseFunctions.array_transpose):
                EmitArrayExpression(visitor, node, rendered =>
                {
                    var index = FreshLambdaName("_i", rendered);
                    var row = FreshLambdaName("_r", [rendered[0], index]);
                    return $"arrayMap({index} -> arrayMap({row} -> {row}[{index}], ({rendered[0]})), "
                        + $"range(1, length(({rendered[0]})[1]) + 1))";
                });
                return;
        }
    }

    /// <summary>
    /// Renders an array-family call whose SQL the callback composes from the rendered operands. Every
    /// operand goes through <see cref="SqlOperandTranslator.AppendArrayOrColumn"/>, so a captured or
    /// inline array is bound as one parameter, and the parameter-only pass visits the operands in the
    /// same order so the minted parameter names stay aligned.
    /// </summary>
    private static void EmitArrayExpression(
        BaseExpressionVisitor visitor, MethodCallExpression node, Func<IReadOnlyList<string>, string> compose)
    {
        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var rendered = new string[args.Count];
        var captured = new SqlParameterTemplateRecorder?[args.Count];

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            rendered[i] = Capture(
                visitor, builder, () => SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]), out captured[i]);
        }

        var call = compose(rendered);
        var processed = visitor.Dialect.MakeArrayFunction(node.Method.Name, call);
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            visitor.Recorder,
            visitor.Dialect.MakeArrayResult(node.Method.Name, processed, node.Method.ReturnType),
            Fragments(call, rendered, captured));
    }

    /// <summary>
    /// Orders the captured operands by where their text actually appears in the composed call, so a
    /// composition that reorders its operands (<c>array_partial_sort</c>) still relocates every recorded
    /// occurrence at its real offset inside the emitted SQL.
    /// </summary>
    private static IReadOnlyList<SqlParameterTrackedFragment> Fragments(
        string text, IReadOnlyList<string> rendered, IReadOnlyList<SqlParameterTemplateRecorder?> captures)
    {
        var fragments = new List<SqlParameterTrackedFragment>(captures.Count);
        for (var i = 0; i < captures.Count; i++)
        {
            if (captures[i] is { OccurrenceCount: > 0 } fragment)
                fragments.Add(new SqlParameterTrackedFragment(rendered[i], null, fragment));
        }

        if (fragments.Count > 1)
        {
            fragments.Sort((left, right) => text.IndexOf(left.AppendedText, StringComparison.Ordinal)
                .CompareTo(text.IndexOf(right.AppendedText, StringComparison.Ordinal)));
        }

        return fragments;
    }

    /// <summary>
    /// Renders <c>arrayFold((acc, x) -&gt; body, array, accumulator)</c>: the fold lambda has two
    /// parameters (the accumulator and the element), and the body may reference the enclosing query's
    /// columns and captured parameters. The accumulator follows the array in SQL order, and the
    /// parameter-only pass visits exactly the same nodes in the same order.
    /// </summary>
    private static void EmitArrayFold(
        BaseExpressionVisitor visitor, MethodCallExpression node, IReadOnlyList<Expression> args)
    {
        var lambda = ExtractLambda(args[0], nameof(ClickHouseFunctions.array_fold));

        if (lambda.Parameters.Count != 2)
            throw new NotSupportedException(
                "The array_fold lambda must have exactly two parameters (accumulator, element).");

        var accumulator = NameOr(lambda.Parameters[0], "acc");
        var element = NameOr(lambda.Parameters[1], "x");

        var parameters = new Dictionary<ParameterExpression, string>();
        if (visitor.LambdaParameters is { } outer)
        {
            foreach (var entry in outer)
                parameters[entry.Key] = entry.Value;
        }

        parameters[lambda.Parameters[0]] = accumulator;
        parameters[lambda.Parameters[1]] = element;

        using var bodyVisitor = new HigherOrderLambdaVisitor(visitor.Options with { DontNeedAlias = false }, parameters);
        bodyVisitor.Visit(lambda.Body);

        if (visitor.IsParamMode)
        {
            SqlOperandTranslator.AppendArrayOrColumn(visitor, args[1]);
            SqlOperandTranslator.AppendArrayOrColumn(visitor, args[2]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        var recorder = visitor.Recorder;
        var start = builder.Length;
        var mark = recorder?.MarkAt(start);
        builder.Append("arrayFold((").Append(accumulator).Append(", ").Append(element).Append(") -> ");
        bodyVisitor.WriteTo(builder, recorder);
        builder.Append(", ");
        SqlOperandTranslator.AppendArrayOrColumn(visitor, args[1]);
        builder.Append(", cast(");

        // The accumulator is cast to its declared CLR type: a small integer literal (0) is inferred
        // as UInt8 by the server, while the lambda's arithmetic widens to Int64, and the fold requires
        // the two to agree (TYPE_MISMATCH otherwise).
        SqlOperandTranslator.AppendArrayOrColumn(visitor, args[2]);
        builder.Append(" as ").Append(DeclaredTypeName(visitor.Dialect, node.Method.ReturnType)).Append(')');
        builder.Append(')');

        var call = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        var fragment = mark is { } checkpoint ? recorder!.RewindTo(checkpoint) : null;
        var fragments = fragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(call, null, fragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        var processed = visitor.Dialect.MakeArrayFunction(node.Method.Name, call);
        SqlParameterFragmentRelocation.AppendComposed(
            builder,
            recorder,
            visitor.Dialect.MakeArrayResult(node.Method.Name, processed, node.Method.ReturnType),
            fragments);
    }

    /// <summary>
    /// Refuses an element type whose vendor aggregation widens to a shape the declared integral CLR
    /// result cannot carry (a floating-point or decimal difference/sum would be silently truncated).
    /// The check runs before the provider gate, so the precise reason is reported on every provider.
    /// </summary>
    private static void RequireIntegralElement(
        MethodCallExpression node, string name, bool allowUnsigned64)
    {
        var element = node.Method.GetGenericArguments()[0];
        element = Nullable.GetUnderlyingType(element) ?? element;

        var integral = element == typeof(sbyte) || element == typeof(byte)
            || element == typeof(short) || element == typeof(ushort)
            || element == typeof(int) || element == typeof(uint)
            || element == typeof(long) || (allowUnsigned64 && element == typeof(ulong));

        if (!integral)
            throw new NotSupportedException(
                $"The {name} function is refused for element type {element}: the vendor widens the result "
                + (name == nameof(ClickHouseFunctions.array_difference)
                    ? "to a floating-point or decimal array whose fractional differences the declared long[] cannot carry; "
                    : "to a floating-point or decimal sum that the declared long cannot carry; ")
                + "only integral element types are accepted.");
    }

    /// <summary>
    /// A lambda identifier that no rendered operand already uses, so the generated parameter cannot
    /// shadow an enclosing lambda parameter (or a column) the operand refers to.
    /// </summary>
    private static string FreshLambdaName(string baseName, IReadOnlyList<string> rendered)
    {
        var candidate = baseName;
        var suffix = 1;

        while (rendered.Any(sql => UsesIdentifier(sql, candidate)))
            candidate = baseName + (++suffix).ToString(CultureInfo.InvariantCulture);

        return candidate;
    }

    private static bool UsesIdentifier(string sql, string name)
    {
        for (var index = sql.IndexOf(name, StringComparison.Ordinal); index >= 0;
             index = sql.IndexOf(name, index + name.Length, StringComparison.Ordinal))
        {
            var beforeOk = index == 0 || !IsIdentifierChar(sql[index - 1]);
            var after = index + name.Length;
            var afterOk = after == sql.Length || !IsIdentifierChar(sql[after]);

            if (beforeOk && afterOk)
                return true;
        }

        return false;
    }

    private static bool IsIdentifierChar(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

    /// <summary>
    /// The provider type name of a declared scalar result, used to cast a fold accumulator into the
    /// type its lambda is declared over. Arrays and tuples have no scalar accumulator spelling and are
    /// refused with their own reason.
    /// </summary>
    private static string DeclaredTypeName(ISqlDialect dialect, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return $"Nullable({DeclaredTypeName(dialect, underlying)})";

        if (type.IsArray || TypeFacts.IsTupleLike(type))
            throw new NotSupportedException(
                $"The array_fold accumulator type {type} is refused: the accumulator must be a scalar "
                + "(or Nullable(scalar)) value so its provider type name can be composed; arrays and "
                + "tuples have no scalar accumulator spelling.");

        return dialect.MakeTypeName(type);
    }

    private static string NameOr(ParameterExpression parameter, string fallback)
        => string.IsNullOrEmpty(parameter.Name) ? fallback : parameter.Name!;

    /// <summary>
    /// Renders an operand into the builder, rewinds both the builder and the parameter recorder to the
    /// pre-render position, and returns the emitted text with a detached recorder that owns its
    /// occurrences rebased to zero. The text reaches the builder only through
    /// <see cref="SqlParameterFragmentRelocation.AppendComposed"/>, which relocates those occurrences to
    /// their real offsets inside the composed wrapper.
    /// </summary>
    private static string Capture(
        BaseExpressionVisitor visitor,
        System.Text.StringBuilder builder,
        Action emit,
        out SqlParameterTemplateRecorder? fragment)
    {
        var recorder = visitor.Recorder;
        var start = builder.Length;
        var mark = recorder?.MarkAt(start);
        emit();
        var value = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        fragment = mark is { } checkpoint ? recorder!.RewindTo(checkpoint) : null;
        return value;
    }

    /// <summary>
    /// Translates the higher-order (lambda) array functions of <see cref="ClickHouseFunctions"/>
    /// (<c>arrayMap</c>, <c>arrayFilter</c>, <c>arrayExists</c>, <c>arrayAll</c>, <c>arrayCount</c>,
    /// <c>arrayFirst*</c>, <c>arrayLast*</c>). Returns <c>false</c> when the call is not one of them.
    /// </summary>
    private static bool TryTranslateHigherOrderArray(BaseExpressionVisitor visitor, MethodCallExpression node, IReadOnlyList<Expression> args)
    {
        var sqlName = node.Method.Name switch
        {
            nameof(ClickHouseFunctions.array_map) when args.Count >= 2 => "arrayMap",
            nameof(ClickHouseFunctions.array_filter) when args.Count >= 2 => "arrayFilter",
            nameof(ClickHouseFunctions.array_exists) when args.Count == 2 => "arrayExists",
            nameof(ClickHouseFunctions.array_all) when args.Count == 2 => "arrayAll",
            nameof(ClickHouseFunctions.array_count) when args.Count == 2 => "arrayCount",
            nameof(ClickHouseFunctions.array_first) when args.Count == 2 => "arrayFirst",
            nameof(ClickHouseFunctions.array_first_index) when args.Count == 2 => "arrayFirstIndex",
            nameof(ClickHouseFunctions.array_last) when args.Count == 2 => "arrayLast",
            nameof(ClickHouseFunctions.array_last_index) when args.Count == 2 => "arrayLastIndex",
            nameof(ClickHouseFunctions.array_fill) when args.Count == 2 => "arrayFill",
            nameof(ClickHouseFunctions.array_reverse_fill) when args.Count == 2 => "arrayReverseFill",
            nameof(ClickHouseFunctions.array_split) when args.Count == 2 => "arraySplit",
            nameof(ClickHouseFunctions.array_reverse_split) when args.Count == 2 => "arrayReverseSplit",
            _ => null
        };

        if (sqlName is null)
            return false;

        if (!visitor.Dialect.SupportsHigherOrderArrayFunctions)
            throw new NotSupportedException(
                "The higher-order array functions (arrayMap/arrayFilter/arrayExists/arrayAll/arrayCount/arrayFirst*/arrayLast*) require a provider that supports them (ClickHouse).");

        EmitHigherOrderArray(visitor, sqlName, node.Method.Name, node.Method.ReturnType, args);
        return true;
    }

    /// <summary>
    /// Renders <c>arrayMap(lambda, arr1, ...)</c>-style calls. ClickHouse binds one lambda parameter per
    /// array operand (never a separate index argument), so the lambda arity must equal the number of
    /// arrays; the documented index-lambda idiom passes <c>arrayEnumerate(array)</c> as an extra array.
    /// Any other arity is refused with a precise reason instead of silently dropping a parameter.
    /// </summary>
    private static void EmitHigherOrderArray(
        BaseExpressionVisitor visitor,
        string sqlName,
        string facadeName,
        Type resultType,
        IReadOnlyList<Expression> args)
    {
        var lambda = ExtractLambda(args[0], facadeName);
        var arrayCount = args.Count - 1;

        if (lambda.Parameters.Count != arrayCount)
            throw new NotSupportedException(
                $"The {facadeName} lambda must have exactly one parameter per array argument: "
                + $"{lambda.Parameters.Count} parameter(s) for {arrayCount} array(s). ClickHouse has no "
                + "separate index lambda parameter; pass arrayEnumerate(array) as an additional array "
                + "to bind an index.");

        var parameters = new Dictionary<ParameterExpression, string>();
        if (visitor.LambdaParameters is { } outer)
        {
            foreach (var entry in outer)
                parameters[entry.Key] = entry.Value;
        }

        var lambdaParameters = new string[lambda.Parameters.Count];
        for (var i = 0; i < lambda.Parameters.Count; i++)
        {
            var name = NameOr(lambda.Parameters[i], FallbackLambdaName(i));
            parameters[lambda.Parameters[i]] = name;
            lambdaParameters[i] = name;
        }

        using var bodyVisitor = new HigherOrderLambdaVisitor(visitor.Options with { DontNeedAlias = false }, parameters);
        bodyVisitor.Visit(lambda.Body);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (1, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var builder = visitor.Builder!;
        var recorder = visitor.Recorder;
        var start = builder.Length;
        var mark = recorder?.MarkAt(start);
        builder.Append(sqlName).Append('(');

        if (lambdaParameters.Length == 1)
            builder.Append(lambdaParameters[0]);
        else
            builder.Append('(').AppendJoin(", ", lambdaParameters).Append(')');

        builder.Append(" -> ");
        bodyVisitor.WriteTo(builder, recorder);

        for (var (i, cnt) = (1, args.Count); i < cnt; i++)
        {
            builder.Append(", ");
            SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);
        }

        builder.Append(')');

        var call = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        var fragment = mark is { } checkpoint ? recorder!.RewindTo(checkpoint) : null;
        var fragments = fragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(call, null, fragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        var processed = visitor.Dialect.MakeArrayFunction(sqlName, call);
        SqlParameterFragmentRelocation.AppendComposed(
            builder, recorder, visitor.Dialect.MakeArrayResult(facadeName, processed, resultType), fragments);
    }

    /// <summary>The generated lambda identifier when the expression tree carries no parameter name.</summary>
    private static string FallbackLambdaName(int index) => index switch
    {
        0 => "x",
        1 => "y",
        2 => "z",
        _ => "x" + index.ToString(CultureInfo.InvariantCulture)
    };

    private static LambdaExpression ExtractLambda(Expression expression, string facadeName)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
            return lambda;

        throw new NotSupportedException(
            $"The {facadeName} function requires an inline lambda argument: a lambda held in a variable "
            + "or a delegate is not part of the query expression tree, so its body cannot be translated.");
    }

    private static void EmitArrayFunction(
        BaseExpressionVisitor visitor,
        string sqlName,
        string facadeName,
        Type resultType,
        IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var builder = visitor.Builder!;
        var recorder = visitor.Recorder;
        var start = builder.Length;
        var mark = recorder?.MarkAt(start);
        builder.Append(sqlName).Append('(');

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (i > 0)
                builder.Append(", ");

            SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);
        }

        builder.Append(')');

        var call = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        var fragment = mark is { } checkpoint ? recorder!.RewindTo(checkpoint) : null;
        var fragments = fragment is { OccurrenceCount: > 0 }
            ? new[] { new SqlParameterTrackedFragment(call, null, fragment) }
            : Array.Empty<SqlParameterTrackedFragment>();
        var processed = visitor.Dialect.MakeArrayFunction(sqlName, call);
        SqlParameterFragmentRelocation.AppendComposed(
            builder, recorder, visitor.Dialect.MakeArrayResult(facadeName, processed, resultType), fragments);
    }

    private static void RequireArrayFunctions(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsArrayFunctions)
            throw new NotSupportedException(
                "The array functions require a provider with a native array type that supports them (ClickHouse).");
    }

    private static void RequireArrayJoin(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsArrayJoin)
            throw new NotSupportedException(
                "arrayJoin requires a provider that supports it (ClickHouse).");
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireArraySupport(visitor);
        SqlOperandTranslator.EmitFunction(visitor, sqlName, args);
    }

    /// <summary>
    /// Renders an array function that is additionally gated per name by
    /// <see cref="ISqlDialect.SupportsArrayFunction(string)"/>, so a missing or version-gated server
    /// function is refused at preparation, before its arguments are visited or its name is emitted.
    /// </summary>
    private static void EmitGuardedFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        RequireArraySupport(visitor);

        if (!visitor.Dialect.SupportsArrayFunction(sqlName))
            throw new NotSupportedException($"The PostgreSQL function '{sqlName}' is not supported by this provider.");

        SqlOperandTranslator.EmitFunction(visitor, sqlName, args);
    }

    private static void EmitOperator(BaseExpressionVisitor visitor, string sqlOperator, Expression left, Expression right)
    {
        RequireArraySupport(visitor);
        SqlOperandTranslator.EmitOperator(visitor, sqlOperator, left, right);
    }

    private static void RequireArraySupport(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsArrays)
            throw new NotSupportedException(
                "Arrays are not supported by this provider: array parameters, the any/all array quantifiers and the array functions require PostgreSQL.");
    }
}

/// <summary>
/// Renders the body of a higher-order array lambda (<c>arrayMap(x -&gt; x + 2, ...)</c>): the lambda's
/// parameter is emitted as a bare SQL identifier, every other node is delegated to
/// <see cref="BaseExpressionVisitor"/> so the body can still reference the query's columns and
/// captured parameters.
/// </summary>
internal sealed class HigherOrderLambdaVisitor : BaseExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, string> _parameters;

    internal HigherOrderLambdaVisitor(VisitorOptions options, Dictionary<ParameterExpression, string> parameters)
        : base(options) => _parameters = parameters;

    internal override IReadOnlyDictionary<ParameterExpression, string>? LambdaParameters => _parameters;

    public override BaseExpressionVisitor Clone()
    {
        if (IsParamMode)
            throw new NotSupportedException("Cannot clone in param mode");

        var clone = new HigherOrderLambdaVisitor(Options, _parameters)
        {
            SuppressColumnCollation = SuppressColumnCollation,
            ConverterContext = ConverterContext,
        };
        Adopt(clone);
        return clone;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameters.TryGetValue(node, out var name))
        {
            if (!IsParamMode)
            {
                NeedAliasForColumn = true;
                Builder!.Append(name);
            }

            return node;
        }

        return base.VisitParameter(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (IsLambdaParameter(node.Expression))
            throw new NotSupportedException(
                $"Member access '.{node.Member.Name}' on the higher-order array lambda parameter is not "
                + "supported: the parameter is a bare SQL element, so it has no translatable members; "
                + "use operators or functions instead.");

        return base.VisitMember(node);
    }

    private bool IsLambdaParameter(Expression? expression)
    {
        while (expression is MemberExpression member)
            expression = member.Expression;

        return expression is ParameterExpression parameter && _parameters.ContainsKey(parameter);
    }
}
