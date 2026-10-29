namespace NextORM.Core;

/// <summary>
/// SQL Server native <c>SqlBulkCopy</c> options. A group with every value left <see langword="null"/>
/// requests nothing; supplying it on another provider is rejected when the target provider is known.
/// </summary>
public sealed record SqlServerBulkInsertOptions
{
    /// <summary>
    /// Whether <c>SqlBulkCopy</c> runs its own internal transaction around the driver batch. Mapped to the
    /// actual driver batch, not to a whole-operation guarantee: with several driver batches only the
    /// current batch is covered, and an external context transaction combined with this option is
    /// rejected. Defaults to <see langword="null"/> (not requested).
    /// </summary>
    public bool? UseInternalTransaction { get; init; }

    /// <summary>Whether at least one value was explicitly supplied; an empty group creates no request.</summary>
    internal bool RequestsAny => UseInternalTransaction is not null;
}
