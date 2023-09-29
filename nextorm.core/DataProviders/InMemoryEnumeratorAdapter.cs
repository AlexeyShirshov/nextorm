namespace nextorm.core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly IAsyncEnumerator<TEntity> _inner;

    public InMemoryEnumeratorAdapter(QueryCommand<TResult> cmd, IAsyncEnumerator<TEntity> inner)
    {
        _cmd = cmd;
        _inner = inner;
    }

    public TResult Current =>_cmd.Map(_inner.Current!);
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        
        return _inner.DisposeAsync();
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return _inner.MoveNextAsync();
    }
}