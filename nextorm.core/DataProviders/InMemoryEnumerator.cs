using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    //private readonly CompiledQuery<TResult> _cmd;
    private readonly Func<object, TResult>? _map;
    private readonly IEnumerator<TEntity> _data;
    private readonly Func<TEntity, bool>? _condition;
    //private readonly bool _noMap;

    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, IEnumerator<TEntity> data)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _map = typeof(TResult) == typeof(TEntity)
            ? null
            : cmd.MapDelegate;

        _data = data;

        //if (cmd is InMemoryCompiledQuery<TResult, TEntity> cq)
        _condition = cmd.Condition;

        //_noMap = ;
    }

    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_map is null) return (TResult)(object)_data.Current!;

            return _map(_data.Current!);
            // return default;
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
    next:
        var r = _data.MoveNext();

        if (r && _condition is not null && !_condition(_data.Current))
        {
            goto next;
        }

        return ValueTask.FromResult(r);
    }
}