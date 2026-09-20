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
