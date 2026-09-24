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
    public void UseTransaction_WithNull_IsAllowed()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ((ITransactionManager)ctx).UseTransaction(null);

        act.Should().NotThrow();
    }
}
