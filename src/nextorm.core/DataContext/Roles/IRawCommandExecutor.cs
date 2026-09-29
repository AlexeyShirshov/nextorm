namespace NextORM.Core;

/// <summary>
/// Executes a raw parameterised command (not only <c>SELECT</c>) and exposes its result sets, output
/// parameters and return value. This is the foundation of stored-procedure support: the same
/// <see cref="ProcedureParameter"/> shape carries input, output and structured parameters.
/// </summary>
/// <remarks>
/// The default interface implementations throw <see cref="NotSupportedException"/>, so a provider that
/// cannot run a given command rejects it without declaring the role; the in-memory context rejects both
/// raw commands and stored procedures through these defaults. <see cref="IDataContext"/> inherits this
/// role. SQLite and ClickHouse reject stored procedures before opening a connection through
/// <see cref="ISqlDialect.SupportsStoredProcedures"/> (the <c>DataContext</c> capability gate), not
/// through the default implementation.
/// <para>
/// <b>SQL injection.</b> The command text is executed verbatim, without parameterisation by the
/// planner. Never concatenate untrusted input; pass values through <see cref="ProcedureParameter"/>.
/// </para>
/// <para>
/// <b>Return values.</b> <see cref="ProcedureResult.ReturnValue"/> is populated only for a
/// stored-procedure command type issued through <see cref="ExecuteProcedure(string, IReadOnlyList{ProcedureParameter})"/>,
/// and only where the provider has a return status (SQL Server). Text commands are provider-dependent
/// and typically do not set it — use an <see cref="System.Data.ParameterDirection.Output"/> parameter
/// instead.
/// </para>
/// <para>
/// <b>Open reader.</b> The returned <see cref="ProcedureResult"/> keeps the command and reader open
/// until disposed. On SQL Server without MARS an open reader blocks other commands on the same
/// connection; dispose the result before issuing another command on the context.
/// </para>
/// <para>
/// <b>Scalars and NULL.</b> A scalar <c>Read&lt;T&gt;()</c> of SQL <c>NULL</c> returns <c>default</c>;
/// read through a nullable <c>T</c> (for example <c>int?</c>) to observe the <c>NULL</c>.
/// </para>
/// </remarks>
public interface IRawCommandExecutor
{
    /// <summary>
    /// Executes <paramref name="sql"/> with <paramref name="parameters"/> and returns a
    /// <see cref="ProcedureResult"/> owning the reader and command.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>The result, which must be disposed to release the reader and command.</returns>
    ProcedureResult ExecuteRaw(string sql, params IReadOnlyList<ProcedureParameter> parameters)
        => throw new NotSupportedException($"{GetType().Name} does not support raw SQL execution.");

    /// <summary>
    /// Asynchronously executes <paramref name="sql"/> with <paramref name="parameters"/> and returns a
    /// <see cref="ProcedureResult"/> owning the reader and command.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    Task<ProcedureResult> ExecuteRawAsync(string sql, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default)
        => Task.FromException<ProcedureResult>(new NotSupportedException($"{GetType().Name} does not support raw SQL execution."));

    /// <summary>
    /// Asynchronously executes <paramref name="sql"/> with <paramref name="parameters"/>, using
    /// <see langword="default"/> for the cancellation token. Convenience overload of
    /// <see cref="ExecuteRawAsync(string, IReadOnlyList{ProcedureParameter}, CancellationToken)"/>.
    /// </summary>
    /// <param name="sql">The command text to execute.</param>
    /// <param name="parameters">The parameters referenced by <paramref name="sql"/>.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    Task<ProcedureResult> ExecuteRawAsync(string sql, params IReadOnlyList<ProcedureParameter> parameters)
        => ExecuteRawAsync(sql, parameters, default);

    /// <summary>
    /// Executes the stored procedure <paramref name="name"/> with <paramref name="parameters"/> and
    /// returns a <see cref="ProcedureResult"/> owning the reader and command.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <returns>The result, which must be disposed to release the reader and command.</returns>
    /// <remarks>
    /// The name is not escaped, quoted or parameterised; never pass untrusted input as the name.
    /// Parameter names are supplied without the provider's prefix. PostgreSQL maps this to a
    /// <c>CALL</c> (procedures only; call functions through
    /// <see cref="ExecuteRaw(string, IReadOnlyList{ProcedureParameter})"/>). A return value is
    /// populated only where the provider has one (SQL Server).
    /// </remarks>
    ProcedureResult ExecuteProcedure(string name, params IReadOnlyList<ProcedureParameter> parameters)
        => throw new NotSupportedException($"{GetType().Name} does not support stored procedures.");

    /// <summary>
    /// Asynchronously executes the stored procedure <paramref name="name"/> with
    /// <paramref name="parameters"/> and returns a <see cref="ProcedureResult"/> owning the reader and
    /// command. See <see cref="ExecuteProcedure(string, IReadOnlyList{ProcedureParameter})"/>.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    Task<ProcedureResult> ExecuteProcedureAsync(string name, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default)
        => Task.FromException<ProcedureResult>(new NotSupportedException($"{GetType().Name} does not support stored procedures."));

    /// <summary>
    /// Asynchronously executes the stored procedure <paramref name="name"/> with
    /// <paramref name="parameters"/>, using <see langword="default"/> for the cancellation token.
    /// Convenience overload of
    /// <see cref="ExecuteProcedureAsync(string, IReadOnlyList{ProcedureParameter}, CancellationToken)"/>.
    /// </summary>
    /// <param name="name">The procedure name, passed to the provider as-is.</param>
    /// <param name="parameters">The procedure parameters (input, output, input/output and return value).</param>
    /// <returns>A task producing the result, which must be disposed to release the reader and command.</returns>
    Task<ProcedureResult> ExecuteProcedureAsync(string name, params IReadOnlyList<ProcedureParameter> parameters)
        => ExecuteProcedureAsync(name, parameters, default);
}
