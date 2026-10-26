namespace NextORM.Core;

/// <summary>
/// The provider-specific bulk-copy flags requested for a write, passed to the native bulk hooks
/// <see cref="DataContext.BulkInsertRows"/> and <see cref="DataContext.BulkInsertRowsAsync"/>. A set flag
/// is a request; an unset flag means "not requested", so an explicitly supplied <c>false</c> on
/// <see cref="BulkInsertOptions"/> never appears here (absence is the off state). Only the SQL Server
/// native <c>SqlBulkCopy</c> path can express <c>TableLock</c> and <c>FireTriggers</c>;
/// <c>CheckConstraints=true</c> and <c>KeepNulls=true</c> are honoured by every path, the first because a
/// path that always checks constraints needs no request and the second because every path writes the
/// caller's values.
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
    /// Throws <see cref="NotSupportedException"/> naming every requested flag the path cannot express, so a
    /// path that cannot honour the request fails loudly instead of ignoring it. Only <c>TableLock</c> and
    /// <c>FireTriggers</c> are rejected: <c>CheckConstraints=true</c> and <c>KeepNulls=true</c> match the
    /// default behaviour of every path and are therefore already honoured.
    /// </summary>
    /// <param name="path">The bulk path that cannot express the flags (for the error message).</param>
    /// <exception cref="NotSupportedException">A flag the path cannot express is set.</exception>
    public void ThrowIfRequested(string path)
    {
        var requested = new List<string>(2);
        if (TableLock) requested.Add("TableLock=true");
        if (FireTriggers) requested.Add("FireTriggers=true");

        if (requested.Count == 0)
            return;

        throw new NotSupportedException(
            $"The bulk-insert option(s) {string.Join(", ", requested)} are only supported by the SQL Server native SqlBulkCopy path; {path} cannot express them.");
    }
}
