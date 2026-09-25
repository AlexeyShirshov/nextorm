namespace NextORM.Core;

/// <summary>
/// Fluent terminal for an <c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c> whose modified rows are written into
/// an existing table through SQL Server's <c>OUTPUT ... INTO &lt;target&gt;(columns)</c> clause instead of
/// being returned to the client. Started with <c>OutputInto(...)</c> on an
/// <see cref="InsertReturningBuilder{TEntity, TResult}"/>, <see cref="UpdateReturningBuilder{TEntity, TResult}"/>
/// or <see cref="DeleteReturningBuilder{TEntity, TResult}"/>.
/// <para>
/// The builder deliberately has no row terminals: nothing is returned to the client, so
/// <see cref="Execute"/> reports the affected-row count and <see cref="ToSql"/> renders the statement.
/// To also return the rows to the client, use the separate <c>OutputIntoThenOutput(...)</c> form.
/// </para>
/// <para>
/// The target is an explicit table name (nextorm does not declare a table variable in phase 1), and its
/// columns must have the same names as the selected output columns: the selected column list is reused as
/// the target column list.
/// </para>
/// </summary>
public sealed class OutputIntoBuilder
{
    private readonly IOutputIntoMutation _mutation;
    private readonly string _targetTable;

    internal OutputIntoBuilder(IOutputIntoMutation mutation, string targetTable)
    {
        _mutation = mutation;
        _targetTable = targetTable;
    }

    /// <summary>Executes the statement and returns the number of rows written into the target.</summary>
    /// <returns>The number of affected rows, as reported by the provider.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>OUTPUT ... INTO</c>, or the context is read-only.</exception>
    public int Execute() => RequireExecutor().Execute(BuildCommand());

    /// <summary>Asynchronously executes the statement and returns the number of rows written into the target.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of affected rows.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>OUTPUT ... INTO</c>, or the context is read-only.</exception>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => RequireExecutor().Execute(BuildCommand(), cancellationToken);

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

    private MutationCommand BuildCommand() => _mutation.BuildOutputIntoCommand(_targetTable);

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
    /// <summary>Builds the command that writes the modified rows into <paramref name="targetTable"/> and returns nothing to the client.</summary>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>The mutation command carrying the output-into target.</returns>
    MutationCommand BuildOutputIntoCommand(string targetTable);

    /// <summary>The context the mutation executes on.</summary>
    IDataContext DataContext { get; }
}
