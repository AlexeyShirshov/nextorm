#define PARAM_CONDITION
using System.Collections;
namespace NextORM.Core;

public class InMemoryEnumeratorAdapter<TResult, TEntity> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumerable<TResult>
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
    object? IEnumerator.Current => Current;
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

    /// <summary>
    /// Synchronous enumeration over a buffered subquery. The inner enumerator of a buffered
    /// in-memory subquery implements <see cref="IEnumerator{T}"/>; an async inner source has no
    /// synchronous counterpart and is rejected.
    /// </summary>
    public bool MoveNext()
    {
        if (_inner is not IEnumerator<TEntity> sync)
            throw new NotSupportedException("Synchronous enumeration over an async in-memory source is not supported.");

    next:
        if (!sync.MoveNext())
            return false;

#if PARAM_CONDITION
        if (_condition is not null && !_condition(sync.Current, null))
#else
        if (_condition is not null && !_condition(sync.Current))
#endif
            goto next;

        if (_seen is not null && !_seen.Add(Current))
            goto next;

        return true;
    }

    public void Reset() => throw new NotSupportedException();

    public void Dispose() => GC.SuppressFinalize(this);

    public IEnumerator<TResult> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}