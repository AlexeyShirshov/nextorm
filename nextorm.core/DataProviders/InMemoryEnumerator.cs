#define PARAM_CONDITION
using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumeratorInit<TEntity>, IEnumerable<TResult>
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<TEntity, TResult>? _map;
    private IEnumerator<TEntity>? _data;
#if PARAM_CONDITION
    private object[]? _params;
    private readonly Func<TEntity, object[]?, bool>? _condition;
#else
    private Func<TEntity, bool>? _condition;
#endif
    private readonly CancellationToken _cancellationToken;

    //private readonly bool _noMap;

    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _map = typeof(TResult) == typeof(TEntity)
            ? null
            : cmd.MapDelegate;

        _condition = cmd.Condition;
        _cancellationToken = cancellationToken;

        //_noMap = ;
    }
#if PARAM_CONDITION
    public void Init(object data, object[]? @params)
#else
    public void Init(object data, Func<TEntity, bool>? condition)
#endif
    {
        _data = ((IEnumerable<TEntity>)data).GetEnumerator();
#if PARAM_CONDITION
        _params = @params;
#else
        _condition = condition;
#endif
    }
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_map is null) return (TResult)(object)_data!.Current!;

            return _map(_data!.Current!);
            // return default;
        }
    }

    object? IEnumerator.Current => Current;

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult(false);

        next:
        var r = _data!.MoveNext();

        if (r && _condition is not null
#if PARAM_CONDITION
            && !_condition(_data.Current, _params)
#else
            && !_condition(_data.Current)
#endif
        )
        {
            goto next;
        }

        return ValueTask.FromResult(r);
    }

    public bool MoveNext()
    {
    next:
        var r = _data!.MoveNext();

        if (r && _condition is not null
#if PARAM_CONDITION
            && !_condition(_data.Current, _params)
#else
            && !_condition(_data.Current)
#endif
        )
        {
            goto next;
        }

        return r;
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public IEnumerator<TResult> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}

internal interface IEnumeratorInit<TEntity>
{
#if PARAM_CONDITION
    void Init(object data, object[]? @params);
#else
    void Init(object data, Func<TEntity, bool>? condition);
#endif
}