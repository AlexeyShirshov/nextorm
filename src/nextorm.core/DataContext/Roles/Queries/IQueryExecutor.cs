namespace NextORM.Core;

/// <summary>
/// Executes a prepared query command and materializes its result.
/// The convenience overloads (parameterless <c>ToList</c>, <c>Any</c>, cancellation-only
/// variants) live in <see cref="DataContextExtensions"/>.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Asynchronously materializes all result rows into a list.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels reading of the result rows.</param>
    /// <returns>A task producing the materialized result rows.</returns>
    Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    /// <summary>
    /// Materializes all result rows into a list.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The materialized result rows.</returns>
    List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params);
    /// <summary>
    /// Asynchronously executes the query and returns its scalar value.
    /// </summary>
    /// <typeparam name="TResult">The scalar result type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="cancellationToken">Cancels execution of the query.</param>
    /// <returns>A task producing the scalar value, or <see langword="null"/> when none was returned.</returns>
    Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken);
    /// <summary>
    /// Executes the query and returns its scalar value.
    /// </summary>
    /// <typeparam name="TResult">The scalar result type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <returns>The scalar value, or <see langword="null"/> when none was returned.</returns>
    TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull);
    /// <summary>
    /// Returns the first result row, throwing when the query returns none.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row.</returns>
    TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params);
    /// <summary>
    /// Asynchronously returns the first result row, throwing when the query returns none.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <returns>A task producing the first result row.</returns>
    Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    /// <summary>
    /// Returns the first result row, or the default value when the query returns none.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params);
    /// <summary>
    /// Asynchronously returns the first result row, or the default value when the query returns none.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <returns>A task producing the first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    /// <summary>
    /// Returns the single result row, throwing when the query returns none or more than one.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row.</returns>
    TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params);
    /// <summary>
    /// Asynchronously returns the single result row, throwing when the query returns none or more than one.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <returns>A task producing the single result row.</returns>
    Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    /// <summary>
    /// Returns the single result row, or the default value when the query returns none, throwing when it returns more than one.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params);
    /// <summary>
    /// Asynchronously returns the single result row, or the default value when the query returns none.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to execute.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <returns>A task producing the single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
}
