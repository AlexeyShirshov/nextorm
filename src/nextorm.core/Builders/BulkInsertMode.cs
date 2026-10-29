namespace NextORM.Core;

/// <summary>
/// Selects the execution path of a bulk insert.
/// </summary>
public enum BulkInsertMode
{
    /// <summary>
    /// Prefers the provider's native bulk path when it can honour every requested option and preflight
    /// prerequisite, and falls back to the portable <c>INSERT ... VALUES</c> path only when that path can
    /// honour every request. <c>Auto</c> is the default for every provider.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses the provider's native bulk path and never falls back. A missing prerequisite or an option the
    /// native path cannot honour throws <see cref="NotSupportedException"/> before input is read or writes
    /// are sent, where determinable.
    /// </summary>
    Native,

    /// <summary>
    /// Explicitly chooses the portable <c>INSERT ... VALUES</c> path. A native-only option throws
    /// <see cref="NotSupportedException"/> instead of being silently ignored.
    /// </summary>
    Portable,
}
