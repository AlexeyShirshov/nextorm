namespace NextORM.Core;

/// <summary>
/// Host object that lets a global query filter read the current <see cref="IDataContext"/> through a
/// member access instead of a bare constant. The filter's context parameter is substituted with
/// <see cref="Context"/>; because the host is reached through a member access, its identity is not part
/// of the plan or expression-cache key, so two contexts share one cached plan while each execution
/// still reads its own context value.
/// </summary>
internal sealed class QueryFilterContext
{
    /// <summary>Creates a host that serves filter reads from <paramref name="context"/>.</summary>
    /// <param name="context">The context whose per-query values the filter reads.</param>
    public QueryFilterContext(IDataContext context) => Context = context;

    /// <summary>The context the filter reads its per-query values from.</summary>
    public IDataContext Context { get; }
}
