namespace NextORM.Core;

/// <summary>
/// Materialises a query into a table: <c>CREATE TABLE ... AS SELECT</c> (PostgreSQL, SQLite, MySQL, MariaDB),
/// <c>SELECT ... INTO</c> (SQL Server) or <c>CREATE TABLE ... ENGINE = MergeTree ... AS SELECT</c>
/// (ClickHouse). The row shape is taken from the query's projection; the created table is addressed later
/// with <see cref="DataContextExtensions.From(IDataContext, string)"/>. Unlike a common table expression,
/// which is reusable only within one statement, the materialised table is reusable across subsequent
/// queries on the same connection.
/// <para>
/// The temporary form (<c>ToTempTable</c>) is session-scoped: create and read it on one context (one
/// connection). It is available on PostgreSQL, SQLite, MySQL and MariaDB only — SQL Server has no
/// <c>CREATE TEMPORARY TABLE ... AS SELECT</c> (a session-scoped table is <c>ToTable("#name")</c>) and
/// ClickHouse cannot express a temporary <c>AS SELECT</c>. The terminal issues exactly one command and
/// returns no row count; the in-memory context rejects it with <see cref="NotSupportedException"/>.
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

    /// <summary>
    /// Materialises a builder's query into an automatically named temporary table and returns the
    /// generated name. The name is a raw (unquoted) identifier — the naming convention is not applied —
    /// and is read back with <c>From(name)</c> on the same context.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <returns>The generated temporary-table name.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement.</exception>
    public static string ToTempTable<TResult>(this EntityBuilder<TResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteNamed(builder.DataProvider, builder.ToCommand(), temporary: true, options: null);
    }

    /// <summary>
    /// Materialises a builder's query into an automatically named temporary table with
    /// <paramref name="options"/> and returns the generated name.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="options">The statement options; must not be <see langword="null"/>.</param>
    /// <returns>The generated temporary-table name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTable<TResult>(this EntityBuilder<TResult> builder, CreateTableAsOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteNamed(builder.DataProvider, builder.ToCommand(), temporary: true, options);
    }

    /// <summary>
    /// Materialises a built query into an automatically named temporary table and returns the generated
    /// name. The name is a raw (unquoted) identifier — the naming convention is not applied — and is read
    /// back with <c>From(name)</c> on the same context.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <returns>The generated temporary-table name.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement.</exception>
    public static string ToTempTable<TResult>(this QueryCommand<TResult> query)
        => ExecuteNamed(ContextOf(query), query, temporary: true, options: null);

    /// <summary>
    /// Materialises a built query into an automatically named temporary table with <paramref name="options"/>
    /// and returns the generated name.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="options">The statement options; must not be <see langword="null"/>.</param>
    /// <returns>The generated temporary-table name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTable<TResult>(this QueryCommand<TResult> query, CreateTableAsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteNamed(ContextOf(query), query, temporary: true, options);
    }

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

    /// <summary>
    /// Asynchronously materialises a builder's query into an automatically named temporary table and
    /// returns the generated name, to be read back with <c>From(name)</c> on the same context.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated temporary-table name.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement.</exception>
    public static Task<string> ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteNamedAsync(builder.DataProvider, builder.ToCommand(), temporary: true, options: null, cancellationToken);
    }

    /// <summary>
    /// Asynchronously materialises a builder's query into an automatically named temporary table with
    /// <paramref name="options"/> and returns the generated name.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="options">The statement options; must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated temporary-table name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task<string> ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, CreateTableAsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteNamedAsync(builder.DataProvider, builder.ToCommand(), temporary: true, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously materialises a built query into an automatically named temporary table and returns
    /// the generated name, to be read back with <c>From(name)</c> on the same context.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated temporary-table name.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement.</exception>
    public static Task<string> ToTempTableAsync<TResult>(this QueryCommand<TResult> query, CancellationToken cancellationToken = default)
        => ExecuteNamedAsync(ContextOf(query), query, temporary: true, options: null, cancellationToken);

    /// <summary>
    /// Asynchronously materialises a built query into an automatically named temporary table with
    /// <paramref name="options"/> and returns the generated name.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="options">The statement options; must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated temporary-table name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task<string> ToTempTableAsync<TResult>(this QueryCommand<TResult> query, CreateTableAsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteNamedAsync(ContextOf(query), query, temporary: true, options, cancellationToken);
    }

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

    // The auto-named form: the caller does not choose a name, but must keep the generated one to read the
    // session-scoped table back with From(name) on the same context.
    private static string ExecuteNamed<TResult>(IDataContext context, QueryCommand<TResult> source, bool temporary, CreateTableAsOptions? options)
    {
        var name = NextTempTableName();
        Execute(context, source, name, temporary, options);
        return name;
    }

    private static async Task<string> ExecuteNamedAsync<TResult>(IDataContext context, QueryCommand<TResult> source, bool temporary, CreateTableAsOptions? options, CancellationToken cancellationToken)
    {
        var name = NextTempTableName();
        await ExecuteAsync(context, source, name, temporary, options, cancellationToken).ConfigureAwait(false);
        return name;
    }

    private static string NextTempTableName()
        => "__nextorm_temp_" + Guid.NewGuid().ToString("N")[..8];

    private static IDataContext ContextOf<TResult>(QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.DataContext
            ?? throw new NotSupportedException("The query is not bound to a data context; build it from a context with From<T>().");
    }

    private static IDataContext ContextOf(QueryCommand query)
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
