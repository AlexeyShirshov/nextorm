using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC03 — under <see cref="PreparedCommandCache.None"/> every execution mints a fresh command and
/// disposes it exactly once at the end of that execution's ownership lifetime. The cases below cover
/// buffered execution, a complete streaming enumeration, an early stream disposal and the two
/// exceptional stream exits (a read failure and a cancellation), because a leaked command on any of
/// those paths would keep a provider connection bound to a dead plan.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandNoneLifetimeTests
{
    [Fact]
    public void NonePolicy_BufferedExecution_MintsAndDisposesOneCommandPerExecution()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.None);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            DbCommand? previous = null;

            for (var i = 1; i <= 3; i++)
            {
                ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
                prepared.CreatedCommandCount.Should().Be(i, "every execution mints a fresh command");
                prepared.DisposedCommandCount.Should().Be(i, "the finished execution disposes exactly its own command");
                prepared.PeekCommand(connection).Should().BeNull("the None policy never publishes a command for reuse");
                prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the lease is released after the execution");
                connection.CreatedCommandCount.Should().Be(i);

                var current = connection.LastCommand;
                current.DisposeCount.Should().Be(1, "a command is disposed exactly once");
                if (previous is not null)
                    current.Should().NotBeSameAs(previous!, "no command may be reused across executions");
                previous = current;
            }

            connection.DisposedCommandCount.Should().Be(3, "three executions disposed exactly three commands");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NonePolicy_FullStreamingEnumeration_DisposesTheCommandOnceAtTheEnd()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.None,
                static record => record.GetInt32(0));
            var connectionManager = new ProbeConnectionManager(connection);
            var enumerator = PreparedCommandHarness.CreateEnumerator(prepared, connectionManager, cancellationToken: TestContext.Current.CancellationToken);
            var ids = new List<int>();

            while (enumerator.MoveNext())
                ids.Add(enumerator.Current);

            ids.Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(1);
            connection.LastCommand.DisposeCount.Should().Be(0, "the stream still owns the command while it is readable");

            enumerator.Dispose();

            connection.LastCommand.DisposeCount.Should().Be(1, "the command is released exactly once at stream end");
            prepared.PeekCommand(connection).Should().BeNull("no command is retained under None");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the stream released its lease");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NonePolicy_EarlyStreamDisposal_DisposesOnce_AndTheNextExecutionSucceeds()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.None,
                static record => record.GetInt32(0));
            var connectionManager = new ProbeConnectionManager(connection);
            var enumerator = PreparedCommandHarness.CreateEnumerator(prepared, connectionManager, cancellationToken: TestContext.Current.CancellationToken);

            enumerator.MoveNext().Should().BeTrue("the first row opens the reader and holds the lease");
            var first = connection.LastCommand;
            first.DisposeCount.Should().Be(0, "the live stream still owns its command");
            prepared.GetLeaseOwnerForTest(connection).Should().NotBe(0, "an open stream holds the connection lease");

            enumerator.Dispose();

            first.DisposeCount.Should().Be(1, "the abandoned stream disposes its command exactly once");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "abandoning the stream releases the lease");
            prepared.PeekCommand(connection).Should().BeNull("None never publishes a command");

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), connectionManager);
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 },
                "a released lease lets the same prepared command execute again");
            prepared.CreatedCommandCount.Should().Be(2, "the retry mints its own fresh command");
            connection.LastCommand.Should().NotBeSameAs(first);
            first.DisposeCount.Should().Be(1, "the abandoned command is never disposed a second time");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NonePolicy_ReadFailure_DisposesTheCommandExactlyOnce()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.None,
                static record => record.GetInt32(0));
            var enumerator = PreparedCommandHarness.CreateEnumerator(prepared, new ProbeConnectionManager(connection), cancellationToken: TestContext.Current.CancellationToken);
            connection.ReaderWrapper = inner => new ProbeDbDataReader(inner) { ThrowOnReadNumber = 2 };

            enumerator.MoveNext().Should().BeTrue();
            var command = connection.LastCommand;

            FluentActions.Invoking(() => enumerator.MoveNext())
                .Should().Throw<InvalidOperationException>("the provider read failure must surface");

            enumerator.Dispose();

            command.DisposeCount.Should().Be(1, "an exceptional stream exit still disposes the command exactly once");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the failed stream released its lease");
            prepared.PeekCommand(connection).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NonePolicy_CancellationDuringRead_DisposesTheCommandExactlyOnce()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.None,
                static record => record.GetInt32(0));
            var enumerator = PreparedCommandHarness.CreateEnumerator(prepared, new ProbeConnectionManager(connection), cancellationToken: TestContext.Current.CancellationToken);
            connection.ReaderWrapper = inner => new ProbeDbDataReader(inner) { CancelOnReadNumber = 2 };

            (await enumerator.MoveNextAsync()).Should().BeTrue();
            var command = connection.LastCommand;

            var act = async () => await enumerator.MoveNextAsync();
            await act.Should().ThrowAsync<OperationCanceledException>("the cancellation must surface");

            await enumerator.DisposeAsync();

            command.DisposeCount.Should().Be(1, "a canceled stream disposes its command exactly once");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the canceled stream released its lease");
            prepared.PeekCommand(connection).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
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
