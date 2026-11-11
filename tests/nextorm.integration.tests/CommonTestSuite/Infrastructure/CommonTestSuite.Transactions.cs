using System.Data;
using System.Data.Common;
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
            ctx.CreateInsertBuilder<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 1).Insert();
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
            ctx.CreateInsertBuilder<IInsertEntity>().Value(x => x.Name, marker).Value(x => x.Age, 2).Insert();
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

    [Fact]
    public void TryBeginTransaction_WhenNone_ShouldStart()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;

        var started = transactions.TryBeginTransaction(out var tx);

        started.Should().BeTrue();
        tx.Should().NotBeNull();
        transactions.CurrentTransaction.Should().BeSameAs(tx);
        tx!.Rollback();
        tx.Dispose();
    }

    [Fact]
    public void TryBeginTransaction_WhenActive_ShouldReturnTheActiveOne()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        using var tx = transactions.BeginTransaction();

        var started = transactions.TryBeginTransaction(out var returned);

        started.Should().BeFalse();
        returned.Should().BeSameAs(tx);
        tx.Rollback();
    }

    [Fact]
    public async Task TryBeginTransactionAsync_WhenNone_ShouldStart()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;

        var (started, tx) = await transactions.TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeTrue();
        tx.Should().NotBeNull();
        transactions.CurrentTransaction.Should().BeSameAs(tx);
        tx!.Rollback();
        tx.Dispose();
    }

    [Fact]
    public async Task TryBeginTransactionAsync_WhenActive_ShouldReturnTheActiveOne()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        using var tx = transactions.BeginTransaction();

        var (started, returned) = await transactions.TryBeginTransactionAsync(TestContext.Current.CancellationToken);

        started.Should().BeFalse();
        returned.Should().BeSameAs(tx);
        tx.Rollback();
    }

    // ------------------------------------------------------------------------------------------------
    // Savepoints (T289). One live fact per matrix cell, each run in the synchronous and the asynchronous
    // form. The transaction role is the only API under test and every assertion reads rows back from the
    // database, so a driver that silently ignores a savepoint cannot pass.
    // ------------------------------------------------------------------------------------------------

    private static string SavepointMarker() => "sp_" + Guid.NewGuid().ToString("N")[..16];

    private static string SavepointName(string suffix) => "sp_" + suffix + Guid.NewGuid().ToString("N")[..8];

    private void RequireSavepoints()
    {
        Assert.SkipUnless(Provider.SupportsTransactions, "This provider does not support transactions.");
        Assert.SkipUnless(Provider.SupportsSavepoints, "This provider does not support savepoints.");
    }

    private static async Task<DbTransaction> BeginSavepointTransaction(ITransactionManager transactions, bool asyncForm) =>
        asyncForm
            ? await transactions.BeginTransactionAsync(TestContext.Current.CancellationToken)
            : transactions.BeginTransaction();

    private static async Task InsertSavepointRow(IDataContext ctx, bool asyncForm, string? name, int age)
    {
        var builder = ctx.CreateInsertBuilder<IInsertEntity>().Value(x => x.Name, name).Value(x => x.Age, age);
        if (asyncForm)
            await builder.InsertAsync(TestContext.Current.CancellationToken);
        else
            _ = builder.Insert();
    }

    private static async Task<int> CountSavepointRows(IDataContext ctx, bool asyncForm, string name)
    {
        var ids = asyncForm
            ? await ctx.From<IInsertEntity>().Where(x => x.Name == name).Select(x => x.Id).ToListAsync(TestContext.Current.CancellationToken)
            : ctx.From<IInsertEntity>().Where(x => x.Name == name).Select(x => x.Id).ToList();
        return ids.Count;
    }

    private static async Task CreateSavepoint(ITransactionManager transactions, bool asyncForm, string name)
    {
        if (asyncForm)
            await transactions.CreateSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            transactions.CreateSavepoint(name);
    }

    private static async Task RollbackToSavepoint(ITransactionManager transactions, bool asyncForm, string name)
    {
        if (asyncForm)
            await transactions.RollbackToSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            transactions.RollbackToSavepoint(name);
    }

    private static async Task ReleaseSavepoint(ITransactionManager transactions, bool asyncForm, string name)
    {
        if (asyncForm)
            await transactions.ReleaseSavepointAsync(name, TestContext.Current.CancellationToken);
        else
            transactions.ReleaseSavepoint(name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_Create_ShouldLeaveTheTransactionUsable(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var keep = SavepointMarker();
        var after = SavepointMarker();

        using (var tx = await BeginSavepointTransaction(transactions, asyncForm))
        {
            await InsertSavepointRow(ctx, asyncForm, keep, 1);
            await CreateSavepoint(transactions, asyncForm, SavepointName("create"));
            await InsertSavepointRow(ctx, asyncForm, after, 2);
            tx.Commit();
        }

        (await CountSavepointRows(ctx, asyncForm, keep)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, after)).Should().Be(1,
            "creating a savepoint must neither end nor roll back the transaction");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_RollbackTo_ShouldDiscardOnlyLaterRows(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var keep = SavepointMarker();
        var discard = SavepointMarker();
        var point = SavepointName("rollback");

        using var tx = await BeginSavepointTransaction(transactions, asyncForm);
        await InsertSavepointRow(ctx, asyncForm, keep, 1);
        await CreateSavepoint(transactions, asyncForm, point);
        await InsertSavepointRow(ctx, asyncForm, discard, 2);
        await RollbackToSavepoint(transactions, asyncForm, point);

        (await CountSavepointRows(ctx, asyncForm, keep)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, discard)).Should().Be(0);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_ReleaseThenCommit_ShouldPersistBothRows(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var before = SavepointMarker();
        var after = SavepointMarker();
        var point = SavepointName("release");

        using (var tx = await BeginSavepointTransaction(transactions, asyncForm))
        {
            await InsertSavepointRow(ctx, asyncForm, before, 1);
            await CreateSavepoint(transactions, asyncForm, point);
            await InsertSavepointRow(ctx, asyncForm, after, 2);
            await ReleaseSavepoint(transactions, asyncForm, point);
            tx.Commit();
        }

        (await CountSavepointRows(ctx, asyncForm, before)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, after)).Should().Be(1,
            "releasing a savepoint must not roll back the work done after it");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_Nested_ShouldRollBackToTheInnerAndOuterPoints(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var baseline = SavepointMarker();
        var outerRow = SavepointMarker();
        var innerRow = SavepointMarker();
        var outer = SavepointName("outer");
        var inner = SavepointName("inner");

        using var tx = await BeginSavepointTransaction(transactions, asyncForm);
        await InsertSavepointRow(ctx, asyncForm, baseline, 1);
        await CreateSavepoint(transactions, asyncForm, outer);
        await InsertSavepointRow(ctx, asyncForm, outerRow, 2);
        await CreateSavepoint(transactions, asyncForm, inner);
        await InsertSavepointRow(ctx, asyncForm, innerRow, 3);

        await RollbackToSavepoint(transactions, asyncForm, inner);

        (await CountSavepointRows(ctx, asyncForm, baseline)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, outerRow)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, innerRow)).Should().Be(0);

        await RollbackToSavepoint(transactions, asyncForm, outer);

        (await CountSavepointRows(ctx, asyncForm, baseline)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, outerRow)).Should().Be(0);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_CreateThenRollbackWithNoCommandBetween_ShouldKeepTheTransactionUsable(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var before = SavepointMarker();
        var after = SavepointMarker();
        var point = SavepointName("nocmd");

        using (var tx = await BeginSavepointTransaction(transactions, asyncForm))
        {
            await InsertSavepointRow(ctx, asyncForm, before, 1);
            await CreateSavepoint(transactions, asyncForm, point);
            await RollbackToSavepoint(transactions, asyncForm, point);
            await InsertSavepointRow(ctx, asyncForm, after, 2);
            tx.Commit();
        }

        (await CountSavepointRows(ctx, asyncForm, before)).Should().Be(1);
        (await CountSavepointRows(ctx, asyncForm, after)).Should().Be(1,
            "Npgsql defers SAVEPOINT until the next command; the immediate rollback must still leave the transaction usable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_RollbackTo_ShouldPreserveNullsInPreSavepointRows(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var age = Random.Shared.Next(1_000_000_000, int.MaxValue);
        var point = SavepointName("nulls");

        using var tx = await BeginSavepointTransaction(transactions, asyncForm);
        await InsertSavepointRow(ctx, asyncForm, name: null, age);
        await CreateSavepoint(transactions, asyncForm, point);
        await InsertSavepointRow(ctx, asyncForm, SavepointMarker(), age);
        await RollbackToSavepoint(transactions, asyncForm, point);

        var names = asyncForm
            ? await ctx.From<IInsertEntity>().Where(x => x.Age == age).Select(x => x.Name).ToListAsync(TestContext.Current.CancellationToken)
            : ctx.From<IInsertEntity>().Where(x => x.Age == age).Select(x => x.Name).ToList();

        names.Should().ContainSingle("the row created after the savepoint must be gone").Which.Should().BeNull();
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_NoActiveTransaction_ShouldThrowInvalidOperation(bool asyncForm)
    {
        RequireSavepoints();

        var transactions = (ITransactionManager)_sut.DataProvider;
        var point = SavepointName("noactive");

        var act = async () =>
        {
            if (asyncForm)
                await transactions.CreateSavepointAsync(point, TestContext.Current.CancellationToken);
            else
                transactions.CreateSavepoint(point);
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active transaction*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_InvalidNames_ShouldThrowArgumentException(bool asyncForm)
    {
        RequireSavepoints();

        var transactions = (ITransactionManager)_sut.DataProvider;
        using var tx = await BeginSavepointTransaction(transactions, asyncForm);

        string[] invalid = ["", "bad name", "1sp", "sp-name", "sp;drop", new string('a', 33)];
        foreach (var name in invalid)
        {
            if (asyncForm)
            {
                var asyncAct = async () => await transactions.CreateSavepointAsync(name, TestContext.Current.CancellationToken);
                await asyncAct.Should().ThrowAsync<ArgumentException>();
            }
            else
            {
                var act = () => transactions.CreateSavepoint(name);
                act.Should().Throw<ArgumentException>();
            }
        }

        if (asyncForm)
        {
            var nullAsyncAct = async () => await transactions.CreateSavepointAsync(null!, TestContext.Current.CancellationToken);
            await nullAsyncAct.Should().ThrowAsync<ArgumentNullException>();
        }
        else
        {
            var nullAct = () => transactions.CreateSavepoint(null!);
            nullAct.Should().Throw<ArgumentNullException>();
        }

        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_DuplicateName_ShouldBeDelegatedWithoutATrackingLayer(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var keep = SavepointMarker();
        var point = SavepointName("dup");

        using var tx = await BeginSavepointTransaction(transactions, asyncForm);
        await InsertSavepointRow(ctx, asyncForm, keep, 1);
        await CreateSavepoint(transactions, asyncForm, point);
        await CreateSavepoint(transactions, asyncForm, point);
        await InsertSavepointRow(ctx, asyncForm, SavepointMarker(), 2);
        await RollbackToSavepoint(transactions, asyncForm, point);

        // Shadowing a duplicate name is provider-defined (MySQL replaces it, PostgreSQL and SQLite keep
        // the newest); only the portable guarantee is asserted: the two creates are delegated unchanged
        // and the row written before them survives.
        (await CountSavepointRows(ctx, asyncForm, keep)).Should().Be(1);
        tx.Rollback();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Savepoint_EnlistedTransaction_ShouldReceiveSavepointsWithoutChangingOwnership(bool asyncForm)
    {
        RequireSavepoints();

        var ctx = _sut.DataProvider;
        var transactions = (ITransactionManager)ctx;
        var keep = SavepointMarker();
        var discard = SavepointMarker();
        var point = SavepointName("enlisted");

        ((DataContext)ctx).EnsureConnectionOpen();
        var connection = ((DataContext)ctx).GetConnection();
        using var raw = asyncForm
            ? await connection.BeginTransactionAsync(TestContext.Current.CancellationToken)
            : connection.BeginTransaction();

        transactions.UseTransaction(raw);
        try
        {
            await InsertSavepointRow(ctx, asyncForm, keep, 1);
            await CreateSavepoint(transactions, asyncForm, point);
            await InsertSavepointRow(ctx, asyncForm, discard, 2);
            await RollbackToSavepoint(transactions, asyncForm, point);

            (await CountSavepointRows(ctx, asyncForm, keep)).Should().Be(1);
            (await CountSavepointRows(ctx, asyncForm, discard)).Should().Be(0);
            raw.Connection.Should().NotBeNull("the context must never finish a caller-owned transaction");
        }
        finally
        {
            transactions.UseTransaction(null);
            if (asyncForm)
                await raw.RollbackAsync(TestContext.Current.CancellationToken);
            else
                raw.Rollback();
        }

        transactions.CurrentTransaction.Should().BeNull();
    }
}
