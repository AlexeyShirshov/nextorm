namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly CompiledQuery<TResult> _cmd;
    private readonly IEnumerator<TEntity> _data;
    public InMemoryEnumerator(CompiledQuery<TResult> cmd, IEnumerator<TEntity> data)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        
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