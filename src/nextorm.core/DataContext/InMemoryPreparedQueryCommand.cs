#define PARAM_CONDITION
namespace NextORM.Core;

/// <summary>
/// Creates (or re-initialises) the async enumerator that reads an in-memory query result.
/// Gives a name and XML documentation to the shape that was previously an anonymous
/// <see cref="Func{T1,T2,T3,T4,TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The element type produced by the query.</typeparam>
public delegate IAsyncEnumerator<TResult> CreateEnumeratorDelegate<TResult>(
    QueryCommand<TResult> queryCommand,
    InMemoryPreparedQueryCommand<TResult> preparedQueryCommand,
    object[]? @params,
    CancellationToken cancellationToken);

/// <summary>
/// A prepared in-memory query: the compiled query together with the delegate that (re)creates its
/// enumerator and a cache of the per-call resolver.
/// </summary>
/// <typeparam name="TResult">The projected element type.</typeparam>
/// <param name="compiledQuery">The compiled in-memory query holding the resolved data, joins and sorting.</param>
/// <param name="createEnumerator">Factory that creates a fresh enumerator over the compiled query.</param>
/// <param name="queryCommand">The query definition being executed.</param>
public sealed class InMemoryPreparedQueryCommand<TResult>(object compiledQuery, CreateEnumeratorDelegate<TResult> createEnumerator, QueryCommand<TResult> queryCommand) : IPreparedQueryCommand<TResult>
{
    /// <summary>
    /// Gets the factory that creates a fresh enumerator over the compiled query.
    /// </summary>
    public CreateEnumeratorDelegate<TResult> CreateEnumerator { get; } = createEnumerator;
    /// <summary>
    /// Gets or sets the compiled in-memory query, which holds the resolved data, joins and sorting.
    /// </summary>
    public object CompiledQuery { get; set; } = compiledQuery;
    /// <summary>
    /// Gets the query definition this prepared command executes.
    /// </summary>
    public QueryCommand<TResult> QueryCommand { get; } = queryCommand;
    /// <summary>
    /// The resolved source data for the current execution, if any.
    /// </summary>
    public object? Data;
    /// <summary>
    /// The enumerator reused across executions, re-initialised with new parameter values via
    /// <see cref="Resolver"/>.
    /// </summary>
    public IAsyncEnumerator<TResult>? Enumerator;
    /// <summary>
    /// Cached per-call resolver: re-initialises the (already compiled) enumerator with new parameter
    /// values, skipping the per-call data resolution / joins / sorting checks. Built on first use.
    /// </summary>
    public Func<object[]?, IAsyncEnumerator<TResult>>? Resolver;
    /// <summary>
    /// The number of rows returned by the most recent execution.
    /// </summary>
    public int LastRowCount;
}
