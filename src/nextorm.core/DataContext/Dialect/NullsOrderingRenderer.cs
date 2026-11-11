namespace NextORM.Core;

/// <summary>
/// Renders an <c>ORDER BY</c> key with an explicit NULL placement, and validates placement values that
/// reach rendering or execution. Kept out of <see cref="ISqlDialect"/> so the default interface
/// implementation stays a one-line delegation and the CASE emulation oracle lives in one place.
/// </summary>
public static class NullsOrderingRenderer
{
    /// <summary>
    /// Validates a placement value. <see cref="NullsPosition.Default"/> is accepted because it means
    /// "provider default" and the callers handle it as the plain, unmodified ordering path.
    /// </summary>
    /// <param name="nullsPosition">The placement to validate.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="NullsPosition"/> member.</exception>
    public static NullsPosition Validate(NullsPosition nullsPosition) => nullsPosition switch
    {
        NullsPosition.Default or NullsPosition.First or NullsPosition.Last => nullsPosition,
        _ => throw new ArgumentOutOfRangeException(
            nameof(nullsPosition),
            nullsPosition,
            $"Unsupported NULL placement value {(int)nullsPosition}; use {nameof(NullsPosition)}.{nameof(NullsPosition.Default)}, " +
            $"{nameof(NullsPosition)}.{nameof(NullsPosition.First)} or {nameof(NullsPosition)}.{nameof(NullsPosition.Last)}.")
    };

    /// <summary>
    /// Emulates an explicit NULL placement with a <c>CASE</c> rank key followed by the key itself.
    /// <paramref name="expressionSql"/> is the already-qualified/quoted key SQL, so a parameterized key
    /// occurs twice in the returned fragment and both occurrences must be bound. The rank prefix is
    /// always ascending — including for a descending key — and only the key carries the requested
    /// direction. The returned text is one logical ordering item even though it contains two sort keys.
    /// </summary>
    /// <param name="expressionSql">The rendered key SQL.</param>
    /// <param name="direction">The requested key direction.</param>
    /// <param name="nullsPosition">The explicit placement; <see cref="NullsPosition.Default"/> is rejected.</param>
    /// <returns>The emulated ordering fragment.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nullsPosition"/> is not <see cref="NullsPosition.First"/> or <see cref="NullsPosition.Last"/>.</exception>
    public static string Render(string expressionSql, OrderDirection direction, NullsPosition nullsPosition)
    {
        var (nullRank, nonNullRank) = nullsPosition switch
        {
            NullsPosition.First => ("0", "1"),
            NullsPosition.Last => ("1", "0"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(nullsPosition),
                nullsPosition,
                "An explicit NULL placement (First or Last) is required; Default bypasses the renderer.")
        };

        var key = direction == OrderDirection.Desc ? expressionSql + " desc" : expressionSql;
        return $"CASE WHEN {expressionSql} IS NULL THEN {nullRank} ELSE {nonNullRank} END ASC, {key}";
    }

    /// <summary>
    /// Renders a placement with the provider's native <c>NULLS FIRST</c>/<c>NULLS LAST</c> syntax. The
    /// key occurs once, so a parameterized key binds once.
    /// </summary>
    /// <param name="expressionSql">The rendered key SQL.</param>
    /// <param name="direction">The requested key direction.</param>
    /// <param name="nullsPosition">The explicit placement; <see cref="NullsPosition.Default"/> is rejected.</param>
    /// <returns>The native ordering fragment.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nullsPosition"/> is not <see cref="NullsPosition.First"/> or <see cref="NullsPosition.Last"/>.</exception>
    public static string RenderNative(string expressionSql, OrderDirection direction, NullsPosition nullsPosition)
    {
        var placement = nullsPosition switch
        {
            NullsPosition.First => " NULLS FIRST",
            NullsPosition.Last => " NULLS LAST",
            _ => throw new ArgumentOutOfRangeException(
                nameof(nullsPosition),
                nullsPosition,
                "An explicit NULL placement (First or Last) is required; Default bypasses the renderer.")
        };

        return direction == OrderDirection.Desc ? expressionSql + " desc" + placement : expressionSql + placement;
    }
}
