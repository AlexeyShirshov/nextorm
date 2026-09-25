using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// A strongly typed query command producing <typeparamref name="TResult"/> rows. It adds the
/// terminal execution surface - materialization, scalar and aggregate terminals, and set operations -
/// on top of the relational shape carried by <see cref="QueryCommand"/>.
/// </summary>
/// <typeparam name="TResult">The projected row, element or scalar type produced by the command.</typeparam>
public sealed partial class QueryCommand<TResult> : QueryCommand
{

    /// <summary>
    /// Initializes a command of type <typeparamref name="TResult"/> from a query shape.
    /// </summary>
    /// <param name="dataProvider">The context that executes the command, or <c>null</c> when the command is an unbound clone.</param>
    /// <param name="definition">The shape (projection, filtering, joins, grouping, sorting and provider clauses) to copy from.</param>
    public QueryCommand(IDataContext? dataProvider, QueryDefinition definition)
        : base(dataProvider, definition)
    {

    }
    /// <summary>
    /// Prepares the command and records <typeparamref name="TResult"/> in the result plan. The result
    /// type hash is folded into the plan key only when <paramref name="dontCalculateHash"/> is <c>false</c>.
    /// </summary>
    /// <param name="dontCalculateHash">When <c>true</c>, skips recomputing the plan hashes; the caller promises the shape is unchanged.</param>
    /// <param name="cancellationToken">A token to cancel preparation.</param>
    public override void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    {
        base.PrepareCommand(dontCalculateHash, cancellationToken);
        if (!dontCalculateHash)
        {
            ResultPlanHash = typeof(TResult).GetHashCode();
        }
        ResultType = typeof(TResult);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
    {
        return _dataContext!.GetPreparedQueryCommand(this, !nonStreamUsing, false, cancellationToken);
    }
    /// <summary>
    /// Prepares the command and returns an async enumerator over its rows.
    /// </summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An async enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(params object[]? @params) => CreateAsyncEnumerator(CancellationToken.None, @params);
    /// <summary>
    /// Prepares the command and returns an async enumerator over its rows.
    /// </summary>
    /// <param name="cancellationToken">A token observed while preparing and while enumerating rows.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An async enumerator over the result rows.</returns>
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>
    /// Prepares the command and returns a task producing a synchronous row enumerator.
    /// </summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing an enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(params object[]? @params) => CreateEnumeratorAsync(CancellationToken.None, @params);
    /// <summary>
    /// Prepares the command and returns a task producing a synchronous row enumerator.
    /// </summary>
    /// <param name="cancellationToken">A token observed while preparing and opening the reader.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing an enumerator over the result rows.</returns>
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateEnumeratorAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>Streams the result set asynchronously using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An async stream of the result rows.</returns>
    public IAsyncEnumerable<TResult> Pipeline(params object[] @params) => Pipeline(CancellationToken.None, @params);
    /// <summary>
    /// Streams the result set asynchronously. The previous implementation ran the synchronous
    /// <see cref="ToEnumerable"/> on a thread-pool thread and pushed every row through an
    /// unbounded <c>Channel</c>: it blocked a pool thread for the whole result set, buffered the
    /// entire result in memory when the consumer was slower, and observed cancellation only
    /// between rows. Driving the async enumerator directly keeps the same semantics without the
    /// extra thread and without the unbounded buffer.
    /// </summary>
    public async IAsyncEnumerable<TResult> Pipeline(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        await using var enumerator = _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);
        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            yield return enumerator.Current;
    }
    /// <summary>Returns the result set as an async stream using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An async stream of the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), CancellationToken.None, @params);
    /// <summary>Returns the result set as an async stream, using the supplied cancellation token.</summary>
    /// <param name="cancellationToken">A token observed while enumerating rows.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An async stream of the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken), cancellationToken, @params);
    /// <summary>Executes the command and returns the result set as a synchronous sequence.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A sequence over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(params object[] @params) => _dataContext!.GetEnumerable(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), @params);
    /// <summary>Executes the command and materializes every row into a list.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A list containing all result rows.</returns>
    public List<TResult> ToList(params ReadOnlySpan<object?> @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);

        return _dataContext.ToList<TResult>(preparedCommand, @params);
    }
    /// <summary>Executes the command asynchronously and materializes every row into a list, using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a list of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None), @params, CancellationToken.None);
    /// <summary>Executes the command asynchronously and materializes every row into a list.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a list of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken), @params, cancellationToken);
    /// <summary>Executes the command and materializes every row into an array.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>An array containing all result rows.</returns>
    public TResult[] ToArray(params ReadOnlySpan<object?> @params) => ToList(@params).ToArray();
    /// <summary>Executes the command asynchronously and materializes every row into an array, using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing an array of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult[]> ToArrayAsync(params object[] @params) => ToArrayAsync(CancellationToken.None, @params);
    /// <summary>Executes the command asynchronously and materializes every row into an array.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing an array of all result rows.</returns>
    public async Task<TResult[]> ToArrayAsync(CancellationToken cancellationToken, params object[] @params)
        => (await ToListAsync(cancellationToken, @params).ConfigureAwait(false)).ToArray();
    /// <summary>Executes the command and materializes the rows into a hash set.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A set containing all result rows.</returns>
    public HashSet<TResult> ToHashSet(params ReadOnlySpan<object?> @params) => new(ToList(@params));
    /// <summary>Executes the command and materializes the rows into a hash set that uses the supplied comparison.</summary>
    /// <param name="comparer">The equality comparer used to compare rows, or <c>null</c> to use the default comparer.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A set containing all result rows.</returns>
    public HashSet<TResult> ToHashSet(IEqualityComparer<TResult>? comparer, params ReadOnlySpan<object?> @params) => new(ToList(@params), comparer);
    /// <summary>Executes the command asynchronously and materializes the rows into a hash set, using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a set of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<HashSet<TResult>> ToHashSetAsync(params object[] @params) => ToHashSetAsync(CancellationToken.None, @params);
    /// <summary>Executes the command asynchronously and materializes the rows into a hash set.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a set of all result rows.</returns>
    public async Task<HashSet<TResult>> ToHashSetAsync(CancellationToken cancellationToken, params object[] @params)
        => new(await ToListAsync(cancellationToken, @params).ConfigureAwait(false));
    /// <summary>Executes the command asynchronously and materializes the rows into a hash set that uses the supplied comparison.</summary>
    /// <param name="comparer">The equality comparer used to compare rows, or <c>null</c> to use the default comparer.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a set of all result rows.</returns>
    public async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer, CancellationToken cancellationToken, params object[] @params)
        => new(await ToListAsync(cancellationToken, @params).ConfigureAwait(false), comparer);
    /// <summary>Executes the command and materializes the rows into a dictionary keyed by <paramref name="keySelector"/>.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">Maps a row to its dictionary key; keys must be unique.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A dictionary containing all result rows.</returns>
    public Dictionary<TKey, TResult> ToDictionary<TKey>(Func<TResult, TKey> keySelector, params ReadOnlySpan<object?> @params) where TKey : notnull
        => ToDictionary(keySelector, null, @params);
    /// <summary>Executes the command and materializes the rows into a dictionary keyed by <paramref name="keySelector"/>.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">Maps a row to its dictionary key; keys must be unique.</param>
    /// <param name="comparer">The equality comparer used to compare keys, or <c>null</c> to use the default comparer.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A dictionary containing all result rows.</returns>
    public Dictionary<TKey, TResult> ToDictionary<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer, params ReadOnlySpan<object?> @params) where TKey : notnull
    {
        var list = ToList(@params);
        var dict = new Dictionary<TKey, TResult>(list.Count, comparer);
        foreach (var item in list)
            dict.Add(keySelector(item), item);
        return dict;
    }
    /// <summary>Executes the command asynchronously and materializes the rows into a dictionary keyed by <paramref name="keySelector"/>, using the default cancellation token.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">Maps a row to its dictionary key; keys must be unique.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a dictionary of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(keySelector, CancellationToken.None, @params);
    /// <summary>Executes the command asynchronously and materializes the rows into a dictionary keyed by <paramref name="keySelector"/>.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">Maps a row to its dictionary key; keys must be unique.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a dictionary of all result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(keySelector, null, cancellationToken, @params);
    /// <summary>Executes the command asynchronously and materializes the rows into a dictionary keyed by <paramref name="keySelector"/>.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">Maps a row to its dictionary key; keys must be unique.</param>
    /// <param name="comparer">The equality comparer used to compare keys, or <c>null</c> to use the default comparer.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing a dictionary of all result rows.</returns>
    public async Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
    {
        var list = await ToListAsync(cancellationToken, @params).ConfigureAwait(false);
        var dict = new Dictionary<TKey, TResult>(list.Count, comparer);
        foreach (var item in list)
            dict.Add(keySelector(item), item);
        return dict;
    }
    /// <summary>Returns <c>true</c> when the query produces at least one row.</summary>
    /// <returns><c>true</c> if at least one row exists; otherwise <c>false</c>.</returns>
    public bool Any() => Any(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns <c>true</c> when the query produces at least one row, binding the supplied parameters.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns><c>true</c> if at least one row exists; otherwise <c>false</c>.</returns>
    public bool Any(params ReadOnlySpan<object?> @params)
    {
        if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
        {
            // Keep IgnoreColumns set: restoring it would change ColumnsPlanHash and force a full
            // re-prepare on every call, even though the cached plan is already built.
            if (!_isPrepared || !IgnoreColumns)
            {
                IgnoreColumns = true;
                PrepareCommand(false, CancellationToken.None);
            }

            queryCommand = EntityBuilderExtensions.GetAnyCommand(_dataContext!, this);
        }

        var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true);
    }
    /// <summary>Asynchronously returns <c>true</c> when the query produces at least one row, using the default cancellation token.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing <c>true</c> if at least one row exists; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns <c>true</c> when the query produces at least one row.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing <c>true</c> if at least one row exists; otherwise <c>false</c>.</returns>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
        {
            if (!_isPrepared || !IgnoreColumns)
            {
                IgnoreColumns = true;
                PrepareCommand(false, cancellationToken);
            }

            queryCommand = EntityBuilderExtensions.GetAnyCommand(_dataContext!, this);
        }

        var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Executes the command and returns the first column of the first row as a scalar.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The scalar value, or <c>null</c> when the query returns no rows.</returns>
    public TResult? ExecuteScalar(params ReadOnlySpan<object?> @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false);
    }
    /// <summary>Asynchronously executes the command and returns the first column of the first row as a scalar.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the scalar value, or <c>null</c> when the query returns no rows.</returns>
    public Task<TResult?> ExecuteScalarAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false, cancellationToken);
    }

    /// <summary>Returns the first row of the query and throws when it produces none.</summary>
    /// <returns>The first result row.</returns>
    public TResult First() => First(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the first row of the query, binding the supplied parameters, and throws when it produces none.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The first result row.</returns>
    public TResult First(params ReadOnlySpan<object?> @params)
    {
        // Keep the single-row shape on the command. Restoring Paging.Limit would change the cached
        // plan key (QueryPlanEqualityComparer includes Limit/SingleRow) and force a full re-prepare
        // (SQL + hashes) on every call.
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.First<TResult>(preparedCommand, @params);
    }
    /// <summary>Asynchronously returns the first row of the query, using the default cancellation token, and throws when it produces none.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the first result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the first row of the query and throws when it produces none.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the first result row.</returns>
    public Task<TResult> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.FirstAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>Returns the first row of the query, or the default value when it produces none.</summary>
    /// <returns>The first result row, or the default value for the element type.</returns>
    public TResult? FirstOrDefault() => FirstOrDefault(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the first row of the query, binding the supplied parameters, or the default value when it produces none.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The first result row, or the default value for the element type.</returns>
    public TResult? FirstOrDefault(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.FirstOrDefault<TResult>(preparedCommand, @params);
    }
    /// <summary>Asynchronously returns the first row of the query using the default cancellation token, or the default value when it produces none.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the first result row, or the default value for the element type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the first row of the query, or the default value when it produces none.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the first result row, or the default value for the element type.</returns>
    public Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.FirstOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>Returns the only row of the query and throws when it produces zero or more than one row.</summary>
    /// <returns>The single result row.</returns>
    public TResult Single() => Single(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the only row of the query, binding the supplied parameters, and throws when it produces zero or more than one row.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The single result row.</returns>
    public TResult Single(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.Single<TResult>(preparedCommand, @params);
    }
    /// <summary>Asynchronously returns the only row of the query using the default cancellation token, and throws when it produces zero or more than one row.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the single result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the only row of the query and throws when it produces zero or more than one row.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the single result row.</returns>
    public Task<TResult> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.SingleAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>Returns the only row of the query, or the default value when it produces no rows; throws when it produces more than one.</summary>
    /// <returns>The single result row, or the default value for the element type when there is none.</returns>
    public TResult? SingleOrDefault() => SingleOrDefault(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the only row of the query, binding the supplied parameters, or the default value when it produces no rows; throws when it produces more than one.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The single result row, or the default value for the element type when there is none.</returns>
    public TResult? SingleOrDefault(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.SingleOrDefault<TResult>(preparedCommand, @params);
    }
    /// <summary>Asynchronously returns the only row of the query using the default cancellation token, or the default value when it produces no rows; throws when it produces more than one.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the single result row, or the default value for the element type when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the only row of the query, or the default value when it produces no rows; throws when it produces more than one.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the single result row, or the default value for the element type when there is none.</returns>
    public Task<TResult?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.SingleOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    /// <summary>Creates an empty command of the same concrete type, bound to this command's context.</summary>
    /// <returns>A new command used as the clone destination.</returns>
    protected override QueryCommand CreateSelf()
    {
        return new QueryCommand<TResult>(_dataContext, Definition);
    }
    /// <summary>Creates an empty command of the same concrete type with the cache-safe parts of the shape, used to build a cached clone.</summary>
    /// <returns>A new command with projections, conditions and grouping stripped for the plan cache.</returns>
    protected override QueryCommand CreateSelfForClone()
    {
        return new QueryCommand<TResult>(null, Definition with { Exp = null, Condition = null, Joins = CloneForCache(Joins), Group = null });
    }
    /// <summary>
    /// Builds the command that returns the last row of the ordered query by reversing every
    /// <c>ORDER BY</c> direction and reusing the <c>First</c> path. A query without <c>ORDER BY</c>
    /// has no defined last row, so it is rejected rather than returning an arbitrary one.
    /// </summary>
    private QueryCommand<TResult> ForLast()
    {
        if (_sorting is null || _sorting.Length == 0)
            throw new InvalidOperationException("Last/LastOrDefault requires an ORDER BY; the source order is not defined without one.");

        var reversed = new Sorting[_sorting.Length];
        for (var i = 0; i < _sorting.Length; i++)
        {
            var sorting = _sorting[i];
            var flipped = sorting.ColumnIndex is int columnIndex
                ? new Sorting(columnIndex)
                : new Sorting(sorting.SortExpression!);
            flipped.Direction = sorting.Direction == OrderDirection.Asc ? OrderDirection.Desc : OrderDirection.Asc;
            flipped.PreparedExpression = sorting.PreparedExpression;
            reversed[i] = flipped;
        }

        var cmd = new QueryCommand<TResult>(_dataContext, Definition with { Sorting = reversed });
        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    /// <summary>Returns the last row of the ordered query and throws when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <returns>The last result row.</returns>
    public TResult Last() => Last(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the last row of the ordered query, binding the supplied parameters, and throws when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The last result row.</returns>
    public TResult Last(params ReadOnlySpan<object?> @params) => ForLast().First(@params);
    /// <summary>Asynchronously returns the last row of the ordered query using the default cancellation token, and throws when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the last result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> LastAsync(params object[] @params) => LastAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the last row of the ordered query and throws when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the last result row.</returns>
    public Task<TResult> LastAsync(CancellationToken cancellationToken, params object[] @params) => ForLast().FirstAsync(cancellationToken, @params);
    /// <summary>Returns the last row of the ordered query, or the default value when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <returns>The last result row, or the default value for the element type.</returns>
    public TResult? LastOrDefault() => LastOrDefault(ReadOnlySpan<object?>.Empty);
    /// <summary>Returns the last row of the ordered query, binding the supplied parameters, or the default value when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The last result row, or the default value for the element type.</returns>
    public TResult? LastOrDefault(params ReadOnlySpan<object?> @params) => ForLast().FirstOrDefault(@params);
    /// <summary>Asynchronously returns the last row of the ordered query using the default cancellation token, or the default value when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the last result row, or the default value for the element type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> LastOrDefaultAsync(params object[] @params) => LastOrDefaultAsync(CancellationToken.None, @params);
    /// <summary>Asynchronously returns the last row of the ordered query, or the default value when it produces none. Requires an <c>ORDER BY</c>.</summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the last result row, or the default value for the element type.</returns>
    public Task<TResult?> LastOrDefaultAsync(CancellationToken cancellationToken, params object[] @params) => ForLast().FirstOrDefaultAsync(cancellationToken, @params);
    /// <summary>Returns a clone of this command with a 1-based projected column added to its sort order.</summary>
    /// <param name="columnIndex">The 1-based index of the projected column to sort by; must be greater than zero.</param>
    /// <param name="direction">The sort direction.</param>
    /// <returns>A new command carrying the added sort.</returns>
    public QueryCommand<TResult> OrderBy(int columnIndex, OrderDirection direction)
    {
        if (columnIndex < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIndex));

        var cmd = new QueryCommand<TResult>(_dataContext, Definition with
        {
            Sorting = _sorting is null
                ? [new Sorting(columnIndex) { Direction = direction }]
                : [.. _sorting, new Sorting(columnIndex) { Direction = direction }],
        });

        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    /// <summary>Returns a clone of this command with the 1-based column added to its sort order in ascending direction.</summary>
    /// <param name="columnIndex">The 1-based index of the projected column to sort by; must be greater than zero.</param>
    /// <returns>A new command carrying the added sort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderBy(int columnIndex) => OrderBy(columnIndex, OrderDirection.Asc);
    /// <summary>Returns a clone of this command with the 1-based column added to its sort order in descending direction.</summary>
    /// <param name="columnIndex">The 1-based index of the projected column to sort by; must be greater than zero.</param>
    /// <returns>A new command carrying the added sort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderByDescending(int columnIndex) => OrderBy(columnIndex, OrderDirection.Desc);
    /// <summary>
    /// Returns a clone of this command with <paramref name="orderExp"/> added to its sort order. The
    /// expression is written over the projected result type (<typeparamref name="TResult"/>); each
    /// member is resolved to the projection expression that produced the corresponding output column.
    /// Repeated calls append keys, so the existing order is preserved.
    /// </summary>
    /// <param name="orderExp">The key selector over the projected result.</param>
    /// <param name="direction">The sort direction.</param>
    /// <returns>A new command carrying the added sort.</returns>
    public QueryCommand<TResult> OrderBy(Expression<Func<TResult, object?>> orderExp, OrderDirection direction)
    {
        ArgumentNullException.ThrowIfNull(orderExp);

        var sorting = new Sorting(orderExp) { Direction = direction };
        var cmd = new QueryCommand<TResult>(_dataContext, Definition with
        {
            Sorting = _sorting is null ? [sorting] : [.. _sorting, sorting],
        });

        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    /// <summary>Returns a clone of this command with <paramref name="orderExp"/> added to its sort order in ascending direction.</summary>
    /// <param name="orderExp">The key selector over the projected result.</param>
    /// <returns>A new command carrying the added sort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderBy(Expression<Func<TResult, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Asc);
    /// <summary>Returns a clone of this command with <paramref name="orderExp"/> added to its sort order in descending direction.</summary>
    /// <param name="orderExp">The key selector over the projected result.</param>
    /// <returns>A new command carrying the added sort.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderByDescending(Expression<Func<TResult, object?>> orderExp) => OrderBy(orderExp, OrderDirection.Desc);
    /// <summary>
    /// Returns a clone of this command with the maximum number of returned rows set. Unlike the
    /// builder-level limit this applies after the projection, so a grouped/aggregated query does not
    /// need the aggregate repeated in an outer query.
    /// </summary>
    /// <param name="limit">The maximum number of rows to return; zero means no limit.</param>
    /// <returns>A new command carrying the limit.</returns>
    public QueryCommand<TResult> Limit(int limit)
    {
        var paging = Paging;
        paging.Limit = limit;
        return WithPaging(paging);
    }
    /// <summary>Returns a clone of this command with the number of leading rows to skip set.</summary>
    /// <param name="offset">The number of leading rows to skip; zero starts from the first row.</param>
    /// <returns>A new command carrying the offset.</returns>
    public QueryCommand<TResult> Offset(int offset)
    {
        var paging = Paging;
        paging.Offset = offset;
        return WithPaging(paging);
    }
    /// <summary>Returns a clone of this command with both the page size and the start position set.</summary>
    /// <param name="limit">The maximum number of rows to return; zero means no limit.</param>
    /// <param name="offset">The number of leading rows to skip; zero starts from the first row.</param>
    /// <returns>A new command carrying the limit and offset.</returns>
    public QueryCommand<TResult> Page(int limit, int offset)
    {
        var paging = Paging;
        paging.Limit = limit;
        paging.Offset = offset;
        return WithPaging(paging);
    }
    private QueryCommand<TResult> WithPaging(Paging paging)
    {
        var cmd = new QueryCommand<TResult>(_dataContext, Definition);
        CopyTo(cmd, true);
        cmd.Paging = paging;
        cmd.ResetPreparation();
        return cmd;
    }
    /// <summary>Returns a clone of this command that emits <c>DISTINCT</c>, removing duplicate rows from the result.</summary>
    /// <returns>A new command marked distinct.</returns>
    public QueryCommand<TResult> Distinct()
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.IsDistinct = true;
        return cmd;
    }
    /// <summary>
    /// Returns a new command carrying statement-level query hints, for example SQL Server
    /// <c>"recompile"</c> (rendered as <c>OPTION (recompile)</c>). Repeated calls accumulate hints.
    /// A dialect that does not support query hints rejects a command that carries them when its SQL
    /// is built.
    /// </summary>
    public QueryCommand<TResult> Hint(params string[] hints)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        var source = cmd._from;
        cmd.ResetPreparation();
        cmd._from = source;
        cmd.AddHints(hints);
        return cmd;
    }
    /// <summary>
    /// Overrides identifier quoting for this command: when <paramref name="value"/> is <c>true</c>,
    /// physical table and column names are quoted with the provider's delimiter (<c>"id"</c> on
    /// PostgreSQL/SQLite, <c>[id]</c> on SQL Server, `` `id` `` on MySQL/MariaDB/ClickHouse); when
    /// <c>false</c>, names are emitted verbatim. Without a call the command inherits the context
    /// default set with <c>DataContextBuilder.UseQuotedIdentifiers</c>.
    /// </summary>
    public QueryCommand<TResult> WithQuotedIdentifiers(bool value = true)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        var source = cmd._from;
        cmd.ResetPreparation();
        cmd._from = source;
        cmd.QuoteIdentifiers = value;
        return cmd;
    }
    /// <summary>
    /// Overrides the naming convention for this command: auto-derived table and column names are
    /// translated through <paramref name="convention"/> (for example
    /// <see cref="SnakeCaseNamingConvention.Instance"/>), while names declared with
    /// <c>[SqlTable]</c>/<c>[Column]</c> or a fluent mapping stay verbatim. Without a call the command
    /// inherits the context default set with <c>DataContextBuilder.UseNamingConvention</c>.
    /// </summary>
    public QueryCommand<TResult> WithNamingConvention(INamingConvention? convention)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        var source = cmd._from;
        cmd.ResetPreparation();
        cmd._from = source;
        cmd.NamingConvention = convention;
        return cmd;
    }
    /// <summary>
    /// Overrides the letter case in which this command's SQL keywords are emitted: <c>Upper</c> renders
    /// <c>SELECT ... FROM ...</c>, <c>Lower</c> renders <c>select ... from ...</c>. Without a call the
    /// command inherits the context default set with <c>DataContextBuilder.UseKeywordCase</c>.
    /// Identifiers, string literals, function names, type names and raw SQL are never affected.
    /// </summary>
    public QueryCommand<TResult> WithKeywordCase(KeywordCase keywordCase = global::NextORM.Core.KeywordCase.Upper)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        var source = cmd._from;
        cmd.ResetPreparation();
        cmd._from = source;
        cmd.KeywordCase = keywordCase;
        return cmd;
    }
    /// <summary>
    /// Attaches a SQL Server <c>FOR JSON</c> clause so the whole result set is returned as one JSON
    /// document (<c>FOR JSON PATH</c> by default), and returns the command for further composition or
    /// SQL inspection. Prefer <see cref="ForJson(ForJsonMode, string, bool, object[])"/> to execute
    /// the query and read the document. A dialect that does not support the clause rejects the command
    /// when its SQL is built.
    /// </summary>
    /// <param name="mode">Whether the output is shaped by the projection aliases (<see cref="ForJsonMode.Path"/>) or the table structure (<see cref="ForJsonMode.Auto"/>).</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="includeNullValues">Whether null-valued properties are emitted (<c>INCLUDE_NULL_VALUES</c>).</param>
    /// <returns>A new command carrying the <c>FOR JSON</c> clause.</returns>
    public QueryCommand<TResult> WithForJson(ForJsonMode mode = ForJsonMode.Path, string? root = null, bool includeNullValues = false)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.ForJsonClause = new ForJsonClause(mode, root, includeNullValues);
        return cmd;
    }
    /// <summary>
    /// Attaches a SQL Server <c>FOR XML</c> clause so the whole result set is returned as one XML
    /// document (<c>FOR XML PATH</c> by default), and returns the command for further composition or
    /// SQL inspection. Prefer <see cref="ForXml(ForXmlMode, string, string, bool, object[])"/> to
    /// execute the query and read the document. A dialect that does not support the clause rejects the
    /// command when its SQL is built, and combining it with <see cref="WithForJson"/> throws.
    /// </summary>
    /// <param name="mode">The <c>FOR XML</c> shaping mode (<c>RAW</c>, <c>AUTO</c>, <c>EXPLICIT</c> or <c>PATH</c>).</param>
    /// <param name="elementName">The row element name for <c>RAW</c>/<c>PATH</c>, or <c>null</c> for the default.</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="elements">Whether columns are emitted as child elements (<c>ELEMENTS</c>).</param>
    /// <returns>A new command carrying the <c>FOR XML</c> clause.</returns>
    public QueryCommand<TResult> WithForXml(ForXmlMode mode = ForXmlMode.Path, string? elementName = null, string? root = null, bool elements = false)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.ForXmlClause = new ForXmlClause(mode, elementName, root, elements);
        return cmd;
    }
    /// <summary>
    /// Executes the query and returns the whole result set as one JSON document (SQL Server
    /// <c>FOR JSON</c>). The projection drives the document shape in <see cref="ForJsonMode.Path"/> and
    /// the table structure drives it in <see cref="ForJsonMode.Auto"/>; the query's own element type is
    /// irrelevant because the database returns a single document column. This is a terminal operator,
    /// so it does not apply an implicit <c>TOP 1</c>: the document covers the entire result set.
    /// </summary>
    /// <param name="mode">Whether the output is shaped by the projection aliases (<see cref="ForJsonMode.Path"/>) or the table structure (<see cref="ForJsonMode.Auto"/>).</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="includeNullValues">Whether null-valued properties are emitted (<c>INCLUDE_NULL_VALUES</c>).</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The JSON document, or <c>null</c> when the query produced no rows (SQL Server <c>FOR JSON</c> yields SQL NULL for an empty result set).</returns>
    public string? ForJson(ForJsonMode mode = ForJsonMode.Path, string? root = null, bool includeNullValues = false, params object[]? @params)
        => ExecuteDocument(WithForJson(mode, root, includeNullValues), @params);
    /// <summary>Asynchronously executes the query and returns the whole result set as one JSON document (SQL Server <c>FOR JSON</c>).</summary>
    /// <param name="mode">Whether the output is shaped by the projection aliases (<see cref="ForJsonMode.Path"/>) or the table structure (<see cref="ForJsonMode.Auto"/>).</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="includeNullValues">Whether null-valued properties are emitted (<c>INCLUDE_NULL_VALUES</c>).</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the JSON document, or <c>null</c> when the query produced no rows.</returns>
    public Task<string?> ForJsonAsync(ForJsonMode mode = ForJsonMode.Path, string? root = null, bool includeNullValues = false, CancellationToken cancellationToken = default, params object[]? @params)
        => ExecuteDocumentAsync(WithForJson(mode, root, includeNullValues), @params, cancellationToken);
    /// <summary>
    /// Executes the query and returns the whole result set as one XML document (SQL Server
    /// <c>FOR XML</c>). This is a terminal operator, so it does not apply an implicit <c>TOP 1</c>: the
    /// document covers the entire result set.
    /// </summary>
    /// <param name="mode">The <c>FOR XML</c> shaping mode (<c>RAW</c>, <c>AUTO</c>, <c>EXPLICIT</c> or <c>PATH</c>).</param>
    /// <param name="elementName">The row element name for <c>RAW</c>/<c>PATH</c>, or <c>null</c> for the default.</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="elements">Whether columns are emitted as child elements (<c>ELEMENTS</c>).</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>The XML document, or <c>null</c> when the query produced no rows.</returns>
    public string? ForXml(ForXmlMode mode = ForXmlMode.Path, string? elementName = null, string? root = null, bool elements = false, params object[]? @params)
        => ExecuteDocument(WithForXml(mode, elementName, root, elements), @params);
    /// <summary>Asynchronously executes the query and returns the whole result set as one XML document (SQL Server <c>FOR XML</c>).</summary>
    /// <param name="mode">The <c>FOR XML</c> shaping mode (<c>RAW</c>, <c>AUTO</c>, <c>EXPLICIT</c> or <c>PATH</c>).</param>
    /// <param name="elementName">The row element name for <c>RAW</c>/<c>PATH</c>, or <c>null</c> for the default.</param>
    /// <param name="root">An optional <c>ROOT('name')</c> wrapper around the document, or <c>null</c> for none.</param>
    /// <param name="elements">Whether columns are emitted as child elements (<c>ELEMENTS</c>).</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <param name="params">Positional parameter values, bound in the order they appear in the SQL.</param>
    /// <returns>A task producing the XML document, or <c>null</c> when the query produced no rows.</returns>
    public Task<string?> ForXmlAsync(ForXmlMode mode = ForXmlMode.Path, string? elementName = null, string? root = null, bool elements = false, CancellationToken cancellationToken = default, params object[]? @params)
        => ExecuteDocumentAsync(WithForXml(mode, elementName, root, elements), @params, cancellationToken);

    private string? ExecuteDocument(QueryCommand<TResult> documentCommand, object[]? @params)
    {
        object[] boundParams = @params ?? [];
        return CreateDocumentCommand(documentCommand).ExecuteScalar(boundParams);
    }

    private async Task<string?> ExecuteDocumentAsync(QueryCommand<TResult> documentCommand, object[]? @params, CancellationToken cancellationToken)
    {
        object[] boundParams = @params ?? [];
        return await CreateDocumentCommand(documentCommand).ExecuteScalarAsync(cancellationToken, boundParams).ConfigureAwait(false);
    }

    private QueryCommand<string> CreateDocumentCommand(QueryCommand<TResult> configured)
    {
        if (_dataContext is not NextORM.Core.DataContext)
            throw new NotSupportedException("FOR JSON / FOR XML requires a relational database provider.");

        var cmd = new QueryCommand<string>(_dataContext, configured.Definition);
        configured.CopyTo(cmd, true);
        cmd.ForJsonClause = configured.ForJsonClause;
        cmd.ForXmlClause = configured.ForXmlClause;
        cmd.DocumentMode = true;
        cmd.ResetPreparation();
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>UNION</c> (duplicates removed).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> Union<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Distinct);
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>UNION ALL</c> (duplicates preserved).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> UnionAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.All);
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>INTERSECT</c> (distinct rows present in both operands).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> Intersect<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Intersect);
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>INTERSECT ALL</c> (duplicates preserved).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> IntersectAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.IntersectAll);
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>EXCEPT</c> (distinct rows of this command not present in the other).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> Except<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Except);
        return cmd;
    }
    /// <summary>Returns a clone of this command combined with <paramref name="queryCommand"/> through <c>EXCEPT ALL</c> (duplicates preserved).</summary>
    /// <typeparam name="T">The project type of the second operand.</typeparam>
    /// <param name="queryCommand">The second operand of the set operation.</param>
    /// <returns>A new command carrying the set operation.</returns>
    public QueryCommand<TResult> ExceptAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.ExceptAll);
        return cmd;
    }

    /// <summary>
    /// Clone of this command with the set operation removed, so the first operand of a
    /// <c>UNION</c>/<c>INTERSECT</c>/<c>EXCEPT</c> can be executed independently.
    /// </summary>
    internal QueryCommand<TResult> CloneWithoutUnion()
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ClearUnion();
        cmd.ResetPreparation();
        return cmd;
    }

}
