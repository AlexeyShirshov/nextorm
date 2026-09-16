#define PARAM_CONDITION
namespace nextorm.core;

public sealed class InMemoryCacheEntry<TResult> : IPreparedQueryCommand<TResult>
{
    public InMemoryCacheEntry(object compiledQuery, Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> createEnumerator, QueryCommand<TResult> queryCommand)
    {
        CreateEnumerator = createEnumerator;
        CompiledQuery = compiledQuery;
        QueryCommand = queryCommand;
    }
    public Func<QueryCommand<TResult>, InMemoryCacheEntry<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>> CreateEnumerator { get; }
    public object CompiledQuery { get; set; }
    public QueryCommand<TResult> QueryCommand { get; }
    public bool IsScalar => throw new NotImplementedException();
    public object? Data;
    public IAsyncEnumerator<TResult>? Enumerator;
    /// <summary>
    /// Cached per-call resolver: re-initialises the (already compiled) enumerator with new parameter
    /// values, skipping the per-call data resolution / joins / sorting checks. Built on first use.
    /// </summary>
    public Func<object[]?, IAsyncEnumerator<TResult>>? Resolver;
    public int LastRowCount;
}