using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

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
/// in-memory context has no batch executor, so <see cref="CreateBatchBuilder(IDataContext)"/> rejects it immediately;
/// a database-backed provider without the capability throws <see cref="NotSupportedException"/> when the
/// batch renders instead of silently degrading to separate statements.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var rows = ctx.CreateBatchBuilder()
///     .Update(ctx.CreateUpdateBuilder&lt;Order&gt;().Set(o =&gt; o.Status, "shipped").Where(o =&gt; o.Id == id))
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
    public static BatchBuilder CreateBatchBuilder(this IDataContext context)
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
    private readonly Dictionary<string, CaptureRegistration> _captures = new(StringComparer.Ordinal);
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
    /// Adds a side-effecting <c>INSERT</c> whose returned rows are captured into a same-batch table
    /// variable (SQL Server <c>OUTPUT ... INTO @t</c>) instead of being returned to the client. The batch
    /// renders <c>DECLARE @t TABLE (...); INSERT ... OUTPUT ... INTO @t (...)</c> as one statement; a later
    /// <see cref="FromOutput{TResult}(string)"/> or <see cref="FromOutputTable(string)"/> step of the same
    /// batch reads the captured rows, so the whole operation stays one round trip.
    /// <para>
    /// The table-variable name is one identifier with an optional leading <c>@</c>, normalised to
    /// <c>@name</c>; it is never quoted. <paramref name="columnDefinitions"/> is trusted caller SQL that
    /// is embedded verbatim in the <c>DECLARE</c> text and never parameterised.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type inserted.</typeparam>
    /// <typeparam name="TResult">The row shape the returning builder projects.</typeparam>
    /// <param name="mutation">The insert terminal whose returned columns are captured.</param>
    /// <param name="variableName">The table-variable name, with or without the leading <c>@</c>.</param>
    /// <param name="columnDefinitions">The trusted column-definition text of the <c>DECLARE @t TABLE (...)</c> declaration.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mutation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a bare table-variable identifier, or <paramref name="columnDefinitions"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The capture is duplicated on this batch, the projection has no output columns, or a result-bearing query was already added.</exception>
    /// <exception cref="NotSupportedException">The provider has no <c>OUTPUT INTO</c> form (only SQL Server supports the capture).</exception>
    public BatchBuilder OutputIntoTableVariable<TEntity, TResult>(InsertReturningBuilder<TEntity, TResult> mutation, string variableName, string columnDefinitions)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var name = RequireCaptureSlot(variableName, columnDefinitions, mutation.ReturningColumns, ((IOutputIntoMutation)mutation).DataContext);
        var shape = MutationReadShape.BuildShape<TEntity, TResult>(_context, mutation);
        return RegisterCapture(name, typeof(TResult), shape, mutation.BuildMutationCommand(), columnDefinitions);
    }

    /// <summary>
    /// Adds a side-effecting <c>UPDATE</c> whose returned rows are captured into a same-batch table
    /// variable, mirroring <see cref="OutputIntoTableVariable{TEntity, TResult}(InsertReturningBuilder{TEntity, TResult}, string, string)"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type updated.</typeparam>
    /// <typeparam name="TResult">The row shape the returning builder projects.</typeparam>
    /// <param name="mutation">The update terminal whose returned columns are captured.</param>
    /// <param name="variableName">The table-variable name, with or without the leading <c>@</c>.</param>
    /// <param name="columnDefinitions">The trusted column-definition text of the <c>DECLARE @t TABLE (...)</c> declaration.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mutation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a bare table-variable identifier, or <paramref name="columnDefinitions"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The capture is duplicated on this batch, the projection has no output columns, or a result-bearing query was already added.</exception>
    /// <exception cref="NotSupportedException">The provider has no <c>OUTPUT INTO</c> form (only SQL Server supports the capture).</exception>
    public BatchBuilder OutputIntoTableVariable<TEntity, TResult>(UpdateReturningBuilder<TEntity, TResult> mutation, string variableName, string columnDefinitions)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var name = RequireCaptureSlot(variableName, columnDefinitions, mutation.ReturningColumns, mutation.DataContext);
        var shape = BuildTypedCaptureShape<TResult>(mutation.Projection, typeof(TEntity), "update");
        return RegisterCapture(name, typeof(TResult), shape, mutation.BuildMutationCommand(), columnDefinitions);
    }

    /// <summary>
    /// Adds a side-effecting <c>DELETE</c> whose removed rows are captured into a same-batch table
    /// variable (read through the <c>deleted</c> alias), mirroring
    /// <see cref="OutputIntoTableVariable{TEntity, TResult}(InsertReturningBuilder{TEntity, TResult}, string, string)"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type deleted.</typeparam>
    /// <typeparam name="TResult">The row shape the returning builder projects.</typeparam>
    /// <param name="mutation">The delete terminal whose returned columns are captured.</param>
    /// <param name="variableName">The table-variable name, with or without the leading <c>@</c>.</param>
    /// <param name="columnDefinitions">The trusted column-definition text of the <c>DECLARE @t TABLE (...)</c> declaration.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mutation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a bare table-variable identifier, or <paramref name="columnDefinitions"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The capture is duplicated on this batch, the projection has no output columns, or a result-bearing query was already added.</exception>
    /// <exception cref="NotSupportedException">The provider has no <c>OUTPUT INTO</c> form (only SQL Server supports the capture).</exception>
    public BatchBuilder OutputIntoTableVariable<TEntity, TResult>(DeleteReturningBuilder<TEntity, TResult> mutation, string variableName, string columnDefinitions)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var name = RequireCaptureSlot(variableName, columnDefinitions, mutation.ReturningColumns, mutation.DataContext);
        var shape = BuildTypedCaptureShape<TResult>(mutation.Projection, typeof(TEntity), "delete");
        return RegisterCapture(name, typeof(TResult), shape, mutation.BuildMutationCommand(), columnDefinitions);
    }

    /// <summary>
    /// Adds a side-effecting <c>MERGE</c> whose output rows are captured into a same-batch table
    /// variable. The captured output always leads with <c>$action</c> (written into the first column
    /// declared by <paramref name="columnDefinitions"/>), followed by the returned columns written into
    /// the remaining declared columns; this is the batch-only MERGE OUTPUT-INTO form, not a new MERGE
    /// fluent terminal.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
    /// <typeparam name="TResult">The row shape the returning builder projects.</typeparam>
    /// <param name="mutation">The merge terminal whose returned columns are captured.</param>
    /// <param name="variableName">The table-variable name, with or without the leading <c>@</c>.</param>
    /// <param name="columnDefinitions">The trusted column-definition text of the <c>DECLARE @t TABLE (...)</c> declaration; its first column receives <c>$action</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mutation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a bare table-variable identifier, or <paramref name="columnDefinitions"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The capture is duplicated on this batch, the projection has no output columns, or a result-bearing query was already added.</exception>
    /// <exception cref="NotSupportedException">The provider has no <c>OUTPUT INTO</c> form (only SQL Server supports the capture).</exception>
    public BatchBuilder OutputIntoTableVariable<TEntity, TResult>(MergeReturningBuilder<TEntity, TResult> mutation, string variableName, string columnDefinitions)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var name = RequireCaptureSlot(variableName, columnDefinitions, mutation.ReturningColumns, mutation.DataContext);
        var shape = BuildTypedCaptureShape<TResult>(mutation.Projection, typeof(TEntity), "merge");
        return RegisterCapture(name, typeof(TResult), shape, mutation.BuildMutationCommand(), columnDefinitions);
    }

    /// <summary>
    /// Starts a typed query over the rows a capture step of this batch wrote into a table variable. The
    /// returned builder projects the captured row shape, so the rows can be filtered, ordered, joined,
    /// paged and aggregated with the ordinary operator set; the source renders as the bare
    /// table-variable identifier and is never quoted.
    /// </summary>
    /// <typeparam name="TResult">The captured row shape; it must match the capture's projection.</typeparam>
    /// <param name="variableName">The table-variable name of an earlier capture of this batch.</param>
    /// <returns>A typed builder over the captured rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a valid table-variable identifier.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="variableName"/> is not a capture registered earlier on this batch, or its projection is not <typeparamref name="TResult"/>.</exception>
    public EntityBuilder<TResult> FromOutput<TResult>(string variableName)
    {
        var (name, capture) = RequireCapture(variableName, nameof(variableName));
        if (capture.RowType != typeof(TResult))
            throw new InvalidOperationException(
                $"The batch output source '{name}' projects '{capture.RowType.Name}', not '{typeof(TResult).Name}'; read it with the matching type (batch output source).");

        return new EntityBuilder<TResult>(_context)
        {
            Logger = _context.CommandLogger,
            SourceFrom = new FromExpression(name, capture.Shape) { IsTableVariableSource = true },
        };
    }

    /// <summary>
    /// Starts a query over the rows a capture step of this batch wrote into a table variable, reading its
    /// columns through <see cref="TableAlias"/> accessors (for example <c>t["id"].AsInt</c>). Useful for
    /// joins and for aggregating over the captured rows.
    /// </summary>
    /// <param name="variableName">The table-variable name of an earlier capture of this batch.</param>
    /// <returns>A builder over the captured rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="variableName"/> is not a valid table-variable identifier.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="variableName"/> is not a capture registered earlier on this batch.</exception>
    public EntityBuilder<TableAlias> FromOutputTable(string variableName)
    {
        var (name, _) = RequireCapture(variableName, nameof(variableName));

        return new EntityBuilder<TableAlias>(_context)
        {
            Logger = _context.CommandLogger,
            SourceFrom = new FromExpression(name) { IsTableVariableSource = true },
        };
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
        RequireCapturedSources(query);
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
        RequireCapturedSources(query);
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

    // The capture API is batch-owned: a name may be registered at most once per BatchBuilder instance,
    // so another batch never sees this source and a duplicate is refused before any SQL is produced.
    private string RequireCaptureSlot(string variableName, string columnDefinitions, IReadOnlyList<IPropertyMetadata>? columns, IDataContext context)
    {
        var name = NormalizeCaptureVariableName(variableName, nameof(variableName));
        ArgumentException.ThrowIfNullOrWhiteSpace(columnDefinitions);

        if (_context is not DataContext { Dialect.SupportsOutputInto: true })
            throw new NotSupportedException(
                $"{_context.GetType().Name} cannot capture modified rows into a table variable: the provider has no OUTPUT INTO form (SQL Server only). Read the rows with the mutation's own returning terminal and query them separately instead.");

        if (columns is not { Count: > 0 })
            throw new InvalidOperationException(
                "A batch output source needs at least one output column; use Returning()/Returning(projection) on the mutation builder instead of the identity-function form (batch output source).");

        if (_hasResult)
            throw new InvalidOperationException("A result-bearing query has already been added; add captures before Query()/AddQuery().");

        if (!ReferenceEquals(context, _context))
            throw new ArgumentException("A batch statement is bound to a different data context; build every statement from the batch's context.", nameof(variableName));

        if (_captures.ContainsKey(name))
            throw new InvalidOperationException(
                $"A batch output source named '{name}' is already registered on this batch; every table-variable capture needs a unique name (batch output source).");

        return name;
    }

    private BatchBuilder RegisterCapture(string name, Type rowType, QueryCommand shape, MutationCommand command, string columnDefinitions)
    {
        _steps.Add(BatchStepSpec.ForCapture(command, name, columnDefinitions));
        _captures.Add(name, new CaptureRegistration(rowType, shape));
        return this;
    }

    private QueryCommand BuildTypedCaptureShape<TResult>(System.Linq.Expressions.LambdaExpression? projection, Type entityType, string operation)
    {
        if (projection is null)
            throw new InvalidOperationException(
                $"A batch output source needs a returning projection for this {operation}; call Returning() or Returning(projection) on the mutation builder (batch output source).");

        return MutationReadShape.BuildShape<TResult>(_context, projection, entityType);
    }

    private (string Name, CaptureRegistration Registration) RequireCapture(string variableName, string parameterName)
    {
        var name = NormalizeCaptureVariableName(variableName, parameterName);
        if (!_captures.TryGetValue(name, out var registration))
            throw new InvalidOperationException(
                $"'{variableName}' is not a batch output source captured earlier on this batch; register it with OutputIntoTableVariable(...) before reading it (batch output source).");

        return (name, registration);
    }

    // A root or joined source whose name starts with '@' names a table variable, and the only valid
    // table variable is a capture registered earlier on this batch. This also catches a command built
    // over another batch's source and a use-before-capture at Query()/AddQuery() time.
    private void RequireCapturedSources(QueryCommand command)
    {
        RequireCapturedSource(command.From);

        if (command.Joins is { } joins)
        {
            for (var i = 0; i < joins.Length; i++)
                RequireCapturedSource(joins[i].From);
        }
    }

    private void RequireCapturedSource(FromExpression? from)
    {
        if (from?.Table is not { Length: > 1 } table || table[0] != '@')
            return;

        if (!_captures.ContainsKey(table))
            throw new InvalidOperationException(
                $"'{table}' is not a batch output source registered earlier on this batch; a table-variable source must be captured with OutputIntoTableVariable(...) on the same batch (batch output source).");
    }

    // One documented table-variable identifier with an optional leading '@', normalised once to the
    // canonical '@name'. A bracketed, quoted, whitespace-bearing or statement-injecting form is not a
    // table variable and is refused.
    private static string NormalizeCaptureVariableName(string variableName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            throw new ArgumentException("A batch output source needs a table-variable name (with an optional leading '@').", parameterName);

        var name = variableName[0] == '@' ? variableName : "@" + variableName;
        if (!IsTableVariableName(name))
            throw new ArgumentException(
                $"'{variableName}' is not a valid table-variable name for a batch output source. It must start with a letter or underscore and contain only letters, digits and underscores; a bracketed or quoted identifier is not a table variable.",
                parameterName);

        return name;
    }

    private static bool IsTableVariableName(string name)
    {
        if (name.Length < 2 || name[0] != '@')
            return false;

        var first = name[1];
        if (first != '_' && (first < 'A' || first > 'Z') && (first < 'a' || first > 'z'))
            return false;

        for (var i = 2; i < name.Length; i++)
        {
            var c = name[i];
            if (c != '_' && !char.IsLetterOrDigit(c))
                return false;
        }

        return true;
    }

    private sealed class CaptureRegistration
    {
        public CaptureRegistration(Type rowType, QueryCommand shape)
        {
            RowType = rowType;
            Shape = shape;
        }

        public Type RowType { get; }

        public QueryCommand Shape { get; }
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
/// The result object itself enumerates its result sets: <c>foreach (var set in result)</c> or
/// <c>await foreach (var set in result)</c> yields a <see cref="ResultSet"/> cursor per set. The legacy
/// <see cref="Read{TResult}"/> path reads sets positionally.
/// </summary>
public sealed class BatchResult : IEnumerable<ResultSet>, IAsyncEnumerable<ResultSet>, IResultSetCursorSource
{
    private readonly IReadOnlyList<IList> _sets;
    private readonly IReadOnlyList<Type> _resultTypes;
    private readonly IReadOnlyList<string[]> _columnNames;
    private int _index;
    private int _cursorIndex = -1;
    private bool _legacyReadUsed;
    private bool _setsStarted;
    private bool _setsEnumerated;
    private bool _traversalActive;
    private bool _currentCursorConsumed;

    internal BatchResult(IReadOnlyList<IList> sets, IReadOnlyList<Type> resultTypes, IReadOnlyList<string[]> columnNames, int recordsAffected)
    {
        _sets = sets;
        _resultTypes = resultTypes;
        _columnNames = columnNames;
        RecordsAffected = recordsAffected;
    }

    /// <summary>The number of result sets the batch produced, in the order the queries were added.</summary>
    public int ResultSetCount => _sets.Count;

    /// <summary>
    /// The number of rows the batch's statements affected, as reported by the driver in one total, or
    /// <c>-1</c> when the batch has no affected-row metadata. The value is captured when the batch
    /// reader is released and is part of the eagerly materialised result, so this instance owns no
    /// reader: it never touches a database, is safe to read at any time, and is <em>not</em> derived
    /// from <see cref="ResultSetCount"/> or from the number of materialised rows.
    /// </summary>
    public int RecordsAffected { get; }

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
    /// <see cref="ResultSetCount"/> to know how many sets exist. This positional path cannot be mixed with
    /// enumerating the result object.
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no further result set, the next set's projected type is not <typeparamref name="TResult"/>, or the result was enumerated.</exception>
    public IReadOnlyList<TResult> Read<TResult>()
    {
        ThrowIfSetTraversalStarted();
        _legacyReadUsed = true;

        if (_index >= _sets.Count)
            throw new InvalidOperationException($"The batch has {_sets.Count} result set(s); every result set has already been read.");

        var expected = _resultTypes[_index];
        if (expected != typeof(TResult))
            throw new InvalidOperationException($"Result set {_index} projects '{expected.Name}', not '{typeof(TResult).Name}'; read it with the matching type.");

        var set = (IReadOnlyList<TResult>)_sets[_index];
        _index++;
        return set;
    }

    /// <summary>
    /// Returns an enumerator over the buffered result sets of the batch in the order the queries were
    /// added, yielding a <see cref="ResultSet"/> cursor per set. Each set can be consumed once with
    /// <see cref="ResultSet.Read{T}"/> or <see cref="ResultSet.ReadAsync{T}"/>.
    /// </summary>
    /// <returns>A one-shot, forward-only sequence of result-set cursors; empty when the batch has none.</returns>
    /// <exception cref="InvalidOperationException">The result was already enumerated or read with <see cref="Read{TResult}"/>.</exception>
    public IEnumerator<ResultSet> GetEnumerator()
    {
        BeginSetTraversal();
        return EnumerateSets().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns an async enumerator over the buffered result sets of the batch. See <see cref="GetEnumerator"/>;
    /// the batch is eager, so this never touches a reader.
    /// </summary>
    /// <param name="cancellationToken">Cancels the traversal between sets.</param>
    /// <returns>A one-shot, forward-only async sequence of result-set cursors; empty when the batch has none.</returns>
    /// <exception cref="InvalidOperationException">The result was already enumerated or read with <see cref="Read{TResult}"/>.</exception>
    public IAsyncEnumerator<ResultSet> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        BeginSetTraversal();
        return EnumerateSetsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    private IEnumerable<ResultSet> EnumerateSets()
    {
        if (_setsEnumerated)
            throw AlreadyEnumerated();

        _setsEnumerated = true;
        _traversalActive = true;
        try
        {
            for (var i = 0; i < _sets.Count; i++)
            {
                _cursorIndex = i;
                _currentCursorConsumed = false;
                yield return new ResultSet(this, i, _columnNames[i]);

                InvalidateCurrentCursor();
            }
        }
        finally
        {
            InvalidateCurrentCursor();
            _traversalActive = false;
        }
    }

    private async IAsyncEnumerable<ResultSet> EnumerateSetsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_setsEnumerated)
            throw AlreadyEnumerated();

        _setsEnumerated = true;
        _traversalActive = true;
        try
        {
            for (var i = 0; i < _sets.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _cursorIndex = i;
                _currentCursorConsumed = false;
                yield return new ResultSet(this, i, _columnNames[i]);

                InvalidateCurrentCursor();
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
        finally
        {
            InvalidateCurrentCursor();
            _traversalActive = false;
        }
    }

    IReadOnlyList<T> IResultSetCursorSource.ReadCurrentSet<T>(ResultSet cursor)
    {
        var set = BeginCursorRead(cursor);
        var expected = _resultTypes[cursor.Index];
        if (expected != typeof(T))
            throw new InvalidOperationException($"Result set {cursor.Index} projects '{expected.Name}', not '{typeof(T).Name}'; read it with the matching type.");

        return (IReadOnlyList<T>)set;
    }

    IAsyncEnumerable<T> IResultSetCursorSource.ReadCurrentSetAsync<T>(ResultSet cursor, CancellationToken cancellationToken)
        => ReadCurrentSetAsyncCore<T>(cursor, cancellationToken);

    private async IAsyncEnumerable<T> ReadCurrentSetAsyncCore<T>(ResultSet cursor, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Mirrors ProcedureResult: consume the set when enumeration starts, not when the method is called.
        var set = (IReadOnlyList<T>)((IResultSetCursorSource)this).ReadCurrentSet<T>(cursor);
        foreach (var item in set)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private IList BeginCursorRead(ResultSet cursor)
    {
        if (!cursor.IsInitialized
            || !ReferenceEquals(cursor.Source, this)
            || !_traversalActive
            || cursor.Index != _cursorIndex)
        {
            throw new InvalidOperationException("The result-set cursor is stale: the outer traversal has advanced or ended, and a cursor is valid only while its set is current.");
        }

        if (_currentCursorConsumed)
            throw new InvalidOperationException("This result set has already been read; each set can be read once, either eagerly or lazily.");

        _currentCursorConsumed = true;
        return _sets[cursor.Index];
    }

    private void InvalidateCurrentCursor()
    {
        _cursorIndex = -1;
        _currentCursorConsumed = false;
    }

    private void BeginSetTraversal()
    {
        if (_legacyReadUsed)
            throw new InvalidOperationException("Result sets were already read with Read<TResult>(); a batch supports a single traversal style.");

        if (_setsStarted)
            throw new InvalidOperationException("Result sets were already enumerated; each batch supports a single traversal.");

        _setsStarted = true;
    }

    private void ThrowIfSetTraversalStarted()
    {
        if (_setsStarted)
            throw new InvalidOperationException("Result sets are being enumerated; Read<TResult>() cannot be mixed with that traversal.");
    }

    private static InvalidOperationException AlreadyEnumerated()
        => new("This result-set enumeration has already been consumed; each batch supports a single traversal.");
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

/// <summary>
/// Builds the prepared column-shape command a mutation read projects over. It is shared by the two
/// row-capturing mutation reads — the PostgreSQL data-modifying CTE (<c>INSERT ... RETURNING</c>) and the
/// SQL Server batch capture (<c>OUTPUT ... INTO</c>) — so it is reached by name instead of through a
/// <c>MutationCteQuery&lt;object&gt;</c> instantiation, which is generic for no reason of its own.
/// </summary>
internal static class MutationReadShape
{
    /// <summary>
    /// Builds the shape of a read over the rows an insert terminal returns. A terminal without an explicit
    /// selector is read through its key member, which is the column its <c>RETURNING</c>/<c>OUTPUT</c> list
    /// carries.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type inserted.</typeparam>
    /// <typeparam name="TReturn">The row shape the returning terminal projects.</typeparam>
    /// <param name="dataContext">The context the read executes on.</param>
    /// <param name="insert">The insert terminal whose returned columns define the shape.</param>
    /// <returns>The prepared shape command.</returns>
    internal static QueryCommand BuildShape<TEntity, TReturn>(IDataContext dataContext, InsertReturningBuilder<TEntity, TReturn> insert)
    {
        var projection = insert.Projection ?? BuildKeyProjection<TEntity, TReturn>(insert);
        return BuildShape<TReturn>(dataContext, projection, typeof(TEntity));
    }

    /// <summary>
    /// Builds the prepared column-shape command a mutation read uses: a projection over the mutated entity
    /// that mirrors the returned columns, so member access resolves to the returned columns.
    /// </summary>
    /// <typeparam name="TShape">The row shape the read projects.</typeparam>
    /// <param name="dataContext">The context the read executes on.</param>
    /// <param name="projection">The returning selector that defines the projected columns.</param>
    /// <param name="sourceType">The mapped entity type the selector reads.</param>
    /// <param name="joinedSource">The joined source of a multi-table mutation read, or <see langword="null"/>.</param>
    /// <returns>The prepared shape command.</returns>
    internal static QueryCommand BuildShape<TShape>(IDataContext dataContext, System.Linq.Expressions.LambdaExpression projection, Type sourceType, QueryCommand? joinedSource = null)
    {
        // An identity projection has no column-producing body for the normal
        // projector path, so the shape is built as a bare projection over TShape and expanded by
        // TryBuildProjectionSelectList into slot-tagged item columns (mirroring the mutation's
        // RETURNING list). Non-identity shapes keep their selector.
        var isIdentity = TypeFacts.UnwrapConvert(projection.Body) is System.Linq.Expressions.ParameterExpression;
        var shape = dataContext.CreateCommand<TShape>(new QueryDefinition
        {
            Exp = isIdentity ? null : projection,
            SrcType = isIdentity ? typeof(TShape) : sourceType,
            Joins = joinedSource?.Joins,
        });
        // Mark the identity shape BEFORE preparation so TryBuildProjectionSelectList tags every item
        // column with its deterministic, collision-free per-slot alias before the column plan hash is
        // finalized. Assigning it afterwards (after PrepareCommand) would leave PlanHashCode and the
        // command's ColumnsPlanHash describing a shape without the alias.
        if (isIdentity)
            shape.IdentitySlotAliases = true;
        if (joinedSource?.From is { } from)
            shape.From = from;
        shape.PrepareCommand(false, CancellationToken.None);

        return shape;
    }

    // ReturningKey<TKey>() carries no selector; reconstruct the single key member access so the read
    // can still be typed (the key type was validated against TKey when the returning builder was built).
    private static System.Linq.Expressions.LambdaExpression BuildKeyProjection<TEntity, TReturn>(InsertReturningBuilder<TEntity, TReturn> insert)
    {
        var columns = insert.ReturningColumns;
        if (columns.Count == 0)
            throw new NotSupportedException(
                "A data-modifying CTE requires RETURNING columns: ReturningIdentity<TKey>() names no column. Project the returned columns instead, e.g. Return(x => new { x.Id }).");

        var property = columns[0].PropertyInfo;
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "x");
        return System.Linq.Expressions.Expression.Lambda(System.Linq.Expressions.Expression.MakeMemberAccess(parameter, property), parameter);
    }
}
