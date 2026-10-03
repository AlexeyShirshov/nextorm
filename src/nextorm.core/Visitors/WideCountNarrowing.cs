using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Shapes the 64-bit scalar produced by a wide (<c>count_big</c>) correlated collection count into
/// the CLR type its consumer expects. <c>Count()</c> and the property-<c>Count</c> projection require
/// an <see cref="int"/>: the Int64 scalar is narrowed with a checked conversion so a value above
/// <see cref="int.MaxValue"/> throws instead of silently wrapping. <c>LongCount()</c> keeps the real
/// 64-bit value.
/// </summary>
/// <remarks>
/// This is the single translation point the navigation rewrite consumes: the returned expression is
/// what the terminal call site sees, so the check stays visible to scalar, predicate, boolean and
/// arithmetic consumers as well as to the in-memory compiled expression.
/// </remarks>
internal static class WideCountNarrowing
{
    /// <summary>
    /// Narrows the Int64 <paramref name="longCountScalar"/> to <see cref="int"/> with
    /// <see cref="Expression.ConvertChecked(Expression, Type)"/>, so an out-of-range count throws
    /// <see cref="OverflowException"/> at execution instead of wrapping.
    /// </summary>
    /// <param name="longCountScalar">The correlated 64-bit count scalar.</param>
    /// <returns>The checked Int32 conversion of <paramref name="longCountScalar"/>.</returns>
    public static Expression ForCount(Expression longCountScalar)
    {
        ArgumentNullException.ThrowIfNull(longCountScalar);
        return Expression.ConvertChecked(longCountScalar, typeof(int));
    }

    /// <summary>
    /// Keeps the Int64 <paramref name="longCountScalar"/> unchanged for <see cref="long"/>-typed
    /// consumers, preserving the full 64-bit count.
    /// </summary>
    /// <param name="longCountScalar">The correlated 64-bit count scalar.</param>
    /// <returns>The same <paramref name="longCountScalar"/> instance.</returns>
    public static Expression ForLongCount(Expression longCountScalar)
    {
        ArgumentNullException.ThrowIfNull(longCountScalar);
        return longCountScalar;
    }
}
