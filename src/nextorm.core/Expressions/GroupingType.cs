namespace NextORM.Core;

/// <summary>
/// A super-aggregate modifier applied to a <c>GROUP BY</c> list. <c>None = 0</c> keeps the default so
/// unset commands and the existing plan hashes are unaffected.
/// </summary>
public enum GroupingType
{
    /// <summary>A plain <c>GROUP BY</c>.</summary>
    None = 0,
    /// <summary><c>GROUP BY ROLLUP (...)</c>: every prefix of the grouping list plus the grand total.</summary>
    Rollup = 1,
    /// <summary><c>GROUP BY CUBE (...)</c>: every combination of the grouping list.</summary>
    Cube = 2,
    /// <summary><c>GROUP BY GROUPING SETS (...)</c>: an explicit list of column subsets.</summary>
    GroupingSets = 3
}
