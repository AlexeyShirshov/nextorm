#define PARAM_CONDITION
using System.Collections;
namespace NextORM.Core;

/// <summary>
/// Adapts an asynchronous <see cref="IAsyncEnumerator{T}"/> of source rows to the query's
/// <typeparamref name="TResult"/> projection. Used when an in-memory subquery is backed by an async
/// source that has no synchronous counterpart; it applies the predicate and, when requested, the
/// <c>DISTINCT</c> filter as rows are pulled.
/// </summary>
/// <typeparam name="TResult">The projected element type.</typeparam>
/// <typeparam name="TEntity">The source element type.</typeparam>
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

    /// <summary>
    /// Initializes an adapter over the given asynchronous source enumerator.
    /// </summary>
    /// <param name="cmd">The compiled in-memory query, supplying the projection and predicate.</param>
    /// <param name="inner">The async source enumerator to pull rows from.</param>
    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner)
        : this(cmd, inner, false)
    {
    }

    /// <summary>
    /// Initializes an adapter over the given asynchronous source enumerator, optionally
    /// de-duplicating the projection.
    /// </summary>
    /// <param name="cmd">The compiled in-memory query, supplying the projection and predicate.</param>
    /// <param name="inner">The async source enumerator to pull rows from.</param>
    /// <param name="distinct">When <see langword="true"/>, projected values are de-duplicated as they are produced, matching <c>SELECT DISTINCT</c>.</param>
    public InMemoryEnumeratorAdapter(InMemoryCompiledQuery<TResult, TEntity> cmd, IAsyncEnumerator<TEntity> inner, bool distinct)
    {
        _map = cmd.MapDelegate!;
        _inner = inner;
        _condition = cmd.Condition;

        if (distinct)
            _seen = new HashSet<TResult>(InMemoryDistinct.GetComparer<TResult>());
    }

    /// <summary>
    /// Gets the projection of the current source row.
    /// </summary>
    public TResult Current => _map(_inner.Current!);
    object? IEnumerator.Current => Current;
    /// <summary>
    /// Asynchronously disposes the inner source enumerator.
    /// </summary>
    /// <returns>The task returned by the inner enumerator's dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return _inner.DisposeAsync();
    }

    /// <summary>
    /// Asynchronously advances to the next source row whose predicate passes, skipping
    /// already-seen projections when <c>DISTINCT</c> is active.
    /// </summary>
    /// <returns><see langword="true"/> when a row was found; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Not supported: an asynchronous source cannot be rewound, so resetting throws.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void Reset() => throw new NotSupportedException();

    /// <summary>
    /// Suppresses finalization. The inner source is released by <see cref="DisposeAsync"/>, not here.
    /// </summary>
    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>
    /// Returns this instance as a synchronous enumerator. Synchronous moves require the inner source
    /// to also implement <see cref="IEnumerator{T}"/>.
    /// </summary>
    /// <returns>This enumerator.</returns>
    public IEnumerator<TResult> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}