namespace NextORM.Core;

/// <summary>
/// Composite facade over the individual context roles, kept as the convenient entry point for
/// consumers. The roles are separate interfaces so that providers and collaborators can depend
/// on the smallest surface they actually need:
/// <list type="bullet">
/// <item><see cref="IQueryExecutor"/> — terminal operators;</item>
/// <item><see cref="IQueryMaterializer"/> — planning + row reading;</item>
/// <item><see cref="IQueryCache"/> — plan cache;</item>
/// <item><see cref="IContextEnvironment"/> — loggers, mapping mode, property bag.</item>
/// </list>
/// Connection management is deliberately <b>not</b> part of this composite
/// (see <see cref="IConnectionManager"/>): the in-memory provider must not be forced to
/// implement a no-op connection lifecycle.
/// </summary>
public interface IDataContext :
    IQueryExecutor,
    IQueryMaterializer,
    IQueryCache,
    IContextEnvironment,
    IAsyncDisposable,
    IDisposable
{
}
