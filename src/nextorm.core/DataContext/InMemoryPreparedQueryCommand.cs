#define PARAM_CONDITION
namespace nextorm.core;

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

public sealed class InMemoryPreparedQueryCommand<TResult>(object compiledQuery, CreateEnumeratorDelegate<TResult> createEnumerator, QueryCommand<TResult> queryCommand) : IPreparedQueryCommand<TResult>
{
    public CreateEnumeratorDelegate<TResult> CreateEnumerator { get; } = createEnumerator;
    public object CompiledQuery { get; set; } = compiledQuery;
    public QueryCommand<TResult> QueryCommand { get; } = queryCommand;
    public object? Data;
    public IAsyncEnumerator<TResult>? Enumerator;
    /// <summary>
    /// Cached per-call resolver: re-initialises the (already compiled) enumerator with new parameter
    /// values, skipping the per-call data resolution / joins / sorting checks. Built on first use.
    /// </summary>
    public Func<object[]?, IAsyncEnumerator<TResult>>? Resolver;
    public int LastRowCount;
}
