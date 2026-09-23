namespace NextORM.Core;

/// <summary>
/// Materialises a query into a table with <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c>. The row shape
/// is taken from the query's projection; the created table is addressed later with
/// <see cref="DataContextExtensions.From(IDataContext, string)"/>. Unlike a common table expression,
/// which is reusable only within one statement, the materialised table is reusable across subsequent
/// queries on the same connection.
/// <para>
/// A temporary table is session-scoped: create and read it on one context (one connection). The
/// terminal issues exactly one command and returns no row count — <c>CREATE TABLE AS SELECT</c> reports
/// no meaningful affected rows. Providers without the form (SQL Server, ClickHouse) and the in-memory
/// context reject it with <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Every terminal is offered both on a query builder (before a projection) and on a built
/// <see cref="QueryCommand{TResult}"/> (after <c>Select</c>), so a projection can be materialised too.
/// </para>
/// </summary>
public static class TempTableExtensions
{
    /// <summary>Materialises a builder's query into the temporary table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTempTable<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: true, options);
    }

    /// <summary>Materialises a built query into the temporary table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTempTable<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null)
        => Execute(ContextOf(query), query, name, temporary: true, options);

    /// <summary>Asynchronously materialises a builder's query into a temporary table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: true, options, cancellationToken);
    }

    /// <summary>Asynchronously materialises a built query into a temporary table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: true, options, cancellationToken);

    /// <summary>Renders the temporary-table statement a builder's query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: true, options);
    }

    /// <summary>Renders the temporary-table statement a built query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null)
        => Render(ContextOf(query), query, name, temporary: true, options);

    /// <summary>Materialises a builder's query into the persistent table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableAsOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: false, options);
    }

    /// <summary>Materialises a built query into the persistent table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableAsOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null)
        => Execute(ContextOf(query), query, name, temporary: false, options);

    /// <summary>Asynchronously materialises a builder's query into a persistent table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableAsOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: false, options, cancellationToken);
    }

    /// <summary>Asynchronously materialises a built query into a persistent table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableAsOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: false, options, cancellationToken);

    /// <summary>Renders the persistent-table statement a builder's query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableAsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: false, options);
    }

    /// <summary>Renders the persistent-table statement a built query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this QueryCommand<TResult> query, string name, CreateTableAsOptions? options = null)
        => Render(ContextOf(query), query, name, temporary: false, options);

    private static void Execute<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableAsOptions? options)
    {
        var command = BuildCommand(source, name, temporary, options);
        RequireExecutor(context).Execute(command);
    }

    private static Task ExecuteAsync<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableAsOptions? options, CancellationToken cancellationToken)
    {
        var command = BuildCommand(source, name, temporary, options);
        return RequireExecutor(context).Execute(command, cancellationToken);
    }

    private static string Render<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableAsOptions? options)
    {
        var command = BuildCommand(source, name, temporary, options);
        return RequireExecutor(context).Render(command);
    }

    private static CreateTableAsCommand BuildCommand<TResult>(QueryCommand<TResult> source, string name, bool temporary, CreateTableAsOptions? options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return new CreateTableAsCommand(typeof(TResult), name, temporary, source, options ?? new CreateTableAsOptions());
    }

    private static IDataContext ContextOf<TResult>(QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.DataContext
            ?? throw new NotSupportedException("The query is not bound to a data context; build it from a context with From<T>().");
    }

    private static IMutationExecutor RequireExecutor(IDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{context.GetType().Name} does not support data definition. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
    }
}
