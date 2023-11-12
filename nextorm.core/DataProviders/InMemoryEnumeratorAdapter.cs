//#define INITALGO_2

using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<object, TResult> _map;
    private readonly IAsyncEnumerator<TEntity> _inner;
    private readonly Func<TEntity, bool>? _condition;
    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner)
    {
#if INITALGO_2
        _map = (object o) => cmd.MapDelegate(o, null);
#else
        _map = cmd.MapDelegate;
#endif
        _inner = inner;
        _condition = cmd.Condition;
    }

    public TResult Current => _map(_inner.Current!);
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return _inner.DisposeAsync();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
    next:
        var r = await _inner.MoveNextAsync();

        if (r && _condition is not null && !_condition(_inner.Current))
        {
            goto next;
        }

        return r;
    }
}