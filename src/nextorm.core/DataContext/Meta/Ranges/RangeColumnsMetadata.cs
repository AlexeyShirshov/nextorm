namespace NextORM.Core;

/// <summary>
/// The column-pair storage descriptor of a <see cref="Range{T}"/> property declared with
/// <see cref="RangeColumnsAttribute"/> or the fluent
/// <see cref="EntityPropertyBuilder{T}.RangeColumns(string, string, bool, bool)"/> mapping.
/// </summary>
public sealed class RangeColumnsMetadata
{
    /// <summary>Creates the descriptor for the given pair of bound columns.</summary>
    /// <param name="lowerColumn">The column that stores the lower bound.</param>
    /// <param name="upperColumn">The column that stores the upper bound.</param>
    /// <param name="lowerInclusive">Whether the lower bound is part of the range.</param>
    /// <param name="upperInclusive">Whether the upper bound is part of the range.</param>
    public RangeColumnsMetadata(string lowerColumn, string upperColumn, bool lowerInclusive = true, bool upperInclusive = false)
    {
        LowerColumn = lowerColumn;
        UpperColumn = upperColumn;
        LowerInclusive = lowerInclusive;
        UpperInclusive = upperInclusive;
    }

    /// <summary>The column that stores the lower bound.</summary>
    public string LowerColumn { get; }

    /// <summary>The column that stores the upper bound.</summary>
    public string UpperColumn { get; }

    /// <summary>Whether the lower bound is part of the range.</summary>
    public bool LowerInclusive { get; }

    /// <summary>Whether the upper bound is part of the range.</summary>
    public bool UpperInclusive { get; }
}
