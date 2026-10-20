namespace NextORM.Core;

/// <summary>
/// Selects how the collections declared with <c>LoadWith</c> are loaded. The mode belongs to the whole
/// builder: an explicit value applies to every declared collection, including those declared before it,
/// while <see cref="Default"/> inherits the current choice.
/// </summary>
public enum EagerLoadMode
{
    /// <summary>No explicit choice: inherit the builder's current mode, defaulting to split loading.</summary>
    Default = 0,
    /// <summary>Split loading (the default): the parent command plus one chunked child query per collection.</summary>
    SplitQuery = 1,
    /// <summary>Single-query loading: one denormalized <c>LEFT JOIN</c> command stitched in memory.</summary>
    SingleQuery = 2,
    /// <summary>
    /// Correlated per-parent loading. Recognized but not supported in this release: a query that resolves
    /// to this mode is rejected with <see cref="NotSupportedException"/> before any SQL is generated or
    /// cached. The value is reserved for the correlated/per-parent top-N work tracked separately.
    /// </summary>
    Correlated = 3,
    /// <summary>
    /// Automatic strategy selection. Resolved once during preparation by the conservative E1 rule, which
    /// selects <see cref="SplitQuery"/> for every supported load; it never silently reinterprets a
    /// per-parent top-N or correlated request.
    /// </summary>
    Auto = 4,
}
