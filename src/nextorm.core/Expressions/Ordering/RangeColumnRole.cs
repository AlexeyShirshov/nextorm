namespace NextORM.Core;

/// <summary>
/// Identifies which column of a <see cref="Range{T}"/> pair a <see cref="SelectExpression"/> reads,
/// so the entity materializer can combine the two columns back into a single range value.
/// </summary>
internal enum RangeColumnRole
{
    /// <summary>Not part of a range pair.</summary>
    None,

    /// <summary>The column that stores the lower bound.</summary>
    Lower,

    /// <summary>The column that stores the upper bound.</summary>
    Upper
}
