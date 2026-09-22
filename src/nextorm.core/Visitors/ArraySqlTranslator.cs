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
                EmitFunction(visitor, "array_reverse", args);
                return true;
            case nameof(PostgresFunctions.array_sort) when args.Count == 1:
                EmitFunction(visitor, "array_sort", args);
                return true;
            case nameof(PostgresFunctions.array_shuffle) when args.Count == 1:
                EmitFunction(visitor, "array_shuffle", args);
                return true;
            case nameof(PostgresFunctions.array_sample) when args.Count == 2:
                EmitFunction(visitor, "array_sample", args);
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

        EmitArrayFunction(visitor, sqlName, args);
        return true;
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
            nameof(ClickHouseFunctions.array_map) when args.Count == 2 => "arrayMap",
            nameof(ClickHouseFunctions.array_filter) when args.Count == 2 => "arrayFilter",
            nameof(ClickHouseFunctions.array_exists) when args.Count == 2 => "arrayExists",
            nameof(ClickHouseFunctions.array_all) when args.Count == 2 => "arrayAll",
            nameof(ClickHouseFunctions.array_count) when args.Count == 2 => "arrayCount",
            nameof(ClickHouseFunctions.array_first) when args.Count == 2 => "arrayFirst",
            nameof(ClickHouseFunctions.array_first_index) when args.Count == 2 => "arrayFirstIndex",
            nameof(ClickHouseFunctions.array_last) when args.Count == 2 => "arrayLast",
            nameof(ClickHouseFunctions.array_last_index) when args.Count == 2 => "arrayLastIndex",
            _ => null
        };

        if (sqlName is null)
            return false;

        if (!visitor.Dialect.SupportsHigherOrderArrayFunctions)
            throw new NotSupportedException(
                "The higher-order array functions (arrayMap/arrayFilter/arrayExists/arrayAll/arrayCount/arrayFirst*/arrayLast*) require a provider that supports them (ClickHouse).");

        EmitHigherOrderArray(visitor, sqlName, args);
        return true;
    }

    private static void EmitHigherOrderArray(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        var lambda = ExtractLambda(args[0]);

        if (lambda.Parameters.Count != 1)
            throw new NotSupportedException($"The {sqlName} lambda must have exactly one parameter.");

        var parameter = lambda.Parameters[0];
        var name = string.IsNullOrEmpty(parameter.Name) ? "x" : parameter.Name!;
        var parameters = new Dictionary<ParameterExpression, string>();
        if (visitor.LambdaParameters is { } outer)
        {
            foreach (var entry in outer)
                parameters[entry.Key] = entry.Value;
        }

        parameters[parameter] = name;

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
        var start = builder.Length;
        builder.Append(sqlName).Append('(').Append(name).Append(" -> ");
        bodyVisitor.WriteTo(builder);

        for (var (i, cnt) = (1, args.Count); i < cnt; i++)
        {
            builder.Append(", ");
            SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);
        }

        builder.Append(')');

        var call = builder.ToString(start, builder.Length - start);
        builder.Length = start;
        builder.Append(visitor.Dialect.MakeArrayFunction(sqlName, call));
    }

    private static LambdaExpression ExtractLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
            return lambda;

        throw new NotSupportedException("The higher-order array functions require an inline lambda argument.");
    }

    private static void EmitArrayFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                SqlOperandTranslator.AppendArrayOrColumn(visitor, args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;

        var builder = visitor.Builder!;
        var start = builder.Length;
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
        builder.Append(visitor.Dialect.MakeArrayFunction(sqlName, call));
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
        => IsParamMode
            ? throw new NotSupportedException("Cannot clone in param mode")
            : new HigherOrderLambdaVisitor(Options, _parameters);

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
                "Member access on the higher-order array lambda parameter is not supported; use operators or functions instead.");

        return base.VisitMember(node);
    }

    private bool IsLambdaParameter(Expression? expression)
    {
        while (expression is MemberExpression member)
            expression = member.Expression;

        return expression is ParameterExpression parameter && _parameters.ContainsKey(parameter);
    }
}
