using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly CompiledQuery<TResult> _cmd;
    private readonly IAsyncEnumerator<TEntity> _inner;
    private readonly Func<TEntity, bool>? _condition;
    public InMemoryEnumeratorAdapter(CompiledQuery<TResult> cmd, IAsyncEnumerator<TEntity> inner, Expression<Func<TEntity, bool>>? condition)
    {
        _cmd = cmd;
        _inner = inner;
        _condition = condition?.Compile();
    }

    public TResult Current =>_cmd.Map(_inner.Current!);
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