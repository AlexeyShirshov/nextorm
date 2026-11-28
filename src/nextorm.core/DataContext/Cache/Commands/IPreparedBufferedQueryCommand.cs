namespace NextORM.Core;

/// <summary>A query prepared for repeated execution that materializes results.</summary>
/// <typeparam name="TResult">The type of a result row.</typeparam>
public interface IPreparedBufferedQueryCommand<TResult>
{
    /// <summary>
    /// Materializes the result rows into a list using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the materialized result rows.</returns>
    Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Materializes the result rows into a list, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result rows.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the materialized result rows.</returns>
    Task<List<TResult>> ToListAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Materializes the result rows into a list.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The materialized result rows.</returns>
    List<TResult> ToList(IDataContext dataContext, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Executes the query and returns its scalar value using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the scalar value, or <see langword="null"/> when none was returned.</returns>
    Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, params object[]? @params);

    /// <summary>
    /// Executes the query and returns its scalar value, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="cancellationToken">Cancels execution of the query.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the scalar value, or <see langword="null"/> when none was returned.</returns>
    Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Executes the query and returns its scalar value.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The scalar value, or <see langword="null"/> when none was returned.</returns>
    TResult? ExecuteScalar(IDataContext dataContext, bool throwIfNull, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Returns the first result row, throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row.</returns>
    TResult First(IDataContext dataContext, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Returns the first result row asynchronously using <see cref="CancellationToken.None"/>, throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row.</returns>
    Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Returns the first result row asynchronously, honouring a cancellation token and throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row.</returns>
    Task<TResult> FirstAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Returns the first result row, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    TResult? FirstOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Returns the first result row asynchronously using <see cref="CancellationToken.None"/>, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Returns the first result row asynchronously, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Returns the single result row, throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row.</returns>
    TResult Single(IDataContext dataContext, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Returns the single result row asynchronously using <see cref="CancellationToken.None"/>, throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row.</returns>
    Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Returns the single result row asynchronously, honouring a cancellation token and throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row.</returns>
    Task<TResult> SingleAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Returns the single result row, or the default value when the query returns none, throwing when it returns more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    TResult? SingleOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params);

    /// <summary>
    /// Returns the single result row asynchronously using <see cref="CancellationToken.None"/>, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Returns the single result row asynchronously, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);
}

internal sealed class PreparedBufferedQueryCommand<TResult>(IPreparedQueryCommand<TResult> command)
    : IPreparedBufferedQueryCommand<TResult>
{
    internal IPreparedQueryCommand<TResult> Command { get; } = command;

    public Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params)
        => Command.ToListAsync(dataContext, @params);

    public Task<List<TResult>> ToListAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.ToListAsync(dataContext, cancellationToken, @params);

    public List<TResult> ToList(IDataContext dataContext, params ReadOnlySpan<object?> @params)
        => Command.ToList(dataContext, @params);

    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, params object[]? @params)
        => Command.ExecuteScalarAsync(dataContext, throwIfNull, @params);

    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, CancellationToken cancellationToken, params object[]? @params)
        => Command.ExecuteScalarAsync(dataContext, throwIfNull, cancellationToken, @params);

    public TResult? ExecuteScalar(IDataContext dataContext, bool throwIfNull, params ReadOnlySpan<object?> @params)
        => Command.ExecuteScalar(dataContext, throwIfNull, @params);

    public TResult First(IDataContext dataContext, params ReadOnlySpan<object?> @params)
        => Command.First(dataContext, @params);

    public Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params)
        => Command.FirstAsync(dataContext, @params);

    public Task<TResult> FirstAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.FirstAsync(dataContext, cancellationToken, @params);

    public TResult? FirstOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params)
        => Command.FirstOrDefault(dataContext, @params);

    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params)
        => Command.FirstOrDefaultAsync(dataContext, @params);

    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.FirstOrDefaultAsync(dataContext, cancellationToken, @params);

    public TResult Single(IDataContext dataContext, params ReadOnlySpan<object?> @params)
        => Command.Single(dataContext, @params);

    public Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params)
        => Command.SingleAsync(dataContext, @params);

    public Task<TResult> SingleAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.SingleAsync(dataContext, cancellationToken, @params);

    public TResult? SingleOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params)
        => Command.SingleOrDefault(dataContext, @params);

    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params)
        => Command.SingleOrDefaultAsync(dataContext, @params);

    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.SingleOrDefaultAsync(dataContext, cancellationToken, @params);
}
