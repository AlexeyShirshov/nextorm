using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Renders the internal <see cref="ExtremeRowLowering.NullSafeJoinEqual{T}(T, T)"/> marker, the
/// null-safe group-key equality the <c>All</c> lowering places in a <c>JOIN ON</c> predicate.
/// </summary>
/// <remarks>
/// The marker exists because one provider needs a different spelling in exactly this position:
/// ClickHouse rejects a bare <c>IS NULL</c> residual in <c>JOIN ON</c> (#296) but accepts its native
/// <c>isNotDistinctFrom</c> there, while every other dialect keeps the portable OR-form byte for byte.
/// The lowering only emits the marker for a dialect whose position-aware form is available
/// (<see cref="SqlSourceRenderer.IsClickHouseDialect"/>); any other dialect therefore fails closed
/// here instead of silently receiving a predicate the seam never promised.
/// </remarks>
internal static class ExtremeRowNullSafeJoinTranslator
{
    /// <summary>
    /// Returns <c>false</c> for a call that is not the internal marker; otherwise renders it and
    /// returns <c>true</c>.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(ExtremeRowLowering)
            || node.Method.Name != nameof(ExtremeRowLowering.NullSafeJoinEqual))
            return false;

        if (node.Arguments is not [Expression left, Expression right])
            throw new NotSupportedException(
                "The internal ExtremeRow null-safe join marker requires exactly two operands.");

        // The SQL pass and the parameter pass walk the same expression, so both operands are walked
        // here even though a column operand writes nothing in parameter mode.
        if (visitor.IsParamMode)
        {
            visitor.Visit(left);
            visitor.Visit(right);
            return true;
        }

        if (!SqlSourceRenderer.IsClickHouseDialect(visitor.Dialect))
            throw new NotSupportedException(
                "The internal ExtremeRow null-safe join marker is rendered only for a dialect with a native JOIN ON null-safe comparison.");

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append("isNotDistinctFrom(");
        visitor.Visit(left);
        visitor.Builder!.Append(", ");
        visitor.Visit(right);
        visitor.Builder!.Append(')');
        return true;
    }
}
