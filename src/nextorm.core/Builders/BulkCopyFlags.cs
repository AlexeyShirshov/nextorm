namespace NextORM.Core;

/// <summary>
/// The provider-specific bulk-copy flags requested for a write, passed to the native bulk hooks
/// <see cref="DataContext.BulkInsertRows"/> and <see cref="DataContext.BulkInsertRowsAsync"/>. Only the
/// SQL Server native <c>SqlBulkCopy</c> path can express them; every other path rejects a non-empty set
/// with <see cref="NotSupportedException"/> instead of silently ignoring it.
/// </summary>
/// <param name="CheckConstraints">Whether CHECK and FOREIGN KEY constraints are checked.</param>
/// <param name="TableLock">Whether a table-level bulk-update lock is taken.</param>
/// <param name="KeepNulls">Whether explicit nulls are written instead of the destination DEFAULT.</param>
/// <param name="FireTriggers">Whether INSERT triggers fire for the written rows.</param>
public readonly record struct BulkCopyFlags(bool CheckConstraints, bool TableLock, bool KeepNulls, bool FireTriggers)
{
    /// <summary>The empty flag set, equivalent to a write with no bulk-copy flags.</summary>
    public static readonly BulkCopyFlags None = new(false, false, false, false);

    /// <summary>Whether at least one flag is set.</summary>
    public bool IsAny => CheckConstraints || TableLock || KeepNulls || FireTriggers;

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> naming every requested flag when the set is not empty,
    /// so a path that cannot express the flags fails loudly instead of ignoring them.
    /// </summary>
    /// <param name="path">The bulk path that cannot express the flags (for the error message).</param>
    /// <exception cref="NotSupportedException">At least one flag is set.</exception>
    public void ThrowIfRequested(string path)
    {
        if (!IsAny)
            return;

        var requested = new List<string>(4);
        if (CheckConstraints) requested.Add(nameof(CheckConstraints));
        if (TableLock) requested.Add(nameof(TableLock));
        if (KeepNulls) requested.Add(nameof(KeepNulls));
        if (FireTriggers) requested.Add(nameof(FireTriggers));

        throw new NotSupportedException(
            $"The bulk-insert option(s) {string.Join(", ", requested)} are only supported by the SQL Server native SqlBulkCopy path; {path} cannot express them.");
    }
}
