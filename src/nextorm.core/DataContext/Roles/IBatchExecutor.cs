using System.Data;

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

    /// <summary>Builds the row mapper for the plan's result-bearing query.</summary>
    /// <typeparam name="TResult">The projected row type.</typeparam>
    /// <param name="plan">The rendered plan.</param>
    /// <returns>The compiled row mapper.</returns>
    Func<IDataRecord, TResult> BuildBatchMapper<TResult>(BatchPlan plan);

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
/// (<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c>/<c>TRUNCATE</c>). The result query must be the last step.
/// </summary>
internal sealed class BatchStepSpec
{
    private BatchStepSpec(QueryCommand? query, CreateTableAsCommand? createTableAs, MutationCommand? mutation)
    {
        Query = query;
        CreateTableAs = createTableAs;
        Mutation = mutation;
    }

    /// <summary>The result-bearing read query, or <see langword="null"/> for a non-result step.</summary>
    public QueryCommand? Query { get; }

    /// <summary>The materialisation command of the step, or <see langword="null"/> for a result or DML step.</summary>
    public CreateTableAsCommand? CreateTableAs { get; }

    /// <summary>The side-effecting DML command of the step, or <see langword="null"/> for a result or materialisation step.</summary>
    public MutationCommand? Mutation { get; }

    /// <summary>Creates the result-bearing read-query step.</summary>
    /// <param name="query">The query whose rows form the batch result.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForResult(QueryCommand query) => new(query, null, null);

    /// <summary>Creates a materialisation step.</summary>
    /// <param name="command">The materialisation command.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForCreateTableAs(CreateTableAsCommand command) => new(null, command, null);

    /// <summary>Creates a side-effecting DML step.</summary>
    /// <param name="command">The DML command.</param>
    /// <returns>The step.</returns>
    public static BatchStepSpec ForMutation(MutationCommand command) => new(null, null, command);
}

/// <summary>
/// A rendered batch: the statements with their (globally unique) parameters plus the result-bearing
/// query. Produced by <see cref="IBatchExecutor.RenderBatch"/>. The result-bearing query is always the
/// last statement and only it may return rows; every preceding statement is side-effecting — a
/// materialisation or a DML mutation — which a reader exposes as a result set without columns.
/// </summary>
internal sealed class BatchPlan
{
    /// <summary>Creates a rendered plan.</summary>
    /// <param name="statements">The rendered statements, in execution order.</param>
    /// <param name="resultQuery">The result-bearing query; must be the last statement's query.</param>
    /// <param name="resultSql">The result-bearing statement's SQL.</param>
    /// <param name="useJoinedCommand">When <see langword="true"/>, execute the statements as one <c>;</c>-joined command even if the connection exposes a <see cref="System.Data.Common.DbBatch"/>.</param>
    /// <param name="multiline">When <see langword="true"/>, <see cref="ToSql"/> places each statement on its own line.</param>
    public BatchPlan(IReadOnlyList<BatchStatement> statements, QueryCommand resultQuery, string resultSql, bool useJoinedCommand, bool multiline = false)
    {
        Statements = statements;
        ResultQuery = resultQuery;
        ResultSql = resultSql;
        UseJoinedCommand = useJoinedCommand;
        Multiline = multiline;
    }

    /// <summary>The rendered statements, in execution order.</summary>
    public IReadOnlyList<BatchStatement> Statements { get; }

    /// <summary>The result-bearing query; the last statement's query.</summary>
    public QueryCommand ResultQuery { get; }

    /// <summary>The result-bearing statement's SQL.</summary>
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
