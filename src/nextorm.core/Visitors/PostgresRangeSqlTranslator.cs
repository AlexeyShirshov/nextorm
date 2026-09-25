using System.Collections.Generic;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the PostgreSQL range surface of <see cref="PostgresFunctions"/> into SQL: the
/// <c>&amp;&amp;</c>/<c>@&gt;</c>/<c>&lt;@</c> predicates, the positional, adjacency, union,
/// intersection and difference operators, the <c>lower</c>/<c>upper</c>/<c>isempty</c> family and the
/// range constructors.
/// <para>
/// The whole surface is guarded by <see cref="ISqlDialect.SupportsRanges"/> (PostgreSQL opts in
/// today); a provider that does not support it fails with a clear message instead of emitting invalid
/// SQL.
/// </para>
/// </summary>
internal static class PostgresRangeSqlTranslator
{
    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        [nameof(PostgresFunctions.overlaps)] = "&&",
        [nameof(PostgresFunctions.range_contains)] = "@>",
        [nameof(PostgresFunctions.range_contained_by)] = "<@",
        [nameof(PostgresFunctions.range_union)] = "+",
        [nameof(PostgresFunctions.range_intersection)] = "*",
        [nameof(PostgresFunctions.range_difference)] = "-",
        [nameof(PostgresFunctions.range_adjacent)] = "-|-",
        [nameof(PostgresFunctions.range_strictly_left_of)] = "<<",
        [nameof(PostgresFunctions.range_strictly_right_of)] = ">>",
        [nameof(PostgresFunctions.range_not_extend_right_of)] = "&<",
        [nameof(PostgresFunctions.range_not_extend_left_of)] = "&>"
    };

    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.isempty), nameof(PostgresFunctions.lower), nameof(PostgresFunctions.upper),
        nameof(PostgresFunctions.lower_inc), nameof(PostgresFunctions.upper_inc),
        nameof(PostgresFunctions.lower_inf), nameof(PostgresFunctions.upper_inf),
        nameof(PostgresFunctions.int4range), nameof(PostgresFunctions.int8range),
        nameof(PostgresFunctions.numrange), nameof(PostgresFunctions.tsrange),
        nameof(PostgresFunctions.tstzrange), nameof(PostgresFunctions.daterange)
    };

    /// <summary>Translates a range call; returns <c>false</c> when it is not one of them.</summary>
    /// <param name="visitor">The visitor whose builder receives the SQL.</param>
    /// <param name="node">The method call to translate.</param>
    /// <returns><see langword="true"/> when the call was translated.</returns>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(PostgresFunctions))
            return false;

        var name = node.Method.Name;

        if (Operators.TryGetValue(name, out var sqlOperator))
        {
            RequireRanges(visitor);
            SqlOperandTranslator.EmitOperator(visitor, sqlOperator, node.Arguments[0], node.Arguments[1]);
            return true;
        }

        if (Functions.Contains(name))
        {
            RequireRanges(visitor);
            SqlOperandTranslator.EmitFunction(visitor, name, node.Arguments);
            return true;
        }

        if (name == nameof(PostgresFunctions.empty_range))
        {
            RequireRanges(visitor);
            EmitEmpty(visitor, node);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Renders the PostgreSQL empty-range literal <c>'empty'::&lt;range type&gt;</c>, resolving the
    /// range type from the method's bound type argument.
    /// </summary>
    private static void EmitEmpty(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (visitor.IsParamMode)
            return;

        var boundType = node.Method.GetGenericArguments()[0];
        var rangeType = typeof(Range<>).MakeGenericType(boundType);

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append("'empty'::").Append(visitor.Dialect.MakeTypeName(rangeType));
    }

    private static void RequireRanges(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsRanges)
            throw new NotSupportedException(
                "The PostgreSQL range functions and operators (overlaps, range_contains/range_contained_by, range_union, ...) require a provider with a native range type (PostgreSQL).");
    }
}
