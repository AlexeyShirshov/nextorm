using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void RowLock_SkipLocked_ShouldSkipRowsLockedByAnotherTransaction()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var reader = _sut.DataProvider;
        Assert.SkipUnless(((DataContext)reader).Dialect.Lock is not null, "This provider does not support row locking.");

        using var locker = Provider.CreateContext();
        var transactions = (ITransactionManager)locker;
        var tx = transactions.BeginTransaction();

        try
        {
            locker.From<ISimpleEntity>()
                .Where(x => x.Id == 1)
                .ForUpdate()
                .Select(x => x.Id)
                .ToList()
                .Should().Equal(1);

            reader.From<ISimpleEntity>()
                .Where(x => x.Id == 1)
                .ForUpdate(LockWaitMode.SkipLocked)
                .Select(x => x.Id)
                .ToList()
                .Should().BeEmpty();
        }
        finally
        {
            tx.Rollback();
        }
    }
}
