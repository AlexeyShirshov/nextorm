using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumeratorInit
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<TEntity, TResult>? _map;
    private IEnumerator<TEntity>? _data;
    private readonly Func<TEntity, bool>? _condition;
    private readonly CancellationToken _cancellationToken;

    //private readonly bool _noMap;

    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _map = typeof(TResult) == typeof(TEntity)
            ? null
            : cmd.MapDelegate;

        //if (cmd is InMemoryCompiledQuery<TResult, TEntity> cq)
        _condition = cmd.Condition;
        _cancellationToken = cancellationToken;

        //_noMap = ;
    }
    public void Init(object data)
    {
        _data = ((IEnumerable<TEntity>)data).GetEnumerator();
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

        if (r && _condition is not null && !_condition(_data.Current))
        {
            goto next;
        }

        return ValueTask.FromResult(r);
    }

    public bool MoveNext()
    {
        return _data!.MoveNext();
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

internal interface IEnumeratorInit
{
    void Init(object data);
}