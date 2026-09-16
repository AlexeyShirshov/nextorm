using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

/// <summary>
/// Provider-independent helpers and convenience overloads for <see cref="IDataContext"/>.
/// These used to be default interface methods; keeping them outside the interface leaves the
/// provider-implemented contract (the role interfaces) free of engine-independent behavior.
/// </summary>
public static class DataContextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        => new(dataContext, exp, condition, joins, paging, sorting, group, having, logger);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        => new(dataContext, srcType, condition, joins, paging, sorting, group, having, logger);

    public static Entity<T> Create<T>(this IDataContext dataContext, Action<EntityBuilder<T>>? configEntity = null)
    {
        if (!DataContextCache.Metadata.ContainsKey(typeof(T)))
        {
            var eb = new EntityBuilder<T>();
            configEntity?.Invoke(eb);
            DataContextCache.Metadata[typeof(T)] = eb.Build();
        }
        return new(dataContext) { Logger = dataContext.CommandLogger };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, null, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params ReadOnlySpan<object?> @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<List<TResult>> ToListAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.ToListAsync(preparedQueryCommand, @params, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TResult> ToList<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand)
        => dataContext.ToList(preparedQueryCommand, ReadOnlySpan<object?>.Empty);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> FirstAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstAsync(preparedQueryCommand, @params, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> FirstOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> SingleAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleAsync(preparedQueryCommand, @params, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> SingleOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity<TResult> From<TResult>(this IDataContext dataContext, QueryCommand<TResult> query)
        => new(dataContext, query) { Logger = dataContext.CommandLogger };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity<TResult> From<TResult>(this IDataContext dataContext, Entity<TResult> builder)
        => new(dataContext, builder) { Logger = dataContext.CommandLogger };
}
