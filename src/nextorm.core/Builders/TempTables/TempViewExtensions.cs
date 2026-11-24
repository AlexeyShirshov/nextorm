namespace NextORM.Core;

/// <summary>Creates session-local views on SQLite and PostgreSQL without materializing rows.</summary>
public static class TempViewExtensions
{
    /// <summary>Defines a temporary view over the builder without executing SQL.</summary>
    /// <typeparam name="TResult">The defining query's projected row type.</typeparam>
    /// <param name="builder">The query builder whose projection defines the view.</param>
    /// <returns>An automatically named source to pass to <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TempViewSource<TResult> AsTempView<TResult>(this EntityBuilder<TResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ToCommand().AsTempView();
    }

    /// <summary>Defines a temporary view over the query without executing SQL.</summary>
    /// <typeparam name="TResult">The defining query's projected row type.</typeparam>
    /// <param name="query">The query whose projection defines the view. The definition cannot use bound SQL parameters.</param>
    /// <returns>An automatically named source to pass to <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public static TempViewSource<TResult> AsTempView<TResult>(this QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new TempViewSource<TResult>(query);
    }

    /// <summary>
    /// Starts a query over a source created with <c>AsTempView()</c>. When the query runs on SQLite or
    /// PostgreSQL, one batch creates, reads, and drops the view on the same session. Use
    /// <see cref="TableAlias"/> accessors to select the view's projected columns by name.
    /// </summary>
    /// <typeparam name="TResult">The view's projected row type.</typeparam>
    /// <param name="dataContext">The context that owns the defining query.</param>
    /// <param name="tempView">The deferred view to create and read.</param>
    /// <returns>A builder for composing a query over the view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dataContext"/> or <paramref name="tempView"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The defining query is not bound to <paramref name="dataContext"/>.</exception>
    /// <exception cref="NotSupportedException">The context cannot execute a batch.</exception>
    public static EntityBuilder<TableAlias> From<TResult>(this IDataContext dataContext, TempViewSource<TResult> tempView)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(tempView);

        if (dataContext is not IBatchExecutor)
            throw new NotSupportedException("Temporary views are supported only by SQLite and PostgreSQL.");
        if (!ReferenceEquals(tempView.Source.DataContext, dataContext))
            throw new ArgumentException("The temporary view must be defined on this data context.", nameof(tempView));

        return new EntityBuilder<TableAlias>(dataContext) { Logger = dataContext.CommandLogger, SourceFrom = new FromExpression(tempView) };
    }

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
