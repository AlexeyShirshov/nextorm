namespace NextORM.Core;

/// <summary>
/// Well-known query-filter keys shared by the filter API.
/// </summary>
public static class QueryFilters
{
    /// <summary>
    /// The key reported by a filter declared without an explicit key. Passing it to the key-based
    /// <c>IgnoreFilters</c> overload disables every anonymous filter of the matching entity types.
    /// </summary>
    public const string AnonymousKey = "";
}
