namespace nextorm.core;

public class InMemoryEnumerator<TResult> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly IEnumerator<object> _data;

    public InMemoryEnumerator(QueryCommand<TResult> cmd, IEnumerator<object> data)
    {
        _cmd = cmd;
        _data = data;
    }

    public TResult Current => (_cmd as IQueryCommand<TResult>).Map(_data.Current);

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