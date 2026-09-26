using System.Collections;
using System.Data;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Execution role of a database-backed context for multi-statement batches (see the public
/// <c>BatchExtensions.Batch</c> entry). Kept out of
/// <see cref="IDataContext"/> like the mutation role, so the in-memory context is not forced to
/// implement it and the public entry points reject a context that does not implement the role.
/// <para>
/// Rendering lives on the context (it owns the planner and the dialect); the primitive that turns a
/// rendered plan into one round trip lives on <c>QueryExecutor</c>.
/// </para>
/// </summary>
internal interface IBatchExecutor
{
    /// <summary>
    /// Renders every step with one shared parameter provider so placeholder names do not collide across
    /// statements, and validates the batch against the dialect's capability flags.
    /// </summary>
    /// <param name="steps">The steps in execution order; the result-bearing query is last.</param>
    /// <returns>The rendered statements and the result-bearing query.</returns>
    /// <exception cref="NotSupportedException">The dialect has no batch form, or a materialisation option is unsupported.</exception>
    BatchPlan RenderBatch(IReadOnlyList<BatchStepSpec> steps);

    /// <summary>
    /// Renders the batch that materialises every temporary table <paramref name="command"/> reads
    /// (drop and re-create each), followed by the command itself. Used by the lazy
    /// <c>AsTempTable</c> read path and by its <c>ToBatchSql</c> inspection form.
    /// </summary>
    /// <param name="command">The command that reads the temporary table(s).</param>
    /// <returns>The rendered batch.</returns>
    /// <exception cref="InvalidOperationException">The command reads no temporary table.</exception>
    /// <exception cref="NotSupportedException">The dialect has no batch form or cannot express a temporary materialisation.</exception>
    BatchPlan RenderTemporaryTableBatch(QueryCommand command);

    /// <summary>Builds the row mapper for the plan's result-bearing query.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <returns>The compiled row mapper.</returns>
    Func<IDataRecord, TResult> BuildBatchMapper<TResult>(BatchPlan plan);

    /// <summary>Builds the row mapper for the result set at <paramref name="resultIndex"/>.</summary>
    /// <typeparam name="TResult">The projected row type of that result set.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="resultIndex">The zero-based position of the result set in <see cref="BatchPlan.Results"/>.</param>
    /// <returns>The compiled row mapper.</returns>
    /// <exception cref="InvalidOperationException">The result set at <paramref name="resultIndex"/> does not project <typeparamref name="TResult"/>.</exception>
    Func<IDataRecord, TResult> BuildBatchMapper<TResult>(BatchPlan plan, int resultIndex);

    /// <summary>
    /// Executes the batch in one round trip and eagerly materialises every result set. Each
    /// <paramref name="resultMaterializers"/> entry reads the reader positioned on its result set and
    /// returns the materialised rows as a non-generic <see cref="IList"/>.
    /// </summary>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="resultMaterializers">One materialiser per result set, in result-set order.</param>
    /// <returns>The eagerly materialised result sets.</returns>
    BatchResult ExecuteBatchResults(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> resultMaterializers);

    /// <summary>
    /// Asynchronously executes the batch in one round trip and eagerly materialises every result set.
    /// </summary>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="resultMaterializers">One materialiser per result set, in result-set order.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the eagerly materialised result sets.</returns>
    Task<BatchResult> ExecuteBatchResultsAsync(BatchPlan plan, IReadOnlyList<IBatchResultMaterializer> resultMaterializers, CancellationToken cancellationToken);

    /// <summary>Executes the batch in one round trip and materialises the result rows.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <returns>The materialised result rows.</returns>
    List<TResult> ExecuteBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper);

    /// <summary>Asynchronously executes the batch in one round trip and materialises the result rows.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the materialised result rows.</returns>
    Task<List<TResult>> ExecuteBatchAsync<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken);

    /// <summary>Streams the batch's result rows asynchronously.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <param name="mapper">The compiled row mapper.</param>
    /// <param name="cancellationToken">Cancels enumeration.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    IAsyncEnumerable<TResult> StreamBatch<TResult>(BatchPlan plan, Func<IDataRecord, TResult> mapper, CancellationToken cancellationToken);
}

/// <summary>
/// One requested batch step. A step is the result-bearing read query, a
/// <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c> materialisation, or a side-effecting DML command
/// (<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c>/<c>TRUNCATE</c>). Result-bearing steps must be the trailing
/// group of steps: no side-effecting step may follow a result.
/// </summary>
internal sealed class BatchStepSpec
{
    private BatchStepSpec(QueryCommand? query, CreateTableAsCommand? createTableAs, MutationCommand? mutation, string? rawSql)
    {
        Query = query;
        CreateTableAs = createTableAs;
        Mutation = mutation;
        RawSql = rawSql;
    }

    /// <summary>The result-bearing read query, or <see langword="null"/> for a non-result step.</summary>
    public QueryCommand? Query { get; }

    /// <summary>The materialisation command of the step, or <see langword="null"/> for a result, DML or raw step.</summary>
    public CreateTableAsCommand? CreateTableAs { get; }

    /// <summary>The side-effecting DML command of the step, or <see langword="null"/> for a result, materialisation or raw step.</summary>
    public MutationCommand? Mutation { get; }

    /// <summary>The verbatim SQL of a raw side-effecting step (for example <c>DROP TABLE IF EXISTS</c>), or <see langword="null"/> for the other step kinds.</summary>
    public string? RawSql { get; }

    /// <summary>Creates the result-bearing read-query step.</summary>
    /// <param name="query">The query whose rows form the batch result.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForResult(QueryCommand query) => new(query, null, null, null);

    /// <summary>Creates a materialisation step.</summary>
    /// <param name="command">The materialisation command.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForCreateTableAs(CreateTableAsCommand command) => new(null, command, null, null);

    /// <summary>Creates a side-effecting DML step.</summary>
    /// <param name="command">The DML command.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForMutation(MutationCommand command) => new(null, null, command, null);

    /// <summary>Creates a verbatim side-effecting SQL step. The SQL carries no parameters.</summary>
    /// <param name="sql">The statement text.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForRaw(string sql) => new(null, null, null, sql);
}

/// <summary>
/// One rendered result set of a batch: its result-bearing query and the SQL that produced it, kept in
/// the order the result queries were added.
/// </summary>
internal sealed class BatchResultSpec
{
    /// <summary>Creates a result spec.</summary>
    /// <param name="query">The result-bearing read query.</param>
    /// <param name="sql">The SQL of that query as rendered within the batch.</param>
    public BatchResultSpec(QueryCommand query, string sql)
    {
        Query = query;
        Sql = sql;
    }

    /// <summary>The result-bearing read query.</summary>
    public QueryCommand Query { get; }

    /// <summary>The SQL of the query as rendered within the batch.</summary>
    public string Sql { get; }
}

/// <summary>
/// A rendered batch: the statements with their (globally unique) parameters plus the ordered result
/// specs. Produced by <see cref="IBatchExecutor.RenderBatch"/>. The result-bearing queries are the
/// trailing statements and only they may return rows; every preceding statement is side-effecting — a
/// materialisation or a DML mutation — which a reader exposes as a result set without columns.
/// <see cref="ResultQuery"/> and <see cref="ResultSql"/> expose the first result so the single-result
/// terminal keeps working unchanged.
/// </summary>
internal sealed class BatchPlan
{
    /// <summary>Creates a rendered plan.</summary>
    /// <param name="statements">The rendered statements, in execution order.</param>
    /// <param name="results">The result specs, in result-set order; at least one.</param>
    /// <param name="useJoinedCommand">When <see langword="true"/>, execute the statements as one <c>;</c>-joined command even if the connection exposes a <see cref="System.Data.Common.DbBatch"/>.</param>
    /// <param name="multiline">When <see langword="true"/>, <see cref="ToSql"/> places each statement on its own line.</param>
    public BatchPlan(IReadOnlyList<BatchStatement> statements, IReadOnlyList<BatchResultSpec> results, bool useJoinedCommand, bool multiline = false)
    {
        Statements = statements;
        Results = results;
        ResultQuery = results[0].Query;
        ResultSql = results[0].Sql;
        UseJoinedCommand = useJoinedCommand;
        Multiline = multiline;
    }

    /// <summary>The rendered statements, in execution order.</summary>
    public IReadOnlyList<BatchStatement> Statements { get; }

    /// <summary>The result specs, in result-set order.</summary>
    public IReadOnlyList<BatchResultSpec> Results { get; }

    /// <summary>The first result-bearing query; its <see cref="BatchResultSpec.Sql"/> is <see cref="ResultSql"/>.</summary>
    public QueryCommand ResultQuery { get; }

    /// <summary>The SQL of the first result-bearing statement.</summary>
    public string ResultSql { get; }

    /// <summary>When <see langword="true"/>, the statements execute as one <c>;</c>-joined command regardless of <see cref="System.Data.Common.DbConnection.CanCreateBatch"/>.</summary>
    public bool UseJoinedCommand { get; }

    /// <summary>When <see langword="true"/>, <see cref="ToSql"/> separates statements with a newline instead of a space.</summary>
    public bool Multiline { get; }

    /// <summary>Renders the whole batch as one <c>;</c>-joined SQL string (statements separated by <c>"; "</c>, or <c>";\n"</c> when <see cref="Multiline"/>), for inspection.</summary>
    /// <returns>The joined SQL.</returns>
    public string ToSql()
    {
        if (Statements.Count == 0)
            return string.Empty;

        if (Statements.Count == 1)
            return Statements[0].Sql;

        var length = 2 * (Statements.Count - 1);
        for (var i = 0; i < Statements.Count; i++)
            length += Statements[i].Sql.Length;

        return string.Create(length, (Statements, Multiline), static (span, state) =>
        {
            var (statements, multiline) = state;
            var separator = multiline ? '\n' : ' ';
            var offset = 0;
            for (var i = 0; i < statements.Count; i++)
            {
                if (i > 0)
                {
                    span[offset++] = ';';
                    span[offset++] = separator;
                }

                var sql = statements[i].Sql;
                sql.AsSpan().CopyTo(span[offset..]);
                offset += sql.Length;
            }
        });
    }
}

/// <summary>A single rendered batch statement: its SQL text and the parameters it references.</summary>
internal sealed class BatchStatement
{
    /// <summary>Creates a rendered statement.</summary>
    /// <param name="sql">The parameterised SQL text.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    public BatchStatement(string sql, IReadOnlyList<Parameter> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    /// <summary>The parameterised SQL text.</summary>
    public string Sql { get; }

    /// <summary>The parameters referenced by <see cref="Sql"/>.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }
}
