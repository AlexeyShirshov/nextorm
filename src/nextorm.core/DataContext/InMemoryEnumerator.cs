using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Enumerates the results of an in-memory query, mapping each <typeparamref name="TEntity"/> source
/// row to a <typeparamref name="TResult"/> projection. Supports both asynchronous and synchronous
/// consumption (the data is already in memory) and, when <c>DISTINCT</c> was requested,
/// de-duplicates projected values as they are produced.
/// </summary>
/// <typeparam name="TResult">The projected element type.</typeparam>
/// <typeparam name="TEntity">The source element type.</typeparam>
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

    /// <summary>
    /// Initializes an enumerator over the given compiled query.
    /// </summary>
    /// <param name="cmd">The compiled in-memory query, supplying the projection and predicate.</param>
    /// <param name="cancellationToken">Token checked before each asynchronous move; once cancelled, enumeration ends.</param>
    public InMemoryEnumerator(InMemoryCompiledQuery<TResult, TEntity> cmd, CancellationToken cancellationToken)
        : this(cmd, cancellationToken, false)
    {
    }

    /// <summary>
    /// Initializes an enumerator over the given compiled query, optionally de-duplicating the projection.
    /// </summary>
    /// <param name="cmd">The compiled in-memory query, supplying the projection and predicate.</param>
    /// <param name="cancellationToken">Token checked before each asynchronous move; once cancelled, enumeration ends.</param>
    /// <param name="distinct">When <see langword="true"/>, projected values are de-duplicated as they are produced, matching <c>SELECT DISTINCT</c>.</param>
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

    /// <summary>
    /// Binds the source sequence and parameter values to enumerate. Called once before enumeration by
    /// the in-memory executor; <paramref name="data"/> is dispatched to fast paths for
    /// <c>List&lt;TEntity&gt;</c> and <c>TEntity[]</c>, falling back to a generic enumerator otherwise.
    /// </summary>
    /// <param name="data">The source rows to enumerate.</param>
    /// <param name="params">Positional parameter values consumed by a parameterised predicate, if any.</param>
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

    /// <summary>
    /// Gets the projected value for the current source row. Reading it before the first successful
    /// <see cref="MoveNext"/> or after enumeration has finished is undefined.
    /// </summary>
    public TResult Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _map is null ? (TResult)(object)_current! : _map(_current!);
    }

    object? IEnumerator.Current => Current;

    /// <summary>
    /// Releases the underlying source enumerator. Equivalent to <see cref="Dispose"/>; provided for
    /// <see cref="IAsyncEnumerator{T}"/> consumers.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        // Route through Dispose() so the inner enumerator is released exactly once.
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously advances to the next matching row. Because the data is already in memory the
    /// work is synchronous; the returned task is already completed.
    /// </summary>
    /// <returns><see langword="true"/> when a row was found; otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult(false);

        // In-memory data is synchronous (async sources go through InMemoryEnumeratorAdapter).
        return ValueTask.FromResult(MoveNext());
    }

    /// <summary>
    /// Advances to the next row matching the predicate and, when <c>DISTINCT</c> was requested,
    /// skips already-seen projections.
    /// </summary>
    /// <returns><see langword="true"/> when a row was found; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Does nothing. Re-running the source would require a fresh <see cref="Init"/>, so the
    /// enumerator is re-created rather than reset.
    /// </summary>
    public void Reset()
    {
    }

    /// <summary>
    /// Disposes the fallback source enumerator (created for arbitrary
    /// <see cref="IEnumerable{T}"/> sources) so streaming sources are not leaked. Safe to call more
    /// than once.
    /// </summary>
    public void Dispose()
    {
        // The fallback enumerator (created for arbitrary IEnumerable<TEntity> sources) is
        // IDisposable; without this it would leak for iterator/streaming sources.
        _enumerator?.Dispose();
        _enumerator = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns this instance as the synchronous enumerator over the projected results.
    /// </summary>
    /// <returns>This enumerator.</returns>
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
