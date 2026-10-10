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
}
