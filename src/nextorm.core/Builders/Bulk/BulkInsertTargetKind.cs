namespace NextORM.Core;

/// <summary>
/// The kind of table a bulk insert targets.
/// </summary>
public enum BulkInsertTargetKind
{
    /// <summary>
    /// A regular table. <c>Regular</c> is the default.
    /// </summary>
    Regular,

    /// <summary>
    /// A connection- or session-local temporary table. The bulk insert uses the context's existing
    /// connection and, where the provider has one, the same session; it never creates or drops the table.
    /// </summary>
    LocalTemporary,
}
