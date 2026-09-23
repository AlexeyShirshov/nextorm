namespace NextORM.Core;

/// <summary>
/// Execution role of a database-backed context for DML commands. Kept out of <see cref="IDataContext"/>
/// on purpose (the same treatment as <see cref="IConnectionManager"/>): the in-memory provider must not
/// be forced to implement a mutation no-op, and the public entry points reject a context that does not
/// implement this role with a clear <see cref="NotSupportedException"/>.
/// </summary>
internal interface IMutationExecutor
{
    /// <summary>Renders the SQL text of <paramref name="command"/> without executing it.</summary>
    /// <param name="command">The mutation command to render.</param>
    /// <returns>The parameterised SQL text.</returns>
    string Render(MutationCommand command);

    /// <summary>
    /// Whether the provider can return generated columns on the insert itself through
    /// <c>RETURNING</c>/<c>OUTPUT</c> (as opposed to the scalar identity-function fallback).
    /// </summary>
    bool SupportsGeneratedColumns { get; }

    /// <summary>Renders the insert plus the provider's scalar identity function, without executing it.</summary>
    /// <param name="command">The mutation command to render.</param>
    /// <returns>The insert SQL followed by the identity-function query.</returns>
    /// <exception cref="NotSupportedException">The provider has no identity function.</exception>
    string RenderIdentityFunction(MutationCommand command);

    /// <summary>Executes <paramref name="command"/> and returns the number of affected rows.</summary>
    /// <param name="command">The mutation command to execute.</param>
    /// <returns>The number of affected rows, as reported by the provider.</returns>
    int Execute(MutationCommand command);

    /// <summary>Asynchronously executes <paramref name="command"/> and returns the number of affected rows.</summary>
    /// <param name="command">The mutation command to execute.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of affected rows.</returns>
    Task<int> Execute(MutationCommand command, CancellationToken cancellationToken);

    /// <summary>Executes <paramref name="command"/> and returns the generated identity value.</summary>
    /// <param name="command">The mutation command to execute; it must carry an identity column.</param>
    /// <returns>The generated key value, boxed.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return a generated identity.</exception>
    object? ExecuteIdentity(MutationCommand command);

    /// <summary>Asynchronously executes <paramref name="command"/> and returns the generated identity value.</summary>
    /// <param name="command">The mutation command to execute; it must carry an identity column.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated key value, boxed.</returns>
    /// <exception cref="NotSupportedException">The provider cannot return a generated identity.</exception>
    Task<object?> ExecuteIdentity(MutationCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Executes <paramref name="command"/> and returns the provider's last generated identity read
    /// through its scalar identity function (<c>SCOPE_IDENTITY()</c>, <c>lastval()</c>,
    /// <c>LAST_INSERT_ID()</c>, <c>last_insert_rowid()</c>), without naming the column.
    /// </summary>
    /// <param name="command">The mutation command to execute.</param>
    /// <returns>The generated identity value, boxed.</returns>
    /// <exception cref="NotSupportedException">The provider has no identity function.</exception>
    object? ExecuteIdentityFunction(MutationCommand command);

    /// <summary>
    /// Asynchronously executes <paramref name="command"/> and returns the provider's last generated
    /// identity read through its scalar identity function, without naming the column.
    /// </summary>
    /// <param name="command">The mutation command to execute.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the generated identity value, boxed.</returns>
    /// <exception cref="NotSupportedException">The provider has no identity function.</exception>
    Task<object?> ExecuteIdentityFunction(MutationCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Executes <paramref name="command"/> and materialises the rows it returns through
    /// <c>RETURNING</c>/<c>OUTPUT</c>.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="command">The mutation command to execute; it must carry returned columns.</param>
    /// <param name="selectList">The returned columns, in result-set order.</param>
    /// <param name="oneColumn">Whether the projection is a single scalar column.</param>
    /// <returns>The returned rows, materialized in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>RETURNING</c>/<c>OUTPUT</c>.</exception>
    IReadOnlyList<TResult> ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn);

    /// <summary>
    /// Asynchronously executes <paramref name="command"/> and materialises the rows it returns through
    /// <c>RETURNING</c>/<c>OUTPUT</c>.
    /// </summary>
    /// <typeparam name="TResult">The materialized row type.</typeparam>
    /// <param name="command">The mutation command to execute; it must carry returned columns.</param>
    /// <param name="selectList">The returned columns, in result-set order.</param>
    /// <param name="oneColumn">Whether the projection is a single scalar column.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the returned rows, materialized in result-set order.</returns>
    /// <exception cref="NotSupportedException">The provider cannot express <c>RETURNING</c>/<c>OUTPUT</c>.</exception>
    Task<IReadOnlyList<TResult>> ExecuteReturning<TResult>(MutationCommand command, SelectExpression[] selectList, bool oneColumn, CancellationToken cancellationToken);
}
