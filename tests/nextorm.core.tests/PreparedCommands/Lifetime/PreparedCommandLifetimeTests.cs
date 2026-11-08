using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC11 — connection, transaction and resource lifetimes. A changed transaction is rebound on every
/// execution and a completed/foreign one is rejected before the command runs; closing and resetting a
/// connection evicts its state; and an abandoned connection is not rooted by the prepared command, so a
/// bounded garbage-collection loop reclaims both the connection and its weak-table entry.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandLifetimeTests
{
    [Fact]
    public void TransactionBinding_FollowsTheCurrentTransaction_AndRejectsACompletedOne()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var slot = new TransactionSlot();
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection), () => slot.Value);

            // Absent transaction.
            using (var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty))
                owner.Command.Transaction.Should().BeNull();

            // Present transaction.
            var tx1 = new ProbeTransaction(connection);
            slot.Value = tx1;
            using (var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty))
                owner.Command.Transaction.Should().BeSameAs(tx1, "the execution binds the context transaction");
            prepared.PeekCommand(connection)!.Transaction.Should().BeNull(
                "a completed execution detaches the transaction from the reusable command");
            tx1.Commit();

            // Changed transaction: the reusable command must carry the new one, never the completed one.
            slot.Value = null;
            var tx2 = new ProbeTransaction(connection);
            slot.Value = tx2;
            using (var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty))
                owner.Command.Transaction.Should().BeSameAs(tx2, "the changed transaction replaces the completed one");
            prepared.PeekCommand(connection)!.Transaction.Should().BeNull(
                "the completed execution detaches the new transaction too");
            tx2.Commit();

            slot.Value = null;
            var live = prepared.PeekCommand(connection);
            live.Should().NotBeNull("the Connection policy keeps the command after a completed transaction");
            var liveProbe = connection.LastCommand;
            liveProbe.Should().BeSameAs(live);
            var callsBefore = liveProbe.ExecuteReaderCallCount;

            // A disposed transaction is rejected before the command executes and evicts the poisoned binding.
            var disposedTransaction = new ProbeTransaction(connection);
            disposedTransaction.MarkDisposed();
            slot.Value = disposedTransaction;

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<ObjectDisposedException>("a disposed transaction must fail the execution, not bind silently");

            connection.LastCommand.Should().BeSameAs(live);
            liveProbe.ExecuteReaderCallCount.Should().Be(callsBefore, "the rejected transaction never reached the provider");
            liveProbe.DisposeCount.Should().Be(1, "the evicted command is disposed exactly once");
            prepared.LiveStateCount.Should().Be(0);
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);

            slot.Value = null;
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(2, "the recovery mints a fresh command with no stale transaction");
            prepared.PeekCommand(connection)!.Transaction.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CloseReopenAndResetConnection_EvictsTheState_AndTheNextExecutionWorks()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.LiveStateCount.Should().Be(1);
            var first = prepared.PeekCommand(connection);
            first.Should().NotBeNull();

            connection.Close();
            connection.Open();
            ((IDbCommandHolder)prepared).ResetConnection(connection, new ProbeHarnessContext());

            prepared.LiveStateCount.Should().Be(0, "ResetConnection evicts this connection's state");
            prepared.PeekCommand(connection).Should().BeNull();
            ((ProbeSqliteCommand)first!).DisposeCount.Should().Be(1, "the evicted command is disposed exactly once");

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 }, "the reopened connection executes again");
            prepared.CreatedCommandCount.Should().Be(2);
            prepared.PeekCommand(connection).Should().NotBeNull().And.NotBeSameAs(first);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DisposingTheConnection_EvictsTheCachedPlanState_ThroughTheContextSubscription()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());

            // Force the context to obtain (and subscribe to) the provided connection.
            ctx.From<PreparedCommandHarness.PreparedProbeRow>().Select(x => x.Id).First().Should().Be(1);

            var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
            var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
                command, false, true, PreparedCommandCache.Connection, TestContext.Current.CancellationToken);

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.LiveStateCount.Should().Be(1);
            var live = prepared.PeekCommand(connection);
            live.Should().NotBeNull();

            connection.Dispose();

            prepared.LiveStateCount.Should().Be(0, "disposing the connection runs ResetConnection for every cached plan");
            prepared.PeekCommand(connection).Should().BeNull();
            ((ProbeSqliteCommand)live!).DisposeCount.Should().Be(1);

            using var replacement = PreparedCommandHarness.OpenProbe(path);
            var replacementExecutor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(replacement));
            ExecuteIds(replacementExecutor, prepared).Should().Equal(new[] { 1, 2 },
                "the prepared command is reusable on a fresh connection");
            prepared.CreatedCommandCount.Should().Be(2, "a new connection mints its own command");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AbandonedConnection_IsCollected_AndItsWeakTableEntryDies()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var weak = ExecuteOnAbandonedConnection(prepared, path);
            weak.IsAlive.Should().BeTrue("the connection is alive before the collection loop");

            for (var i = 0; i < 10 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            weak.IsAlive.Should().BeFalse(
                "the prepared command must not root an abandoned connection through its weak-table value or memo");
            prepared.LiveStateCount.Should().Be(0,
                "LiveStateCount reads the weak table, so the reclaimed key's entry is gone too");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ExecuteOnAbandonedConnection(DbPreparedQueryCommand<int> prepared, string path)
    {
        var connection = new ProbeSqliteConnection($"Data Source={path}");
        connection.Open();
        var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
        using (var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty))
        {
            owner.Reader.Read().Should().BeTrue();
        }

        prepared.LiveStateCount.Should().Be(1, "the executed connection has a published state");
        return new WeakReference(connection);
    }

    private static List<int> ExecuteIds(QueryExecutor executor, DbPreparedQueryCommand<int> prepared)
    {
        using var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
        var ids = new List<int>();
        while (owner.Reader.Read())
            ids.Add(owner.Reader.GetInt32(0));
        return ids;
    }
}
