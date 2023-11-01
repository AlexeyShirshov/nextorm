using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>
{
    private readonly CompiledQuery<TResult> _cmd;
    private readonly IEnumerator<TEntity> _data;
    private readonly Func<TEntity, bool>? _condition;

    public InMemoryEnumerator(CompiledQuery<TResult> cmd, IEnumerator<TEntity> data, Expression<Func<TEntity, bool>>? condition)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _cmd = cmd;
        _data = data;
        _condition = condition?.Compile();
    }

    public TResult Current => _cmd.Map(_data.Current!);

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