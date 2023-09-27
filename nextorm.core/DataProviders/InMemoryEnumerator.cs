namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly IEnumerator<TEntity> _data;
    private readonly IDataProvider _dataProvider;

    public InMemoryEnumerator(QueryCommand<TResult> cmd, IEnumerator<TEntity> data, IDataProvider dataProvider)
    {
        _cmd = cmd;
        _data = data;
        _dataProvider = dataProvider;
    }

    public TResult Current => _dataProvider.Map<TResult>(_cmd, _data.Current!);

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