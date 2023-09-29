namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly IEnumerator<TEntity> _data;
    public InMemoryEnumerator(QueryCommand<TResult> cmd, IEnumerator<TEntity> data)
    {
        _cmd = cmd;
        _data = data;
    }

    public TResult Current => _cmd.Map(_data.Current!);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_data.MoveNext());
    }
}