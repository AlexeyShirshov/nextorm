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
    public static void ToTempTable<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: true, options);
    }

    /// <summary>Materialises a builder's query into the temporary table <paramref name="name"/>, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTempTable<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: true, CreateTableOptionsBuilder.Build(configure));
    }

    /// <summary>Materialises a built query into the temporary table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTempTable<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null)
        => Execute(ContextOf(query), query, name, temporary: true, options);

    /// <summary>Materialises a built query into the temporary table <paramref name="name"/>, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTempTable<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => Execute(ContextOf(query), query, name, temporary: true, CreateTableOptionsBuilder.Build(configure));

    /// <summary>Asynchronously materialises a builder's query into a temporary table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: true, options, cancellationToken);
    }

    /// <summary>Asynchronously materialises a builder's query into the temporary table, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: true, CreateTableOptionsBuilder.Build(configure), cancellationToken);
    }

    /// <summary>Asynchronously materialises a built query into a temporary table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: true, options, cancellationToken);

    /// <summary>Asynchronously materialises a built query into the temporary table, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTempTableAsync<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: true, CreateTableOptionsBuilder.Build(configure), cancellationToken);

    /// <summary>Renders the temporary-table statement a builder's query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: true, options);
    }

    /// <summary>Renders the temporary-table statement a builder's query would execute, configuring the options fluently and without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: true, CreateTableOptionsBuilder.Build(configure));
    }

    /// <summary>Renders the temporary-table statement a built query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null)
        => Render(ContextOf(query), query, name, temporary: true, options);

    /// <summary>Renders the temporary-table statement a built query would execute, configuring the options fluently and without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTempTableSql<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => Render(ContextOf(query), query, name, temporary: true, CreateTableOptionsBuilder.Build(configure));

    /// <summary>Materialises a builder's query into the persistent table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: false, options);
    }

    /// <summary>Materialises a builder's query into the persistent table <paramref name="name"/>, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored. <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Execute(builder.DataProvider, builder.ToCommand(), name, temporary: false, CreateTableOptionsBuilder.Build(configure));
    }

    /// <summary>Materialises a built query into the persistent table <paramref name="name"/>.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null)
        => Execute(ContextOf(query), query, name, temporary: false, options);

    /// <summary>Materialises a built query into the persistent table <paramref name="name"/>, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored. <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static void ToTable<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => Execute(ContextOf(query), query, name, temporary: false, CreateTableOptionsBuilder.Build(configure));

    /// <summary>Asynchronously materialises a builder's query into a persistent table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: false, options, cancellationToken);
    }

    /// <summary>Asynchronously materialises a builder's query into the persistent table, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored. <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return ExecuteAsync(builder.DataProvider, builder.ToCommand(), name, temporary: false, CreateTableOptionsBuilder.Build(configure), cancellationToken);
    }

    /// <summary>Asynchronously materialises a built query into a persistent table.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options; <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: false, options, cancellationToken);

    /// <summary>Asynchronously materialises a built query into the persistent table, configuring the options fluently.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored. <see cref="CreateTableOptions.OnCommit"/> is invalid here.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task that completes when the table has been created.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static Task ToTableAsync<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure, CancellationToken cancellationToken = default)
        => ExecuteAsync(ContextOf(query), query, name, temporary: false, CreateTableOptionsBuilder.Build(configure), cancellationToken);

    /// <summary>Renders the persistent-table statement a builder's query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this EntityBuilder<TResult> builder, string name, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: false, options);
    }

    /// <summary>Renders the persistent-table statement a builder's query would execute, configuring the options fluently and without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this EntityBuilder<TResult> builder, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Render(builder.DataProvider, builder.ToCommand(), name, temporary: false, CreateTableOptionsBuilder.Build(configure));
    }

    /// <summary>Renders the persistent-table statement a built query would execute, without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this QueryCommand<TResult> query, string name, CreateTableOptions? options = null)
        => Render(ContextOf(query), query, name, temporary: false, options);

    /// <summary>Renders the persistent-table statement a built query would execute, configuring the options fluently and without executing it.</summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="name">The raw (unquoted) target table name.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot express the statement or one of the requested options.</exception>
    public static string ToTableSql<TResult>(this QueryCommand<TResult> query, string name, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => Render(ContextOf(query), query, name, temporary: false, CreateTableOptionsBuilder.Build(configure));

    /// <summary>
    /// Wraps a built query in a lazy temporary-table source. Nothing is executed: the table is
    /// materialised (and read) only when a query reads the source through
    /// <see cref="DataContextExtensions.From{TResult}(IDataContext, TempTableSource{TResult})"/>.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="options">Optional materialisation options.</param>
    /// <returns>A source to read back with <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public static TempTableSource<TResult> AsTempTable<TResult>(this QueryCommand<TResult> query, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new TempTableSource<TResult>(query, NextTempTableName(), options ?? new CreateTableOptions());
    }

    /// <summary>
    /// Wraps a built query in a lazy temporary-table source, configuring the materialisation options
    /// fluently. Nothing is executed until the source is read.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query whose rows fill the table.</param>
    /// <param name="configure">Configures the materialisation options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>A source to read back with <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static TempTableSource<TResult> AsTempTable<TResult>(this QueryCommand<TResult> query, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new TempTableSource<TResult>(query, NextTempTableName(), CreateTableOptionsBuilder.Build(configure));
    }

    /// <summary>
    /// Wraps a builder's query (every column of the source) in a lazy temporary-table source. Nothing
    /// is executed until the source is read. See
    /// <see cref="AsTempTable{TResult}(QueryCommand{TResult}, CreateTableOptions?)"/> for the contract.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="options">Optional materialisation options.</param>
    /// <returns>A source to read back with <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TempTableSource<TResult> AsTempTable<TResult>(this EntityBuilder<TResult> builder, CreateTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ToCommand().AsTempTable(options);
    }

    /// <summary>
    /// Wraps a builder's query (every column of the source) in a lazy temporary-table source, configuring
    /// the materialisation options fluently. Nothing is executed until the source is read.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="builder">The query whose rows fill the table.</param>
    /// <param name="configure">Configures the materialisation options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>A source to read back with <c>From</c> on the same context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static TempTableSource<TResult> AsTempTable<TResult>(this EntityBuilder<TResult> builder, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ToCommand().AsTempTable(configure);
    }

    /// <summary>
    /// Renders the batch a query reading a lazy temporary table would execute — the table's
    /// <c>DROP TABLE IF EXISTS</c> and <c>CREATE TEMPORARY TABLE ... AS SELECT</c> followed by the read
    /// query — without executing it.
    /// </summary>
    /// <typeparam name="TResult">The query's projected row type.</typeparam>
    /// <param name="query">The query reading a source created with <see cref="AsTempTable{TResult}(QueryCommand{TResult}, CreateTableOptions?)"/>.</param>
    /// <returns>The rendered batch SQL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The query does not read a temporary table.</exception>
    /// <exception cref="NotSupportedException">The context or provider cannot execute a batch or express a temporary materialisation.</exception>
    public static string ToBatchSql<TResult>(this QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var context = ContextOf(query);
        if (context is not IBatchExecutor executor)
            throw new NotSupportedException(
                $"{context.GetType().Name} cannot materialise a temporary table; use a database-backed context (PostgreSQL, SQLite, MySQL or MariaDB).");

        return executor.RenderTemporaryTableBatch(query).ToSql();
    }

    private static void Execute<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableOptions? options)
    {
        var command = BuildCommand(source, name, temporary, options);
        var executor = RequireExecutor(context);

        if (command.Options.DropExisting)
        {
            // Validate the create against the provider before dropping, so an unsupported option cannot
            // leave the table dropped without a replacement.
            _ = executor.Render(command);
            executor.Execute(BuildDropCommand(source, name));
        }

        executor.Execute(command);
    }

    private static async Task ExecuteAsync<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableOptions? options, CancellationToken cancellationToken)
    {
        var command = BuildCommand(source, name, temporary, options);
        var executor = RequireExecutor(context);

        if (command.Options.DropExisting)
        {
            _ = executor.Render(command);
            await executor.Execute(BuildDropCommand(source, name), cancellationToken).ConfigureAwait(false);
        }

        await executor.Execute(command, cancellationToken).ConfigureAwait(false);
    }

    private static string Render<TResult>(IDataContext context, QueryCommand<TResult> source, string name, bool temporary, CreateTableOptions? options)
    {
        var command = BuildCommand(source, name, temporary, options);
        var executor = RequireExecutor(context);
        return command.Options.DropExisting
            ? RenderWithDrop(executor, source, name, command)
            : executor.Render(command);
    }

    private static CreateTableAsCommand BuildCommand<TResult>(QueryCommand<TResult> source, string name, bool temporary, CreateTableOptions? options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        options ??= new CreateTableOptions();
        // Validate before any statement runs, so a contradictory/unsupported option never leaves the
        // target dropped without its replacement.
        options.Validate(temporary);

        return new CreateTableAsCommand(typeof(TResult), name, temporary, source, options);
    }

    // Drops the target before the materialisation when DropExisting is set. The name was already
    // validated and the options checked by BuildCommand, and the create is rendered (validated) before
    // this runs, so the drop is only issued once the create is known to be executable.
    private static DropTableCommand BuildDropCommand<TResult>(QueryCommand<TResult> source, string name)
        => new(name, source.QuoteIdentifiers, source.KeywordCase);

    // Renders the drop + create pair a DropExisting materialisation executes, for the diagnostic
    // ToTableSql/ToTempTableSql form.
    private static string RenderWithDrop<TResult>(IMutationExecutor executor, QueryCommand<TResult> source, string name, CreateTableAsCommand command)
        => executor.Render(BuildDropCommand(source, name)) + "; " + executor.Render(command);

    internal static string NextTempTableName()
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
