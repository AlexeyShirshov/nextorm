namespace NextORM.Core;

/// <summary>Creates session-local views on SQLite and PostgreSQL without materializing rows.</summary>
public static class TempViewExtensions
{
    /// <summary>Creates a temporary view containing all columns of the builder's query.</summary>
    /// <param name="builder">The query builder.</param>
    /// <param name="name">The raw, unqualified view name, without identifier quotes.</param>
    /// <typeparam name="TResult">The query row type.</typeparam>
    /// <returns>A task that completes after the view is created.</returns>
    public static Task ToTempView<TResult>(this EntityBuilder<TResult> builder, string name)
        => builder.ToTempView(name, CancellationToken.None);

    /// <summary>Creates a temporary view with the given query's projection, on its database session.</summary>
    /// <param name="query">The query that defines the view.</param>
    /// <param name="name">The raw, unqualified view name, without identifier quotes.</param>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <returns>A task that completes after the view is created.</returns>
    public static Task ToTempView<TResult>(this QueryCommand<TResult> query, string name)
        => query.ToTempView(name, CancellationToken.None);

    /// <summary>Creates a temporary view containing all columns of the builder's query.</summary>
    /// <param name="builder">The query builder.</param>
    /// <param name="name">The raw, unqualified view name, without identifier quotes.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <typeparam name="TResult">The query row type.</typeparam>
    /// <returns>A task that completes after the view is created.</returns>
    public static Task ToTempView<TResult>(this EntityBuilder<TResult> builder, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ToCommand().ToTempView(name, cancellationToken);
    }

    /// <summary>Creates a temporary view with the given query's projection, on its database session.</summary>
    /// <param name="query">The query that defines the view.</param>
    /// <param name="name">The raw, unqualified view name, without identifier quotes.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <returns>A task that completes after the view is created.</returns>
    /// <exception cref="NotSupportedException">The provider does not support temporary views, the query is unbound, or its body uses SQL parameters.</exception>
    public static Task ToTempView<TResult>(this QueryCommand<TResult> query, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('\0'))
            throw new ArgumentException("A view name cannot contain a NUL character.", nameof(name));
        if (query.DataContext is not IMutationExecutor executor)
            throw new NotSupportedException("Temporary views require a query bound to a SQLite or PostgreSQL context.");

        return executor.Execute(new CreateTempViewCommand(typeof(TResult), name, query), cancellationToken);
    }
}
