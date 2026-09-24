using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static string TransactionMarker() => "txn_" + Guid.NewGuid().ToString("N");

    [Fact]
    public void Transaction_Commit_ShouldPersistInsert()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var marker = TransactionMarker();

        using (var tx = transactions.BeginTransaction())
        {
            ctx.InsertInto<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 1).Insert();
            tx.Commit();
        }

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(1);
    }

    [Fact]
    public void Transaction_Rollback_ShouldDiscardInsert()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var marker = TransactionMarker();

        using (var tx = transactions.BeginTransaction())
        {
            ctx.InsertInto<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 2).Insert();
            ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().Equal(2);
            tx.Rollback();
        }

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
    }

    [Fact]
    public void Transaction_BeginWithIsolationLevel_ShouldStartAndExpose()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction(IsolationLevel.Serializable);

        transactions.CurrentTransaction.Should().BeSameAs(tx);

        tx.Rollback();
    }

    [Fact]
    public void Transaction_SecondBegin_ShouldThrow()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();

        var act = () => transactions.BeginTransaction();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Transaction_CreateCommand_ShouldBindTheTransaction()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();
        using var cmd = ((DataContext)ctx).CreateCommand("select 1");

        cmd.Transaction.Should().BeSameAs(tx);

        tx.Rollback();
    }
}
