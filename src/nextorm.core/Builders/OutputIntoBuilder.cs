namespace NextORM.Core;

/// <summary>
/// Fluent terminal for an <c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c> whose modified rows are written into a
/// target through SQL Server's <c>OUTPUT ... INTO &lt;target&gt;(columns)</c> clause instead of being
/// returned to the client. Started with <c>OutputInto(...)</c> or <c>OutputIntoTableVariable(...)</c> on an
/// <see cref="InsertReturningBuilder{TEntity, TResult}"/>, <see cref="UpdateReturningBuilder{TEntity, TResult}"/>
/// or <see cref="DeleteReturningBuilder{TEntity, TResult}"/>.
/// <para>
/// The builder deliberately has no row terminals: nothing is returned to the client, so
/// <see cref="Execute"/> reports the affected-row count and <see cref="ToSql"/> renders the statement.
/// To also return the rows to the client, use the separate <c>OutputIntoThenOutput(...)</c> form.
/// </para>
/// <para>
/// The target is either an explicit table name that must already exist, or a table variable declared in
/// the same batch. In both forms the selected output columns are reused as the target column list, so the
/// target columns must have the same names as the selected output columns. A table variable additionally
/// takes a caller-supplied, trusted column-definition text; the declaration text is embedded verbatim and
/// never parameterised.
/// </para>
/// </summary>
public sealed class OutputIntoBuilder
{
    private readonly IOutputIntoMutation _mutation;
    private readonly OutputIntoClause _target;

    internal OutputIntoBuilder(IOutputIntoMutation mutation, OutputIntoClause target)
    {
        _mutation = mutation;
        _target = target;
    }

    /// <summary>Executes the statement and returns the number of rows written into the target.</summary>
    /// <returns>The number of affected rows, as reported by the provider.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>OUTPUT ... INTO</c>, or the context is read-only.</exception>
    public int Execute()
    {
        _mutation.ValidateFilters();
        return RequireExecutor().Execute(BuildCommand());
    }

    /// <summary>Asynchronously executes the statement and returns the number of rows written into the target.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of affected rows.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>OUTPUT ... INTO</c>, or the context is read-only.</exception>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await _mutation.ValidateFiltersAsync(cancellationToken).ConfigureAwait(false);
        return await RequireExecutor().Execute(BuildCommand(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the parameterised SQL this statement would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    public string ToSql()
    {
        if (_mutation.DataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_mutation.DataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    private MutationCommand BuildCommand() => _mutation.BuildOutputIntoCommand(_target);

    private IMutationExecutor RequireExecutor()
    {
        if (_mutation.DataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_mutation.DataContext.GetType().Name} does not support data modification. Use a database-backed context (SQL Server) for OUTPUT ... INTO.");
    }
}

/// <summary>
/// The internal seam a returning builder exposes to <see cref="OutputIntoBuilder"/>:
/// build the <c>OUTPUT ... INTO</c>-only command and expose the context it is bound to.
/// </summary>
internal interface IOutputIntoMutation
{
    /// <summary>Builds the command that writes the modified rows into <paramref name="outputInto"/> and returns nothing to the client.</summary>
    /// <param name="outputInto">The output-into target: an existing table or a same-batch table variable.</param>
    /// <returns>The mutation command carrying the output-into target.</returns>
    MutationCommand BuildOutputIntoCommand(OutputIntoClause outputInto);

    /// <summary>The context the mutation executes on.</summary>
    IDataContext DataContext { get; }

    /// <summary>
    /// Validates the written rows against the target entity's active global query filters. The default
    /// is a no-op: an UPDATE/DELETE enforces its filters in the <c>WHERE</c> clause, so only the insert
    /// builder overrides it.
    /// </summary>
    void ValidateFilters()
    {
    }

    /// <summary>
    /// Asynchronously validates the written rows against the target entity's active global query
    /// filters. The default delegates to <see cref="ValidateFilters"/> and completes immediately; the
    /// insert builder overrides it to run its <c>INSERT ... SELECT</c> pre-check asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task that completes when validation has passed.</returns>
    Task ValidateFiltersAsync(CancellationToken cancellationToken)
    {
        ValidateFilters();
        return Task.CompletedTask;
    }
}
