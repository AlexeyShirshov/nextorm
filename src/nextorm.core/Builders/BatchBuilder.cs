using System.Data;

namespace NextORM.Core;

/// <summary>
/// Entry point for executing several statements as one batch (one round trip, one server session).
/// A batch is useful when a statement depends on a session-scoped side effect of an earlier one — a
/// materialisation (<c>CREATE TEMPORARY TABLE ... AS SELECT</c>) or a DML statement (<c>INSERT</c>,
/// <c>UPDATE</c>, <c>DELETE</c>) followed by a query reading the result: under a connection-level pooler
/// that reassigns the backend per transaction (PgBouncer in <c>transaction</c> mode) two separate
/// commands can be routed to different backends, so the read fails with <c>relation does not exist</c>.
/// One batch keeps them on the same backend.
/// <para>
/// A provider capable of a single-round-trip batch opts in through
/// <see cref="ISqlDialect.SupportsBatch"/> (PostgreSQL, SQL Server, MySQL, MariaDB and SQLite). The
/// in-memory context has no batch executor, so <see cref="Batch(IDataContext)"/> rejects it immediately;
/// a database-backed provider without the capability throws <see cref="NotSupportedException"/> when the
/// batch renders instead of silently degrading to separate statements.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var rows = ctx.Batch()
///     .Update(ctx.Update&lt;Order&gt;().Set(o =&gt; o.Status, "shipped").Where(o =&gt; o.Id == id))
///     .Query(ctx.From&lt;Order&gt;().Where(o =&gt; o.Id == id).Select(o =&gt; new { o.Id, o.Status }))
///     .ToList();
/// </code>
/// </example>
public static class BatchExtensions
{
    /// <summary>Starts a batch on a database-backed context.</summary>
    /// <param name="context">The context that executes the batch.</param>
    /// <returns>A builder for the batch's statements.</returns>
    /// <exception cref="NotSupportedException">The context cannot execute batches (the in-memory context).</exception>
    public static BatchBuilder Batch(this IDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is not IBatchExecutor executor)
            throw new NotSupportedException(
                $"{context.GetType().Name} does not support batches. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");

        return new BatchBuilder(context, executor);
    }
}

/// <summary>
/// Collects the statements of a batch: side-effecting statements (materialisations and DML) followed by
/// exactly one result-bearing query, added last with <see cref="Query{TResult}"/>. Every statement is
/// rendered with one shared parameter sequence (captured variables are prefixed per statement), so
/// placeholder names never collide.
/// </summary>
public sealed class BatchBuilder
{
    private readonly IDataContext _context;
    private readonly IBatchExecutor _executor;
    private readonly List<BatchStepSpec> _steps = new();
    private bool _hasResult;

    internal BatchBuilder(IDataContext context, IBatchExecutor executor)
    {
        _context = context;
        _executor = executor;
    }

    /// <summary>
    /// Adds a persistent <c>CREATE TABLE ... AS SELECT</c> materialisation to the batch.
    /// </summary>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="source">The query whose rows fill the table.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express the materialisation or one of the requested options.</exception>
    public BatchBuilder CreateTable(string name, QueryCommand source, CreateTableOptions? options = null)
        => AddCreateTable(name, source, temporary: false, options);

    /// <summary>
    /// Adds a persistent <c>CREATE TABLE ... AS SELECT</c> materialisation to the batch, configuring the
    /// options fluently.
    /// </summary>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="source">The query whose rows fill the table.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express the materialisation or one of the requested options.</exception>
    public BatchBuilder CreateTable(string name, QueryCommand source, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => AddCreateTable(name, source, temporary: false, CreateTableOptionsBuilder.Build(configure));

    /// <summary>
    /// Adds a temporary <c>CREATE TEMPORARY TABLE ... AS SELECT</c> materialisation to the batch. The
    /// table is session-scoped: reading it in the same batch is exactly what the batch guarantees.
    /// </summary>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="source">The query whose rows fill the table.</param>
    /// <param name="options">Optional statement options.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express a temporary materialisation or one of the requested options.</exception>
    public BatchBuilder CreateTempTable(string name, QueryCommand source, CreateTableOptions? options = null)
        => AddCreateTable(name, source, temporary: true, options);

    /// <summary>
    /// Adds a temporary <c>CREATE TEMPORARY TABLE ... AS SELECT</c> materialisation to the batch,
    /// configuring the options fluently.
    /// </summary>
    /// <param name="name">The raw (unquoted) target table name; the naming convention is not applied.</param>
    /// <param name="source">The query whose rows fill the table.</param>
    /// <param name="configure">Configures the statement options through <see cref="CreateTableOptionsBuilder"/>; its return value is ignored.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express a temporary materialisation or one of the requested options.</exception>
    public BatchBuilder CreateTempTable(string name, QueryCommand source, Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
        => AddCreateTable(name, source, temporary: true, CreateTableOptionsBuilder.Build(configure));

    /// <summary>
    /// Adds a side-effecting <c>INSERT</c> to the batch. It runs before the result-bearing query, on the
    /// same session, so the query can observe the inserted rows.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type written by the statement.</typeparam>
    /// <param name="insert">The insert builder whose values define the statement.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="insert"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="ArgumentException">The statement is bound to a different data context.</exception>
    public BatchBuilder Insert<TEntity>(InsertBuilder<TEntity> insert)
    {
        ArgumentNullException.ThrowIfNull(insert);
        return AddMutation(insert.DataContext, insert.BuildBatchCommand(), nameof(insert));
    }

    /// <summary>
    /// Adds a side-effecting <c>UPDATE</c> to the batch. It runs before the result-bearing query, on the
    /// same session, so the query can observe the updated rows.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose rows are updated.</typeparam>
    /// <param name="update">The update builder whose assignments and predicate define the statement.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="update"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="ArgumentException">The statement is bound to a different data context.</exception>
    public BatchBuilder Update<TEntity>(UpdateBuilder<TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return AddMutation(update.DataContext, update.BuildBatchCommand(), nameof(update));
    }

    /// <summary>
    /// Adds a side-effecting <c>DELETE</c> to the batch. It runs before the result-bearing query, on the
    /// same session, so the query observes the deletion.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose rows are removed.</typeparam>
    /// <param name="delete">The delete builder whose predicate defines the statement.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delete"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="ArgumentException">The statement is bound to a different data context.</exception>
    public BatchBuilder Delete<TEntity>(DeleteBuilder<TEntity> delete)
    {
        ArgumentNullException.ThrowIfNull(delete);
        return AddMutation(delete.DataContext, delete.BuildBatchCommand(), nameof(delete));
    }

    /// <summary>
    /// Adds a side-effecting <c>TRUNCATE</c> to the batch. It runs before the result-bearing query.
    /// Providers without <c>TRUNCATE</c> (SQLite) reject it when the batch renders.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose table is truncated.</typeparam>
    /// <param name="truncate">The truncate builder for the target table.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="truncate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result-bearing query has already been added.</exception>
    /// <exception cref="ArgumentException">The statement is bound to a different data context.</exception>
    public BatchBuilder Truncate<TEntity>(TruncateBuilder<TEntity> truncate)
    {
        ArgumentNullException.ThrowIfNull(truncate);
        return AddMutation(truncate.DataContext, truncate.BuildBatchCommand(), nameof(truncate));
    }

    /// <summary>
    /// Adds the result-bearing query and returns the batch terminal. It must be the last statement: any
    /// further statement would throw.
    /// </summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="query">The query whose rows are materialised.</param>
    /// <returns>The batch terminal.</returns>
    /// <exception cref="InvalidOperationException">A result-bearing query has already been added.</exception>
    public BatchQuery<TResult> Query<TResult>(QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_hasResult)
            throw new InvalidOperationException("A batch has exactly one result-bearing query, and it must be the last statement.");

        RequireSameContext(query);
        _steps.Add(BatchStepSpec.ForResult(query));
        _hasResult = true;
        return new BatchQuery<TResult>(_executor, _steps.ToArray());
    }

    private BatchBuilder AddCreateTable(string name, QueryCommand source, bool temporary, CreateTableOptions? options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(source);
        if (_hasResult)
            throw new InvalidOperationException("A batch has exactly one result-bearing query, and it must be the last statement; add materialisations before Query().");

        RequireSameContext(source);
        var command = new CreateTableAsCommand(source.ResultType ?? typeof(object), name, temporary, source, options ?? new CreateTableOptions());
        _steps.Add(BatchStepSpec.ForCreateTableAs(command));
        return this;
    }

    private BatchBuilder AddMutation(IDataContext context, MutationCommand command, string parameterName)
    {
        if (_hasResult)
            throw new InvalidOperationException("A batch has exactly one result-bearing query, and it must be the last statement; add mutations before Query().");

        if (!ReferenceEquals(context, _context))
            throw new ArgumentException("A batch statement is bound to a different data context; build every statement from the batch's context.", parameterName);

        _steps.Add(BatchStepSpec.ForMutation(command));
        return this;
    }

    private void RequireSameContext(QueryCommand command)
    {
        if (command.DataContext is { } commandContext && !ReferenceEquals(commandContext, _context))
            throw new ArgumentException("A batch statement is bound to a different data context; build every statement from the batch's context.", nameof(command));
    }
}

/// <summary>
/// Terminal of a batch: materialises the rows of the batch's result-bearing query. The whole batch —
/// the preceding statements and this query — executes as one round trip.
/// </summary>
/// <typeparam name="TResult">The projected row type.</typeparam>
public sealed class BatchQuery<TResult>
{
    private readonly IBatchExecutor _executor;
    private readonly IReadOnlyList<BatchStepSpec> _steps;

    internal BatchQuery(IBatchExecutor executor, IReadOnlyList<BatchStepSpec> steps)
    {
        _executor = executor;
        _steps = steps;
    }

    /// <summary>Renders the whole batch (statements joined with <c>;</c>) without executing it.</summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public string ToSql() => Render().ToSql();

    /// <summary>Executes the batch and materialises the result rows.</summary>
    /// <returns>The result rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public List<TResult> ToList()
    {
        var plan = Render();
        return _executor.ExecuteBatch(plan, _executor.BuildBatchMapper<TResult>(plan));
    }

    /// <summary>Asynchronously executes the batch and materialises the result rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the result rows, in result-set order.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var plan = Render();
        return _executor.ExecuteBatchAsync(plan, _executor.BuildBatchMapper<TResult>(plan), cancellationToken);
    }

    /// <summary>
    /// Streams the batch's result rows. The underlying reader stays open for the whole batch, so the
    /// batch executes when enumeration starts and its connection is held until enumeration completes.
    /// </summary>
    /// <param name="cancellationToken">Cancels enumeration.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(CancellationToken cancellationToken = default)
    {
        var plan = Render();
        return _executor.StreamBatch(plan, _executor.BuildBatchMapper<TResult>(plan), cancellationToken);
    }

    private BatchPlan Render() => _executor.RenderBatch(_steps);
}
