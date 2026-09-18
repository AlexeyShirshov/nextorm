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

    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger, bool isDistinct)
    {
        var cmd = new QueryCommand<T>(dataContext, exp, condition, joins, paging, sorting, group, having, logger)
        {
            IsDistinct = isDistinct
        };
        return cmd;
    }

    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger, bool isDistinct)
    {
        var cmd = new QueryCommand<T>(dataContext, srcType, condition, joins, paging, sorting, group, having, logger)
        {
            IsDistinct = isDistinct
        };
        return cmd;
    }

    /// <summary>
    /// Starts a query over the mapping of <typeparamref name="T"/> and returns its fluent builder.
    /// The type's metadata is resolved lazily and cached per process; <paramref name="configEntity"/>
    /// therefore runs only on the first call for <typeparamref name="T"/>.
    /// </summary>
    public static EntityBuilder<T> From<T>(this IDataContext dataContext, Action<EntityMetadataBuilder<T>>? configEntity = null)
    {
        if (!DataContextCache.Metadata.ContainsKey(typeof(T)))
        {
            var eb = new EntityMetadataBuilder<T>();
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

    /// <summary>
    /// Starts a query against a raw table (or CTE) name. Needed when the context is used through
    /// <see cref="IDataContext"/> and therefore has no concrete <c>From(string)</c> instance method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder From(this IDataContext dataContext, string table)
        => new(dataContext, table) { Logger = dataContext.CommandLogger };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, QueryCommand<TResult> query)
        => new(dataContext, query) { Logger = dataContext.CommandLogger };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, EntityBuilder<TResult> builder)
        => new(dataContext, builder) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a query from a table-valued function. <paramref name="call"/> must be a call to a static
    /// method annotated with <see cref="SqlTableFunctionAttribute"/> (or declared in an annotated
    /// type); its arguments are rendered as the function arguments and are parameterised like any
    /// other expression. The function must already exist in the target database - nextorm only emits
    /// the call.
    /// <para>
    /// Example: <c>ctx.FromTableFunction(() =&gt; Db.MyTvf(1, "x")).Select(r =&gt; new { r.Id })</c>.
    /// </para>
    /// </summary>
    public static EntityBuilder<T> FromTableFunction<T>(this IDataContext dataContext, Expression<Func<IQueryable<T>>> call)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(call);

        var body = call.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        if (body is not MethodCallExpression methodCall)
            throw new ArgumentException("The expression must be a call to a method mapped with [SqlTableFunction].", nameof(call));

        var entity = dataContext.From<T>();
        entity.SourceFrom = new FromExpression(TableFunctionExpression.Create(methodCall));
        return entity;
    }
}
