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
        nameof(ClickHouseFunctions.generate_ulid)
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

        if (MapLambdaFunctions.Contains(name))
        {
            EmitMapLambda(visitor, node, scalars, name);
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

    /// <summary>Renders a two-parameter map lambda (<c>mapFilter</c>, <c>mapApply</c>, <c>mapAll</c>, <c>mapExists</c>).</summary>
    private static void EmitMapLambda(BaseExpressionVisitor visitor, MethodCallExpression node, IScalarFunctions scalars, string name)
    {
        var lambda = ExtractLambda(node.Arguments[0]);

        if (lambda.Parameters.Count != 2)
            throw new NotSupportedException($"The {name} lambda must have exactly two parameters (key, value).");

        var first = lambda.Parameters[0];
        var second = lambda.Parameters[1];
        var parameters = new Dictionary<ParameterExpression, string>
        {
            [first] = string.IsNullOrEmpty(first.Name) ? "k" : first.Name!,
            [second] = string.IsNullOrEmpty(second.Name) ? "v" : second.Name!
        };

        using var bodyVisitor = new HigherOrderLambdaVisitor(visitor.Options with { DontNeedAlias = false }, parameters);
        bodyVisitor.Visit(lambda.Body);

        var body = bodyVisitor.ToString();
        var lambdaSql = $"({parameters[first]}, {parameters[second]}) -> {body}";

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var map = visitor.VisitToString(node.Arguments[1]);
        visitor.Builder!.Append(scalars.Render(name, [lambdaSql, map]));
    }

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
    /// elements. Every pair is an inline <see cref="ValueTuple"/> construction.
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
                NewExpression { Arguments: { Count: 2 } ctor } => ctor,
                MethodCallExpression { Method.Name: "Create", Method.DeclaringType: var declaring, Arguments: { Count: 2 } create }
                    when declaring == typeof(Tuple) => create,
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
