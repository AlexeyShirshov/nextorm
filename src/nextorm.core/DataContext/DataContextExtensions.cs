using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Provider-independent helpers and convenience overloads for <see cref="IDataContext"/>.
/// These used to be default interface methods; keeping them outside the interface leaves the
/// provider-implemented contract (the role interfaces) free of engine-independent behavior.
/// </summary>
public static class DataContextExtensions
{
    /// <summary>
    /// Creates a typed query command from a <see cref="QueryDefinition"/>. This is the single factory
    /// the builders use; it replaces the previous overloads that took the whole query shape as a long
    /// parameter list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, QueryDefinition definition)
        => new(dataContext, definition);

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

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row. Stops after the first
    /// matching row rather than materializing the full result.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, cancellationToken);

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row, using no parameters.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, null, true, cancellationToken);

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, CancellationToken.None);

    /// <summary>
    /// Synchronously determines whether the query produces at least one row.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params ReadOnlySpan<object?> @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true);

    /// <summary>
    /// Asynchronously executes the query and materializes every row into a new list.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>A list containing the materialized rows; empty when the query matches nothing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<List<TResult>> ToListAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.ToListAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Synchronously executes the query and materializes every row into a new list, using no parameters.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <returns>A list containing the materialized rows; empty when the query matches nothing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TResult> ToList<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand)
        => dataContext.ToList(preparedQueryCommand, ReadOnlySpan<object?>.Empty);

    /// <summary>
    /// Asynchronously returns the first row of the query.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The first materialized row.</returns>
    /// <exception cref="InvalidOperationException">The query returns no rows.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> FirstAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the first row of the query, or the default value of
    /// <typeparamref name="TResult"/> when the query returns no rows.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The first materialized row, or <see langword="default"/> when there are none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> FirstOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the only row of the query.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The single materialized row.</returns>
    /// <exception cref="InvalidOperationException">The query returns no rows or more than one row.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> SingleAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the only row of the query, or the default value of
    /// <typeparamref name="TResult"/> when the query returns no rows.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The single materialized row, or <see langword="default"/> when there are none.</returns>
    /// <exception cref="InvalidOperationException">The query returns more than one row.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> SingleOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Starts a query against a raw table (or CTE) name. Needed when the context is used through
    /// <see cref="IDataContext"/> and therefore has no concrete <c>From(string)</c> instance method.
    /// <para>
    /// Columns are read through <see cref="TableAlias"/> accessors
    /// (<c>t.GetInt64("id")</c> / <c>t["id"].AsInt</c>). The returned builder is generic so that the
    /// full operator set (<c>Where</c>/<c>Join</c>/<c>GroupBy</c>/<c>Having</c>/<c>OrderBy</c>/
    /// <c>Limit</c>/<c>Select</c>) is available, unlike the previous non-generic shape.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TableAlias> From(this IDataContext dataContext, string table)
        => new(dataContext, table) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a query from a raw SQL fragment used as a composable <c>FROM</c> source: it is rendered as
    /// <c>(&lt;sql&gt;) AS alias</c> and can be filtered, joined, projected, grouped and paged further.
    /// Columns are read through <see cref="TableAlias"/> accessors (<c>t["id"].AsInt</c>).
    /// <paramref name="parameters"/> is an object whose public properties become the named parameters
    /// referenced by the SQL (the same convention as <c>WithSql</c>). Requires a provider that supports
    /// a derived-table <c>FROM</c> source (<see cref="ISqlDialect.SupportsRawSqlSource"/>).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The active provider reports <see cref="ISqlDialect.SupportsRawSqlSource"/> as <c>false</c>, or the
    /// context is the in-memory provider (which cannot evaluate raw SQL).
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TableAlias> FromSql(this IDataContext dataContext, string sql, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        return new EntityBuilder<TableAlias>(dataContext) { Logger = dataContext.CommandLogger, SourceFrom = new FromExpression(new RawSqlSourceExpression(sql, parameters)) };
    }

    /// <summary>
    /// Starts a query from an existing <see cref="QueryCommand{TResult}"/> and returns a fluent
    /// builder over its definition.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="query">The command whose definition drives the query.</param>
    /// <returns>A builder for composing the query further.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, QueryCommand<TResult> query)
        => new(dataContext, query) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a new query builder that shares the source and definition of an existing builder.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="builder">The builder to copy the query shape from.</param>
    /// <returns>A new builder bound to <paramref name="dataContext"/>.</returns>
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

    /// <summary>Starts a CTE scope with a single non-recursive declaration.</summary>
    public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query)
        => new CteQuery(dataContext, [new CteDefinition(name, query)]);

    /// <summary>
    /// Starts a CTE scope with a single recursive declaration. <paramref name="maxRecursion"/>
    /// is rendered only by dialects that expose a depth option (SQL Server).
    /// </summary>
    public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query, int? maxRecursion = null)
        => new CteQuery(dataContext, [new CteDefinition(name, query, true, maxRecursion)]);
}
