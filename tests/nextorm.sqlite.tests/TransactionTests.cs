using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Pins the <see cref="ITransactionManager"/> contract for a database-backed context: starting and
/// enlisting a transaction, binding it to every execution path, ownership, and cleanup. A caller-supplied
/// in-memory connection is reused by the context, so it is the single place the data lives and can be
/// inspected directly after the context is disposed.
/// </summary>
public class TransactionTests
{
    private static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var setup = conn.CreateCommand();
        setup.CommandText = "create table simple_entity (id integer primary key);";
        setup.ExecuteNonQuery();
        return conn;
    }

    private static SqliteDataContext ContextFor(SqliteConnection connection)
        => new(connection, new DataContextBuilder());

    private static int RawCount(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "select count(*) from simple_entity";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public void CurrentTransaction_IsNullInitially()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ((ITransactionManager)ctx).CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public void BeginTransaction_StartsAndExposesTheTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();

        transactions.CurrentTransaction.Should().BeSameAs(tx);
        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void BeginTransaction_WithIsolationLevel_StartsTheTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction(IsolationLevel.Serializable);

        tx.IsolationLevel.Should().Be(IsolationLevel.Serializable);
    }

    [Fact]
    public async Task BeginTransactionAsync_StartsAndExposesTheTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        await using var tx = await transactions.BeginTransactionAsync(TestContext.Current.CancellationToken);

        transactions.CurrentTransaction.Should().BeSameAs(tx);
    }

    [Fact]
    public void BeginTransaction_WhenAlreadyActive_Throws()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();

        var act = () => transactions.BeginTransaction();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Insert_InsideTransaction_IsVisibleBeforeCommit()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();
        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 7 }).Insert();

        ctx.From<ISimpleEntity>().Where(x => x.Id == 7).Select(x => x.Id).ToList().Should().Equal(7);

        tx.Rollback();

        // A completed transaction is dropped lazily, so the next query runs outside it.
        ctx.From<ISimpleEntity>().Where(x => x.Id == 7).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public async Task Insert_InsideStreamedTransaction_IsVisibleBeforeCommit()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();
        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 9 }).Insert();

        var ids = new List<int>();
        await foreach (var id in ctx.From<ISimpleEntity>().Where(x => x.Id == 9).Select(x => x.Id).ToAsyncEnumerable(TestContext.Current.CancellationToken))
            ids.Add(id);

        ids.Should().Equal(9);
        tx.Rollback();
    }

    [Fact]
    public void Commit_PersistsTheRows()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();
        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 8 }).Insert();
        tx.Commit();

        transactions.CurrentTransaction.Should().BeNull();
        RawCount(conn).Should().Be(1);
    }

    [Fact]
    public void CreateCommand_BindsTheActiveTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();
        using var cmd = ctx.CreateCommand("select 1");

        cmd.Transaction.Should().BeSameAs(tx);
    }

    [Fact]
    public void UseTransaction_EnlistsAnExternalTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var external = conn.BeginTransaction();
        transactions.UseTransaction(external);

        transactions.CurrentTransaction.Should().BeSameAs(external);

        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 10 }).Insert();
        ctx.From<ISimpleEntity>().Where(x => x.Id == 10).Select(x => x.Id).ToList().Should().ContainSingle();

        external.Rollback();

        ctx.From<ISimpleEntity>().Where(x => x.Id == 10).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void UseTransaction_WithNull_DetachesAnExternalTransaction()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var external = conn.BeginTransaction();
        transactions.UseTransaction(external);

        transactions.UseTransaction(null);

        transactions.CurrentTransaction.Should().BeNull();
        external.Rollback();
    }

    [Fact]
    public void UseTransaction_WithNull_WhenContextOwned_Throws()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var tx = transactions.BeginTransaction();

        var act = () => transactions.UseTransaction(null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UseTransaction_WithAnotherConnection_Throws()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var other = OpenConnection();
        using var external = other.BeginTransaction();

        var act = () => transactions.UseTransaction(external);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_WithContextOwnedTransaction_RollsItBack()
    {
        using var conn = OpenConnection();
        var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        transactions.BeginTransaction();
        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 11 }).Insert();
        RawCount(conn).Should().Be(1);

        ctx.Dispose();

        RawCount(conn).Should().Be(0);
        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void Dispose_WithExternalTransaction_LeavesItToTheCaller()
    {
        using var conn = OpenConnection();
        var ctx = ContextFor(conn);
        var transactions = (ITransactionManager)ctx;

        using var external = conn.BeginTransaction();
        transactions.UseTransaction(external);
        ctx.InsertInto<ISimpleEntity>().Values(new SimpleEntity { Id = 12 }).Insert();

        ctx.Dispose();

        external.Connection.Should().NotBeNull();

        external.Rollback();
        RawCount(conn).Should().Be(0);
    }
}
