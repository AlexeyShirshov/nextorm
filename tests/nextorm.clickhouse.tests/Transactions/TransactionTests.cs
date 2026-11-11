using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse speaks HTTP and has no ADO.NET transaction, so its dialect reports
/// <see cref="ISqlDialect.SupportsTransactions"/> as false and the transaction role rejects it.
/// </summary>
public class TransactionTests
{
    [Fact]
    public void CurrentTransaction_IsNullInitially()
    {
        using var ctx = ClickHouseTestContext.Create();

        ((ITransactionManager)ctx).CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public void BeginTransaction_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).BeginTransaction();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = async () => await ((ITransactionManager)ctx).BeginTransactionAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void CreateSavepoint_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).CreateSavepoint("sp_1");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task CreateSavepointAsync_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = async () => await ((ITransactionManager)ctx).CreateSavepointAsync("sp_1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void RollbackToSavepoint_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).RollbackToSavepoint("sp_1");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task RollbackToSavepointAsync_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = async () => await ((ITransactionManager)ctx).RollbackToSavepointAsync("sp_1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void ReleaseSavepoint_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).ReleaseSavepoint("sp_1");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ReleaseSavepointAsync_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = async () => await ((ITransactionManager)ctx).ReleaseSavepointAsync("sp_1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void UseTransaction_WithNull_IsAllowed()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).UseTransaction(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void TryBeginTransaction_ShouldReturnFalseWithoutThrowing()
    {
        using var ctx = ClickHouseTestContext.Create();

        var started = ((ITransactionManager)ctx).TryBeginTransaction(out var tx);

        started.Should().BeFalse();
        tx.Should().BeNull();
    }

    [Fact]
    public async Task TryBeginTransactionAsync_ShouldReturnFalseWithoutThrowing()
    {
        using var ctx = ClickHouseTestContext.Create();

        var (started, tx) = await ((ITransactionManager)ctx)
            .TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeFalse();
        tx.Should().BeNull();
    }
}
