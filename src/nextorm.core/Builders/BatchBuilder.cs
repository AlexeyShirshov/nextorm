using System.Collections;
using System.Data;
using System.Data.Common;

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
/// one or more result-bearing queries. A single result can be added with the terminal
/// <see cref="Query{TResult}"/>; several results can be added with <see cref="AddQuery{TResult}"/> and
/// then materialised together with <see cref="Execute"/> (one network round trip, every result set
/// read eagerly). Every statement is rendered with one shared parameter sequence (captured variables are
/// prefixed per statement), so placeholder names never collide.
/// </summary>
public sealed class BatchBuilder
{
    private readonly IDataContext _context;
    private readonly IBatchExecutor _executor;
    private readonly List<BatchStepSpec> _steps = new();
    private readonly List<Func<BatchPlan, IBatchResultMaterializer>> _resultFactories = new();
    private bool _hasResult;
    private bool _terminalQueryUsed;

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
    /// Adds a verbatim, side-effecting raw SQL statement to the batch. The text is rendered exactly as
    /// given — placeholders are not rewritten — and carries no parameters, so a value that must vary
    /// cannot be captured from an expression tree. Useful for statements the command model does not
    /// cover, such as SQL Server <c>INSERT ... EXEC</c> or scripts touching a session-scoped
    /// <c>#temp</c> table. It must be added before the result-bearing queries. The text is executed
    /// verbatim and binds no parameters, so pass only trusted SQL — never concatenate untrusted user
    /// input into a raw statement.
    /// </summary>
    /// <param name="sql">The statement text; it runs before the result-bearing queries.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="sql"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A result-bearing query has already been added; add raw statements before <see cref="Query{TResult}"/>/<see cref="AddQuery{TResult}"/>.</exception>
    /// <remarks>The batch must still end with at least one result-bearing query.</remarks>
    public BatchBuilder Raw(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        if (_hasResult)
            throw new InvalidOperationException("A result-bearing query has already been added; add raw statements before Query()/AddQuery().");

        _steps.Add(BatchStepSpec.ForRaw(sql));
        return this;
    }

    /// <summary>
    /// Adds the sole result-bearing query and returns the batch terminal. It must be the last statement:
    /// any further statement would throw. To carry more than one result use
    /// <see cref="AddQuery{TResult}"/> and <see cref="Execute"/> instead.
    /// </summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="query">The query whose rows are materialised.</param>
    /// <returns>The batch terminal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A result-bearing query has already been added.</exception>
    /// <exception cref="ArgumentException">The query is bound to a different data context.</exception>
    public BatchQuery<TResult> Query<TResult>(QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_hasResult)
            throw new InvalidOperationException("A batch already has a result-bearing query; Query() is the single-result terminal and cannot be combined with AddQuery(). Use AddQuery() for every result and Execute() to materialise them.");

        RequireSameContext(query);
        _steps.Add(BatchStepSpec.ForResult(query));
        _hasResult = true;
        _terminalQueryUsed = true;
        return new BatchQuery<TResult>(_executor, _steps.ToArray());
    }

    /// <summary>
    /// Adds a result-bearing query without ending the batch, so several result sets can be carried by one
    /// batch and materialised together by <see cref="Execute"/>. May be called repeatedly; every result
    /// query must be added after all side-effecting statements.
    /// </summary>
    /// <typeparam name="TResult">The projected row type of the added query.</typeparam>
    /// <param name="query">The query whose rows form the next result set.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The batch was ended with <see cref="Query{TResult}"/>; a single-result terminal cannot be combined with <see cref="AddQuery{TResult}"/>.</exception>
    /// <exception cref="ArgumentException">The query is bound to a different data context.</exception>
    public BatchBuilder AddQuery<TResult>(QueryCommand<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_terminalQueryUsed)
            throw new InvalidOperationException("A batch ended with Query() cannot accept further queries; use AddQuery() for every result set and Execute() to materialise them.");

        RequireSameContext(query);
        _steps.Add(BatchStepSpec.ForResult(query));
        _hasResult = true;

        var index = _resultFactories.Count;
        _resultFactories.Add(plan =>
            new BatchResultMaterializer<TResult>(_executor.BuildBatchMapper<TResult>(plan, index)));

        return this;
    }

    /// <summary>
    /// Renders the whole batch (every added statement joined with <c>;</c>) without executing it. The
    /// rendered text is exactly what <see cref="Execute"/> runs; this is the inspection counterpart of
    /// execution for a batch built with <see cref="AddQuery{TResult}"/>, mirroring
    /// <see cref="BatchQuery{TResult}.ToSql"/> for the single-result terminal.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No result-bearing query was added, or the batch was ended with <see cref="Query{TResult}"/>.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public string ToSql()
    {
        RequireExecutable();
        return _executor.RenderBatch(_steps).ToSql();
    }

    /// <summary>
    /// Executes the batch and eagerly materialises every result set added with <see cref="AddQuery{TResult}"/>.
    /// The whole batch — all side-effecting statements and all result queries — runs as one network round
    /// trip; the result sets are fully buffered before this method returns.
    /// </summary>
    /// <returns>The eagerly materialised result sets, readable in the order the queries were added.</returns>
    /// <exception cref="InvalidOperationException">No result-bearing query was added, or the batch was ended with <see cref="Query{TResult}"/>.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public BatchResult Execute()
    {
        RequireExecutable();
        var plan = _executor.RenderBatch(_steps);
        return _executor.ExecuteBatchResults(plan, BuildMaterializers(plan));
    }

    /// <summary>
    /// Asynchronously executes the batch and eagerly materialises every result set added with
    /// <see cref="AddQuery{TResult}"/>. The whole batch runs as one network round trip and the result sets
    /// are fully buffered before the returned task completes.
    /// </summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the eagerly materialised result sets.</returns>
    /// <exception cref="InvalidOperationException">No result-bearing query was added, or the batch was ended with <see cref="Query{TResult}"/>.</exception>
    /// <exception cref="NotSupportedException">The dialect cannot express the batch or one of its statements.</exception>
    public async Task<BatchResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        RequireExecutable();
        var plan = _executor.RenderBatch(_steps);
        return await _executor.ExecuteBatchResultsAsync(plan, BuildMaterializers(plan), cancellationToken).ConfigureAwait(false);
    }

    private void RequireExecutable()
    {
        if (_terminalQueryUsed)
            throw new InvalidOperationException("Query() returns a terminal; use its ToList/ToListAsync, or build the batch with AddQuery() and call Execute().");

        if (!_hasResult)
            throw new InvalidOperationException("A batch needs at least one result-bearing query before it can be executed; add one with AddQuery() or Query().");
    }

    private IBatchResultMaterializer[] BuildMaterializers(BatchPlan plan)
    {
        var materializers = new IBatchResultMaterializer[_resultFactories.Count];
        for (var i = 0; i < _resultFactories.Count; i++)
            materializers[i] = _resultFactories[i](plan);

        return materializers;
    }

    private BatchBuilder AddCreateTable(string name, QueryCommand source, bool temporary, CreateTableOptions? options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(source);
        if (_hasResult)
            throw new InvalidOperationException("A result-bearing query has already been added; add materialisations before Query()/AddQuery().");

        RequireSameContext(source);
        var command = new CreateTableAsCommand(source.ResultType ?? typeof(object), name, temporary, source, options ?? new CreateTableOptions());
        _steps.Add(BatchStepSpec.ForCreateTableAs(command));
        return this;
    }

    private BatchBuilder AddMutation(IDataContext context, MutationCommand command, string parameterName)
    {
        if (_hasResult)
            throw new InvalidOperationException("A result-bearing query has already been added; add mutations before Query()/AddQuery().");

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
/// The eagerly materialised result sets of a batch built with <see cref="BatchBuilder.AddQuery{TResult}"/>
/// and executed with <see cref="BatchBuilder.Execute"/> or <see cref="BatchBuilder.ExecuteAsync"/>. Every
/// set is fully buffered in memory, so the instance owns no reader or connection and is not disposable.
/// </summary>
public sealed class BatchResult
{
    private readonly IReadOnlyList<IList> _sets;
    private readonly IReadOnlyList<Type> _resultTypes;
    private int _index;

    internal BatchResult(IReadOnlyList<IList> sets, IReadOnlyList<Type> resultTypes)
    {
        _sets = sets;
        _resultTypes = resultTypes;
    }

    /// <summary>The number of result sets the batch produced, in the order the queries were added.</summary>
    public int ResultSetCount => _sets.Count;

    /// <summary>
    /// Returns the next result set, in the order the result queries were added. Each set may be read once;
    /// the set's projected row type must match <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The projected row type of the next result set.</typeparam>
    /// <returns>The rows of the next result set.</returns>
    /// <remarks>
    /// The result sets are forward-only: every call returns the next set in order and there is exactly
    /// one call per set (the first call returns the first set, the second call the second set, and so on).
    /// A call with the wrong <typeparamref name="TResult"/> throws without consuming the set, so it can be
    /// retried with the matching type; reading past the last set throws. Use
    /// <see cref="ResultSetCount"/> to know how many sets exist.
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no further result set, or the next set's projected type is not <typeparamref name="TResult"/>.</exception>
    public IReadOnlyList<TResult> Read<TResult>()
    {
        if (_index >= _sets.Count)
            throw new InvalidOperationException($"The batch has {_sets.Count} result set(s); every result set has already been read.");

        var expected = _resultTypes[_index];
        if (expected != typeof(TResult))
            throw new InvalidOperationException($"Result set {_index} projects '{expected.Name}', not '{typeof(TResult).Name}'; read it with the matching type.");

        var set = (IReadOnlyList<TResult>)_sets[_index];
        _index++;
        return set;
    }
}

/// <summary>
/// Materialises one result set of a batch as a non-generic <see cref="IList"/>. Offers both a
/// synchronous and an asynchronous row loop so the async execution path never falls back to
/// <see cref="DbDataReader.Read"/> (sync-over-async) and honours the cancellation token. The compiled
/// mapper of the result set is captured by the generic implementation, so no reflection is involved.
/// </summary>
internal interface IBatchResultMaterializer
{
    /// <summary>The projected row type of the result set this materialiser reads.</summary>
    Type ResultType { get; }

    /// <summary>Reads the current result set to its end using the synchronous row loop.</summary>
    /// <param name="reader">The reader positioned on the result set.</param>
    /// <returns>The materialised rows.</returns>
    IList Read(DbDataReader reader);

    /// <summary>Reads the current result set to its end using the asynchronous row loop.</summary>
    /// <param name="reader">The reader positioned on the result set.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A value task producing the materialised rows.</returns>
    ValueTask<IList> ReadAsync(DbDataReader reader, CancellationToken cancellationToken);
}

/// <summary>
/// The typed <see cref="IBatchResultMaterializer"/>: holds the compiled row mapper of one result set
/// and buffers its rows into a <see cref="List{T}"/>.
/// </summary>
/// <typeparam name="TResult">The projected row type of the result set.</typeparam>
internal sealed class BatchResultMaterializer<TResult> : IBatchResultMaterializer
{
    private readonly Func<IDataRecord, TResult> _mapper;

    /// <inheritdoc/>
    public Type ResultType => typeof(TResult);

    /// <summary>Creates a materialiser over a compiled row mapper.</summary>
    /// <param name="mapper">The compiled mapper of the result set.</param>
    internal BatchResultMaterializer(Func<IDataRecord, TResult> mapper) => _mapper = mapper;

    /// <inheritdoc/>
    public IList Read(DbDataReader reader)
    {
        var list = new List<TResult>();
        while (reader.Read())
            list.Add(_mapper(reader));

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<IList> ReadAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var list = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(_mapper(reader));

        return list;
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
