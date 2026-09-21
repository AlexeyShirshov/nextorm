using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

public sealed class InMemoryEnumerator<TResult, TEntity> : IAsyncEnumerator<TResult>, IEnumerator<TResult>, IEnumeratorInit<TEntity>, IEnumerable<TResult>
{
    private readonly Func<TEntity, TResult>? _map;
    private readonly Func<object[]?, Func<TEntity, bool>>? _conditionFactory;
    private readonly Func<TEntity, bool>? _conditionDirect;
    private readonly Func<TEntity, object[]?, bool>? _conditionRaw;
    private readonly CancellationToken _cancellationToken;

    private Func<TEntity, bool>? _condition;
    private object[]? _conditionRawParams;

    // Fast paths for the common in-memory sources; falls back to IEnumerator<TEntity> otherwise.
    private List<TEntity>? _list;
    private TEntity[]? _array;
    private IEnumerator<TEntity>? _enumerator;
    private int _idx;
    private TEntity _current = default!;
    private HashSet<TResult>? _seen;

    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, CancellationToken cancellationToken)
        : this(cmd, cancellationToken, false)
    {
    }

    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, CancellationToken cancellationToken, bool distinct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _map = typeof(TResult) == typeof(TEntity)
            ? null
            : cmd.MapDelegate;

        _conditionFactory = cmd.ConditionFactory;
        _conditionDirect = cmd.ConditionDirect;
        _conditionRaw = cmd.Condition;
        _cancellationToken = cancellationToken;

        if (distinct)
            _seen = new HashSet<TResult>(InMemoryDistinct.GetComparer<TResult>());
    }

    public void Init(IEnumerable<TEntity> data, object[]? @params)
    {
        _idx = -1;
        _seen?.Clear();
        switch (data)
        {
            case List<TEntity> list:
                _list = list;
                _array = null;
                _enumerator = null;
                break;
            case TEntity[] array:
                _array = array;
                _list = null;
                _enumerator = null;
                break;
            default:
                _list = null;
                _array = null;
                _enumerator = data.GetEnumerator();
                break;
        }

        if (_conditionFactory is not null && @params is not null)
        {
            _condition = _conditionFactory(@params);
            _conditionRawParams = null;
        }
        else
        {
            _condition = _conditionDirect;
            _conditionRawParams = _conditionRaw is null ? null : @params;
        }
    }

    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map is null ? (TResult)(object)_current! : _map(_current!);
    }

    object? IEnumerator.Current => Current;

    public ValueTask DisposeAsync()
    {
        // Route through Dispose() so the inner enumerator is released exactly once.
        Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult(false);

        // In-memory data is synchronous (async sources go through InMemoryEnumeratorAdapter).
        return ValueTask.FromResult(MoveNext());
    }

    public bool MoveNext()
    {
        if (_seen is null)
            return MoveNextCore();

        // DISTINCT is applied after the WHERE condition and the projection, matching SQL: the
        // predicate is evaluated by MoveNextCore, the projected value is produced by Current.
        while (MoveNextCore())
        {
            if (_seen.Add(Current))
                return true;
        }

        return false;
    }

    private bool MoveNextCore()
    {
        if (_list is not null)
        {
            var list = _list;
            var condition = _condition;
            var i = _idx;

            if (condition is null)
            {
                if (++i >= list.Count) return false;
                _idx = i;
                _current = list[i];
                return true;
            }

            while (++i < list.Count)
            {
                var entity = list[i];
                if (condition(entity))
                {
                    _idx = i;
                    _current = entity;
                    return true;
                }
            }

            _idx = i;
            return false;
        }

        if (_array is not null)
        {
            var array = _array;
            var condition = _condition;
            var i = _idx;

            if (condition is null)
            {
                if (++i >= array.Length) return false;
                _idx = i;
                _current = array[i];
                return true;
            }

            while (++i < array.Length)
            {
                var entity = array[i];
                if (condition(entity))
                {
                    _idx = i;
                    _current = entity;
                    return true;
                }
            }

            _idx = i;
            return false;
        }

        var enumerator = _enumerator!;

        if (_condition is null)
        {
            if (_conditionRaw is null)
            {
                if (!enumerator.MoveNext()) return false;
                _current = enumerator.Current;
                return true;
            }

            var raw = _conditionRaw;
            var rawParams = _conditionRawParams;
            while (enumerator.MoveNext())
            {
                var entity = enumerator.Current;
                if (raw(entity, rawParams))
                {
                    _current = entity;
                    return true;
                }
            }

            return false;
        }

        var typedCondition = _condition;
        while (enumerator.MoveNext())
        {
            var entity = enumerator.Current;
            if (typedCondition(entity))
            {
                _current = entity;
                return true;
            }
        }

        return false;
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
        // The fallback enumerator (created for arbitrary IEnumerable<TEntity> sources) is
        // IDisposable; without this it would leak for iterator/streaming sources.
        _enumerator?.Dispose();
        _enumerator = null;
        GC.SuppressFinalize(this);
    }

    public IEnumerator<TResult> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;
}

/// <summary>
/// Resolves the equality comparer used to de-duplicate a <c>SELECT DISTINCT</c> result in the
/// in-memory provider. Anonymous types, tuples, records and value types all expose structural
/// equality, so the default comparer matches the SQL notion of a duplicate. A reference type
/// without a value-equality override (and interfaces) cannot be compared by value, so DISTINCT is
/// rejected instead of silently returning rows SQL would have collapsed.
/// </summary>
internal static class InMemoryDistinct
{
    public static IEqualityComparer<T> GetComparer<T>()
    {
        var type = typeof(T);

        if (type.IsScalar() || type.IsValueType || type.IsAnonymous() || HasValueEquality(type))
            return EqualityComparer<T>.Default;

        throw new NotSupportedException(
            $"DISTINCT is not supported by the in-memory provider for projection type '{type.Name}' because it does not implement value equality. Project an anonymous type or a value type, or override Equals/GetHashCode.");
    }

    private static bool HasValueEquality(Type type)
    {
        var equals = type.GetMethod(nameof(object.Equals), BindingFlags.Public | BindingFlags.Instance, null, [typeof(object)], null);
        return equals is not null && equals.DeclaringType == type;
    }
}

internal interface IEnumeratorInit<in TEntity>
{
    void Init(IEnumerable<TEntity> data, object[]? @params);
}
