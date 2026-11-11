namespace NextORM.Core;

/// <summary>
/// Declares that a <see cref="Range{T}"/> property is stored as a pair of scalar columns instead of a
/// single range column. This is the storage model for providers without a native range type
/// (SQL Server, MySQL, MariaDB, SQLite, ClickHouse); PostgreSQL stores <see cref="Range{T}"/> natively
/// and does not need it. The lower and upper bounds are written to and read from the two named columns;
/// a SQL <c>NULL</c> bound means the corresponding side is unbounded.
/// </summary>
/// <remarks>
/// The pair can only represent finite and unbounded ranges, not the empty range: two <c>NULL</c>
/// columns read back as the fully unbounded range. Bound inclusivity is declared on the attribute
/// (defaulting to the PostgreSQL canonical <c>[lower, upper)</c>) and is part of the mapping, not of
/// the stored values.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class RangeColumnsAttribute : Attribute
{
    /// <summary>Declares the two columns holding the lower and upper bounds of the range.</summary>
    /// <param name="lowerColumn">The column that stores the lower bound.</param>
    /// <param name="upperColumn">The column that stores the upper bound.</param>
    public RangeColumnsAttribute(string lowerColumn, string upperColumn)
    {
        LowerColumn = string.IsNullOrEmpty(lowerColumn) ? throw new ArgumentException("The lower column name is required.", nameof(lowerColumn)) : lowerColumn;
        UpperColumn = string.IsNullOrEmpty(upperColumn) ? throw new ArgumentException("The upper column name is required.", nameof(upperColumn)) : upperColumn;
    }

    /// <summary>The column that stores the lower bound.</summary>
    public string LowerColumn { get; }

    /// <summary>The column that stores the upper bound.</summary>
    public string UpperColumn { get; }

    /// <summary>Whether the lower bound is part of the range. Defaults to <see langword="true"/>.</summary>
    public bool LowerInclusive { get; init; } = true;

    /// <summary>Whether the upper bound is part of the range. Defaults to <see langword="false"/>.</summary>
    public bool UpperInclusive { get; init; }

    internal RangeColumnsMetadata ToMetadata() => new(LowerColumn, UpperColumn, LowerInclusive, UpperInclusive);
}
