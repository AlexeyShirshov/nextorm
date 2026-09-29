using System.Data;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The <c>TryBeginTransaction</c> default interface methods on <see cref="ITransactionManager"/>,
/// exercised against a fake manager so every branch (start / already active / unsupported provider) is
/// covered without a database.
/// </summary>
public class TransactionManagerTryBeginTests
{
    [Fact]
    public void TryBeginTransaction_NoActive_StartsAndReturnsTrue()
    {
        ITransactionManager manager = new FakeTransactionManager();

        var started = manager.TryBeginTransaction(out var transaction);

        started.Should().BeTrue();
        transaction.Should().NotBeNull();
        manager.CurrentTransaction.Should().BeSameAs(transaction);
    }

    [Fact]
    public void TryBeginTransaction_Active_ReturnsFalseWithTheActiveOne()
    {
        ITransactionManager manager = new FakeTransactionManager();
        var existing = manager.BeginTransaction();

        var started = manager.TryBeginTransaction(out var transaction);

        started.Should().BeFalse();
        transaction.Should().BeSameAs(existing);
    }

    [Fact]
    public void TryBeginTransaction_UnsupportedProvider_ReturnsFalseWithNull()
    {
        ITransactionManager manager = new FakeTransactionManager(supportsTransactions: false);

        var started = manager.TryBeginTransaction(out var transaction);

        started.Should().BeFalse();
        transaction.Should().BeNull();
    }

    [Fact]
    public async Task TryBeginTransactionAsync_NoActive_StartsAndReturnsTrue()
    {
        ITransactionManager manager = new FakeTransactionManager();

        var (started, transaction) = await manager.TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeTrue();
        transaction.Should().NotBeNull();
        manager.CurrentTransaction.Should().BeSameAs(transaction);
    }

    [Fact]
    public async Task TryBeginTransactionAsync_Active_ReturnsFalseWithTheActiveOne()
    {
        ITransactionManager manager = new FakeTransactionManager();
        var existing = manager.BeginTransaction();

        var (started, transaction) = await manager.TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeFalse();
        transaction.Should().BeSameAs(existing);
    }

    [Fact]
    public async Task TryBeginTransactionAsync_UnsupportedProvider_ReturnsFalseWithNull()
    {
        ITransactionManager manager = new FakeTransactionManager(supportsTransactions: false);

        var (started, transaction) = await manager.TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeFalse();
        transaction.Should().BeNull();
    }

    private sealed class FakeTransactionManager(bool supportsTransactions = true) : ITransactionManager
    {
        public DbTransaction? CurrentTransaction { get; private set; }

        public DbTransaction BeginTransaction()
        {
            if (!supportsTransactions)
                throw new NotSupportedException("This provider does not support transactions.");

            if (CurrentTransaction is not null)
                throw new InvalidOperationException("Nested transactions are not supported; use the active transaction instead.");

            CurrentTransaction = new FakeDbTransaction();
            return CurrentTransaction;
        }

        public DbTransaction BeginTransaction(IsolationLevel isolationLevel) => BeginTransaction();

        public Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(BeginTransaction());

        public Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
            => Task.FromResult(BeginTransaction());

        public void UseTransaction(DbTransaction? transaction) => CurrentTransaction = transaction;
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

        protected override DbConnection? DbConnection => null;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }
    }
}
