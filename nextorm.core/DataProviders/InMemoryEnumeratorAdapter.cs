#define PARAM_CONDITION
using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<TEntity, TResult> _map;
    private readonly IAsyncEnumerator<TEntity> _inner;
#if PARAM_CONDITION
    private object[]? _params;
    private readonly Func<TEntity, object[]?, bool>? _condition;
#else
    private Func<TEntity, bool>? _condition;
#endif

    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner)
    {
        _map = cmd.MapDelegate;
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

        if (r && _condition is not null
#if PARAM_CONDITION
            && !_condition(_inner.Current, _params)
#else
            && !_condition(_inner.Current)
#endif
        )
        {
            goto next;
        }

        return r;
    }
}