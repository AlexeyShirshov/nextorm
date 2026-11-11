namespace NextORM.Core;

/// <summary>
/// Flags controlling how raw SQL prepared through <c>PrepareFromSql</c> is executed. Replaces the
/// pair of <c>nonStreamUsing</c>/<c>storeInCache</c> booleans that used to trail the parameter list.
/// </summary>
[Flags]
public enum PrepareFromSqlMode
{
    /// <summary>
    /// Buffered/scalar execution optimized for a materialized result (the default).
    /// </summary>
    None = 0,

    /// <summary>
    /// Stream the result instead of buffering it. Required when the command is consumed as an
    /// <c>IEnumerable</c>/<c>IAsyncEnumerable</c>.
    /// </summary>
    Streaming = 1,

    /// <summary>Store the prepared command in the plan cache.</summary>
    StoreInCache = 2,
}
