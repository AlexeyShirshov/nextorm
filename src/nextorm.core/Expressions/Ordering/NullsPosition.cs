namespace NextORM.Core;

/// <summary>
/// Where SQL <c>NULL</c> values are placed in an <c>ORDER BY</c> key relative to non-null values.
/// </summary>
/// <remarks>
/// <see cref="Default"/> asks for the provider's own NULL ordering and emits no clause, so it keeps the
/// provider default. <see cref="First"/> and <see cref="Last"/> request an explicit placement: providers
/// with native support render <c>NULLS FIRST</c>/<c>NULLS LAST</c>, while providers without it emulate the
/// placement with a <c>CASE</c> prefix. Placement is independent of <see cref="OrderDirection"/>: it never
/// flips when the direction flips, and reversing a query's order swaps <see cref="First"/> and
/// <see cref="Last"/> so the reversed order stays logically equivalent.
/// </remarks>
public enum NullsPosition
{
    /// <summary>The provider's default NULL ordering; no clause is emitted.</summary>
    Default = 0,
    /// <summary>SQL <c>NULL</c> values sort before every non-null value, regardless of direction.</summary>
    First = 1,
    /// <summary>SQL <c>NULL</c> values sort after every non-null value, regardless of direction.</summary>
    Last = 2
}
