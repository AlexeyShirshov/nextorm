using System.Data;
using System.Data.Common;

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
}
