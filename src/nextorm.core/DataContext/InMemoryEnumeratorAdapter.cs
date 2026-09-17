#define PARAM_CONDITION
namespace nextorm.core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<TEntity, TResult> _map;
    private readonly IAsyncEnumerator<TEntity> _inner;
    private readonly HashSet<TResult>? _seen;
#if PARAM_CONDITION
    private readonly Func<TEntity, object[]?, bool>? _condition;
#else
    private Func<TEntity, bool>? _condition;
#endif

    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner)
        : this(cmd, inner, false)
    {
    }

    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner, bool distinct)
    {
        _map = cmd.MapDelegate!;
        _inner = inner;
        _condition = cmd.Condition;

        if (distinct)
            _seen = new HashSet<TResult>(InMemoryDistinct.GetComparer<TResult>());
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
        var r = await _inner.MoveNextAsync().ConfigureAwait(false);

        if (r && _condition is not null
#if PARAM_CONDITION
            && !_condition(_inner.Current, null)
#else
            && !_condition(_inner.Current)
#endif
        )
        {
            goto next;
        }

        if (!r)
            return false;

        if (_seen is not null && !_seen.Add(Current))
            goto next;

        return true;
    }
}