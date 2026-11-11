using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace NextORM.Core;

/// <summary>
/// Owns the transaction associated with the context's connection: starting one, enlisting in a
/// caller-supplied one, and exposing the active transaction to the execution path. Deliberately
/// separate from <see cref="IConnectionManager"/> (SRP) and not part of <see cref="IDataContext"/>,
/// so the in-memory context is not forced to implement it.
/// </summary>
/// <remarks>
/// Only one transaction is active at a time: starting a second one, or enlisting a transaction while
/// one is active, throws <see cref="InvalidOperationException"/>. Like the underlying connection, the
/// role is not thread-safe.
/// </remarks>
public interface ITransactionManager
{
    /// <summary>The active transaction, or <see langword="null"/> when none is enlisted.</summary>
    DbTransaction? CurrentTransaction { get; }

    /// <summary>Starts a transaction on the context's connection with the provider's default isolation level.</summary>
    /// <returns>The started transaction.</returns>
    DbTransaction BeginTransaction();

    /// <summary>Starts a transaction on the context's connection with the given isolation level.</summary>
    /// <param name="isolationLevel">The isolation level to request from the provider.</param>
    /// <returns>The started transaction.</returns>
    DbTransaction BeginTransaction(IsolationLevel isolationLevel);

    /// <summary>Asynchronously starts a transaction on the context's connection with the provider's default isolation level.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task producing the started transaction.</returns>
    Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Asynchronously starts a transaction on the context's connection with the given isolation level.</summary>
    /// <param name="isolationLevel">The isolation level to request from the provider.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task producing the started transaction.</returns>
    Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enlists an externally owned transaction (for example the one behind
    /// <c>DbContext.Database.CurrentTransaction</c>). The context never commits, rolls back or
    /// disposes it. Pass <see langword="null"/> to detach such an enlisted transaction; a transaction
    /// started with <see cref="BeginTransaction()"/> must be committed, rolled back or disposed instead.
    /// </summary>
    /// <param name="transaction">The transaction to enlist, or <see langword="null"/> to detach the current one.</param>
    void UseTransaction(DbTransaction? transaction);

    /// <summary>
    /// Creates a savepoint with the given name on the active (or enlisted) transaction, so a later
    /// <see cref="RollbackToSavepoint"/> can undo only the work done after it.
    /// </summary>
    /// <remarks>
    /// The call is delegated to the provider's ADO.NET transaction. Savepoints are not tracked by
    /// nextorm: name uniqueness and nesting are the caller's responsibility, and a duplicate valid
    /// name is passed to the provider unchanged.
    /// <para>
    /// Declared as a default interface method so existing implementations keep compiling; an
    /// implementation that does not support savepoints throws <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    /// <param name="name">
    /// The savepoint name: 1-32 characters, ASCII <c>[A-Za-z_][A-Za-z0-9_]*</c>. Any other name
    /// throws <see cref="ArgumentException"/>.
    /// </param>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public void CreateSavepoint(string name) =>
        throw new NotSupportedException("CreateSavepoint is not supported by this ITransactionManager implementation.");

    /// <summary>Asynchronously creates a savepoint with the given name on the active (or enlisted) transaction.</summary>
    /// <remarks>Declared as a default interface method so existing implementations keep compiling.</remarks>
    /// <param name="name">The savepoint name: 1-32 ASCII identifier characters.</param>
    /// <param name="cancellationToken">Token passed to the provider's asynchronous savepoint call.</param>
    /// <returns>A task that completes once the savepoint has been created.</returns>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("CreateSavepointAsync is not supported by this ITransactionManager implementation.");

    /// <summary>
    /// Rolls the active (or enlisted) transaction back to the savepoint with the given name, discarding
    /// only the work done after it.
    /// </summary>
    /// <remarks>
    /// Declared as a default interface method so existing implementations keep compiling. Rolling back
    /// to a savepoint does not end the transaction and does not release the savepoint unless the
    /// provider does so.
    /// </remarks>
    /// <param name="name">The savepoint name: 1-32 ASCII identifier characters.</param>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public void RollbackToSavepoint(string name) =>
        throw new NotSupportedException("RollbackToSavepoint is not supported by this ITransactionManager implementation.");

    /// <summary>Asynchronously rolls the active (or enlisted) transaction back to the savepoint with the given name.</summary>
    /// <remarks>Declared as a default interface method so existing implementations keep compiling.</remarks>
    /// <param name="name">The savepoint name: 1-32 ASCII identifier characters.</param>
    /// <param name="cancellationToken">Token passed to the provider's asynchronous rollback call.</param>
    /// <returns>A task that completes once the rollback has been applied.</returns>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("RollbackToSavepointAsync is not supported by this ITransactionManager implementation.");

    /// <summary>
    /// Releases the savepoint with the given name on the active (or enlisted) transaction. Releasing
    /// does not commit: the surrounding transaction still owns the work.
    /// </summary>
    /// <remarks>
    /// Declared as a default interface method so existing implementations keep compiling. The call is
    /// delegated to the provider, so a provider whose release is a no-op (SQL Server) keeps that
    /// behaviour instead of nextorm synthesising SQL.
    /// </remarks>
    /// <param name="name">The savepoint name: 1-32 ASCII identifier characters.</param>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public void ReleaseSavepoint(string name) =>
        throw new NotSupportedException("ReleaseSavepoint is not supported by this ITransactionManager implementation.");

    /// <summary>Asynchronously releases the savepoint with the given name on the active (or enlisted) transaction.</summary>
    /// <remarks>Declared as a default interface method so existing implementations keep compiling.</remarks>
    /// <param name="name">The savepoint name: 1-32 ASCII identifier characters.</param>
    /// <param name="cancellationToken">Token passed to the provider's asynchronous release call.</param>
    /// <returns>A task that completes once the savepoint has been released.</returns>
    /// <exception cref="NotSupportedException">The provider does not support transactions or savepoints.</exception>
    /// <exception cref="InvalidOperationException">No transaction is active on the context.</exception>
    /// <exception cref="ArgumentException">The name is not a valid savepoint name.</exception>
    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("ReleaseSavepointAsync is not supported by this ITransactionManager implementation.");

    /// <summary>
    /// Starts a transaction only when none is active, without throwing when one already is.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="BeginTransaction()"/>, this never throws because a transaction is already
    /// active — it returns the active one instead — and it does not throw when the provider has no
    /// transactions at all (for example ClickHouse): it returns <see langword="false"/> with
    /// <see langword="null"/> in both cases. Any other failure (for instance a connection error) still
    /// propagates.
    /// </remarks>
    /// <param name="transaction">
    /// When this method returns <see langword="true"/>, the transaction it started; when it returns
    /// <see langword="false"/>, the already-active transaction if there is one, otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a transaction was started; <see langword="false"/> when one is
    /// already active (including one enlisted with <see cref="UseTransaction"/>) or the provider does
    /// not support transactions.
    /// </returns>
    public bool TryBeginTransaction([NotNullWhen(true)] out DbTransaction? transaction)
    {
        if ((transaction = CurrentTransaction) is not null)
            return false;

        try
        {
            transaction = BeginTransaction();
            return true;
        }
        catch (NotSupportedException)
        {
            transaction = null;
            return false;
        }
    }

    /// <summary>
    /// Asynchronously starts a transaction only when none is active, without throwing when one already
    /// is. The asynchronous counterpart of <see cref="TryBeginTransaction(out DbTransaction?)"/>; it
    /// reports the outcome as a tuple because an async method cannot have an <c>out</c> parameter.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// <c>(true, transaction)</c> when a transaction was started; <c>(false, transaction)</c> with the
    /// already-active transaction when one is active; <c>(false, null)</c> when the provider does not
    /// support transactions.
    /// </returns>
    public async Task<(bool Started, DbTransaction? Transaction)> TryBeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var current = CurrentTransaction;
        if (current is not null)
            return (false, current);

        try
        {
            return (true, await BeginTransactionAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (NotSupportedException)
        {
            return (false, null);
        }
    }
}
