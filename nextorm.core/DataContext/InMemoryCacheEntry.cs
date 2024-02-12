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
    public int LastRowCount;
}