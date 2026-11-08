namespace NextORM.Core;

/// <summary>
/// The ClickHouse <c>ARRAY JOIN</c> variants. <see cref="Inner"/> renders <c>ARRAY JOIN</c>, which drops
/// a row whose array is empty; <see cref="Left"/> renders <c>LEFT ARRAY JOIN</c>, which keeps it with the
/// array columns left at their defaults.
/// </summary>
public enum ArrayJoinKind
{
    /// <summary><c>ARRAY JOIN</c>: expands each array element and drops rows whose array is empty.</summary>
    Inner = 0,
    /// <summary><c>LEFT ARRAY JOIN</c>: expands each array element and keeps rows whose array is empty.</summary>
    Left = 1
}
