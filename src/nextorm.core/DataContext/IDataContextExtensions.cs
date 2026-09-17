namespace nextorm.core;

/// <summary>
/// Common table expression (<c>with</c>) entry points for <see cref="IDataContext"/>.
/// </summary>
public static class IDataContextExtensions
{
    /// <summary>Starts a CTE scope with a single non-recursive declaration.</summary>
    public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query)
        => new CteQuery(dataContext, [new CteDefinition(name, query)]);

    /// <summary>
    /// Starts a CTE scope with a single recursive declaration. <paramref name="maxRecursion"/>
    /// is rendered only by dialects that expose a depth option (SQL Server).
    /// </summary>
    public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query, int? maxRecursion = null)
        => new CteQuery(dataContext, [new CteDefinition(name, query, true, maxRecursion)]);
}
