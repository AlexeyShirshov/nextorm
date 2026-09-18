using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Translates the PostgreSQL array surface written through <see cref="NORM.NORM_SQL"/>: the
/// <c>any</c>/<c>all</c> quantifiers over an array and the common array functions and operators.
/// <para>
/// An array operand is always bound as a single parameter (the whole array), never expanded into a
/// value list, so the SQL text does not depend on the number of elements and the plan stays
/// cacheable. A runtime array is supplied either through <see cref="NORM.Param{T}(int)"/> or as a
/// captured array; see <see cref="SqlOperandTranslator"/> for the operand rendering.
/// </para>
/// <para>
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsArrays"/> (PostgreSQL) may use
/// these constructs; every other provider rejects them with a clear message.
/// </para>
/// </summary>
internal static class ArraySqlTranslator
{
    /// <summary>
    /// Translates <c>NORM.SQL.any(...)</c>/<c>NORM.SQL.all(...)</c> when the operand is an array,
    /// in both the value form (<c>any(array)</c>) and the predicate form
    /// (<c>any(column, array)</c>). Returns <c>false</c> for the subquery form, which is handled by
    /// <see cref="NormSqlTranslator"/>.
    /// </summary>
    internal static bool TryTranslateAnyAll(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var keyword = node.Method.Name switch
        {
            nameof(NORM.NORM_SQL.any) => "any",
            nameof(NORM.NORM_SQL.all) => "all",
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
    /// Translates the array functions and operators of <see cref="NORM.NORM_SQL"/> (for example
    /// <c>cardinality</c>, <c>array_length</c>, <c>array_position</c> and the <c>@&gt;</c>/<c>&amp;&amp;</c>
    /// operators). Returns <c>false</c> when the call is not an array function.
    /// </summary>
    internal static bool TryTranslateFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;

        switch (node.Method.Name)
        {
            case nameof(NORM.NORM_SQL.cardinality) when args.Count == 1:
                EmitFunction(visitor, "cardinality", args);
                return true;
            case nameof(NORM.NORM_SQL.array_length) when args.Count == 2:
                EmitFunction(visitor, "array_length", args);
                return true;
            case nameof(NORM.NORM_SQL.array_ndims) when args.Count == 1:
                EmitFunction(visitor, "array_ndims", args);
                return true;
            case nameof(NORM.NORM_SQL.array_lower) when args.Count == 2:
                EmitFunction(visitor, "array_lower", args);
                return true;
            case nameof(NORM.NORM_SQL.array_upper) when args.Count == 2:
                EmitFunction(visitor, "array_upper", args);
                return true;
            case nameof(NORM.NORM_SQL.array_position) when args.Count == 2:
                EmitFunction(visitor, "array_position", args);
                return true;
            case nameof(NORM.NORM_SQL.array_to_string) when args.Count == 2:
                EmitFunction(visitor, "array_to_string", args);
                return true;
            case nameof(NORM.NORM_SQL.array_append) when args.Count == 2:
                EmitFunction(visitor, "array_append", args);
                return true;
            case nameof(NORM.NORM_SQL.array_prepend) when args.Count == 2:
                EmitFunction(visitor, "array_prepend", args);
                return true;
            case nameof(NORM.NORM_SQL.array_cat) when args.Count == 2:
                EmitFunction(visitor, "array_cat", args);
                return true;
            case nameof(NORM.NORM_SQL.array_remove) when args.Count == 2:
                EmitFunction(visitor, "array_remove", args);
                return true;
            case nameof(NORM.NORM_SQL.array_replace) when args.Count == 3:
                EmitFunction(visitor, "array_replace", args);
                return true;
            case nameof(NORM.NORM_SQL.array_fill) when args.Count == 2:
                EmitFunction(visitor, "array_fill", args);
                return true;
            case nameof(NORM.NORM_SQL.array_dims) when args.Count == 1:
                EmitFunction(visitor, "array_dims", args);
                return true;
            case nameof(NORM.NORM_SQL.array_positions) when args.Count == 2:
                EmitFunction(visitor, "array_positions", args);
                return true;
            case nameof(NORM.NORM_SQL.array_reverse) when args.Count == 1:
                EmitFunction(visitor, "array_reverse", args);
                return true;
            case nameof(NORM.NORM_SQL.array_sort) when args.Count == 1:
                EmitFunction(visitor, "array_sort", args);
                return true;
            case nameof(NORM.NORM_SQL.string_to_array) when args.Count == 2:
                EmitFunction(visitor, "string_to_array", args);
                return true;
            case nameof(NORM.NORM_SQL.array_contains) when args.Count == 2:
                EmitOperator(visitor, "@>", args[0], args[1]);
                return true;
            case nameof(NORM.NORM_SQL.array_contained_by) when args.Count == 2:
                EmitOperator(visitor, "<@", args[0], args[1]);
                return true;
            case nameof(NORM.NORM_SQL.array_overlaps) when args.Count == 2:
                EmitOperator(visitor, "&&", args[0], args[1]);
                return true;
            case nameof(NORM.NORM_SQL.array_concat) when args.Count == 2:
                EmitOperator(visitor, "||", args[0], args[1]);
                return true;
            default:
                return false;
        }
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
