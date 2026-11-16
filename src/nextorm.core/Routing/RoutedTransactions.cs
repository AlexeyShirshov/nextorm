using System.Data;
using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Starts transactions on a routed host instead of the context connection. A transaction belongs to one
/// connection, so binding it to a topology also pins that topology to the chosen host until the
/// transaction ends: every command routed there keeps running inside it.
/// </summary>
/// <remarks>
/// Implemented by <see cref="DataContext"/> and kept out of <see cref="ITransactionManager"/> so that
/// the existing connection-scoped role does not grow members a context may not support.
/// </remarks>
public interface IRoutedTransactionManager
{
    /// <summary>
    /// Starts a transaction on the host <paramref name="target"/> routes <paramref name="scope"/> to and
    /// pins the topology to that host, so later commands routed there join the transaction.
    /// </summary>
    /// <param name="target">The topology to transact on.</param>
    /// <param name="scope">The mapping variant that selects the host, for example a bucket scope.</param>
    /// <param name="isolationLevel">The isolation level to request; <see cref="IsolationLevel.Unspecified"/> uses the provider default.</param>
    /// <returns>The started transaction.</returns>
    /// <exception cref="InvalidOperationException">
    /// The topology is not registered, selected no host for the scope, or another transaction is open.
    /// </exception>
    DbTransaction BeginTransaction(RouteTarget target, MappingVariant scope = default, IsolationLevel isolationLevel = IsolationLevel.Unspecified);

    /// <summary>Asynchronously starts a transaction on the routed host and pins the topology to it.</summary>
    /// <param name="target">The topology to transact on.</param>
    /// <param name="scope">The mapping variant that selects the host.</param>
    /// <param name="isolationLevel">The isolation level to request.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task producing the started transaction.</returns>
    Task<DbTransaction> BeginTransactionAsync(RouteTarget target, MappingVariant scope = default, IsolationLevel isolationLevel = IsolationLevel.Unspecified, CancellationToken cancellationToken = default);

    /// <summary>The transaction currently open on <paramref name="target"/>'s host, or <see langword="null"/>.</summary>
    /// <param name="target">The topology to inspect.</param>
    /// <returns>The active transaction of the host the topology is bound to, if any.</returns>
    DbTransaction? CurrentTransaction(RouteTarget target);
}

/// <summary>
/// Convenience entry points for <see cref="IRoutedTransactionManager"/> on the concrete context, so a
/// caller does not cast to the role for the common case.
/// </summary>
public static class RoutedTransactionExtensions
{
    /// <inheritdoc cref="IRoutedTransactionManager.BeginTransaction(RouteTarget, MappingVariant, IsolationLevel)"/>
    public static DbTransaction BeginTransaction(
        this DataContext context,
        RouteTarget target,
        MappingVariant scope = default,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ((IRoutedTransactionManager)context).BeginTransaction(target, scope, isolationLevel);
    }

    /// <inheritdoc cref="IRoutedTransactionManager.BeginTransactionAsync(RouteTarget, MappingVariant, IsolationLevel, CancellationToken)"/>
    public static Task<DbTransaction> BeginTransactionAsync(
        this DataContext context,
        RouteTarget target,
        MappingVariant scope = default,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ((IRoutedTransactionManager)context).BeginTransactionAsync(target, scope, isolationLevel, cancellationToken);
    }

    /// <inheritdoc cref="IRoutedTransactionManager.CurrentTransaction(RouteTarget)"/>
    public static DbTransaction? GetCurrentTransaction(this DataContext context, RouteTarget target)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ((IRoutedTransactionManager)context).CurrentTransaction(target);
    }
}
