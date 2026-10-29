using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC09 — a failed execution cleans up exactly the command it owns and can never leave a disposed
/// command published for reuse. Fault injection happens at the ADO.NET layer (the instrumented
/// <see cref="ProbeSqliteConnection"/> and its command), so every case reaches the production
/// <c>QueryExecutor</c>/<c>ResultSetEnumerator</c> acquire-fail-release code:
/// <list type="bullet">
/// <item>a reader-creation failure under <c>Connection</c> must evict and dispose the state command and
/// let the next execution mint a fresh one;</item>
/// <item>the same failure under <c>None</c> must dispose the execution-local command and publish nothing;</item>
/// <item>a binding failure after acquisition (foreign/disposed transaction) and a failed command
/// creation must evict, release and recover;</item>
/// <item>cancellation before open and cancellation during reader creation behave like any other
/// pre-transfer failure; cancellation during row reads is a clean release (not a poisoned command) and
/// the next execution still succeeds.</item>
/// </list>
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandFailureOwnershipTests
{
    [Fact]
    public void ConnectionPolicy_ReaderCreationFailure_EvictsDisposesAndRecoversWithAFreshCommand()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.FailExecuteReaderTimes = 1;

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<InvalidOperationException>("the provider execution failure must surface");

            var failed = connection.LastCommand;
            failed.DisposeCount.Should().Be(1, "the poisoned state command is disposed exactly once by eviction");
            prepared.DisposedCommandCount.Should().Be(1);
            prepared.CreatedCommandCount.Should().Be(1);
            prepared.LiveStateCount.Should().Be(0, "eviction removes the whole per-connection state");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the failed lease was released");
            prepared.PeekCommand(connection).Should().BeNull("nothing may stay published after the eviction");

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 },
                "the same connection and prepared command must recover after the failure");
            prepared.CreatedCommandCount.Should().Be(2, "the recovery mints a brand-new command");
            prepared.PeekCommand(connection).Should().BeSameAs(connection.LastCommand).And.NotBeSameAs(failed);
            failed.DisposeCount.Should().Be(1, "the evicted command is never disposed a second time");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConnectionPolicy_AsyncReaderCreationFailure_EvictsDisposesAndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.FailExecuteReaderAsyncTimes = 1;

            var act = async () => await executor.OpenResultReaderAsync(
                prepared, null, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<InvalidOperationException>();

            var failed = connection.LastCommand;
            failed.DisposeCount.Should().Be(1);
            prepared.LiveStateCount.Should().Be(0);
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);
            prepared.PeekCommand(connection).Should().BeNull();

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(2);
            connection.LastCommand.Should().NotBeSameAs(failed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NonePolicy_ReaderCreationFailure_DisposesTheFreshCommandAndPublishesNothing()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.None);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.FailExecuteReaderTimes = 1;

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<InvalidOperationException>();

            var failed = connection.LastCommand;
            failed.DisposeCount.Should().Be(1, "the execution-local command is disposed exactly once");
            prepared.DisposedCommandCount.Should().Be(1);
            prepared.PeekCommand(connection).Should().BeNull("None never publishes a command");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(2);
            connection.LastCommand.Should().NotBeSameAs(failed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConnectionPolicy_BindingFailureAfterAcquisition_EvictsDisposesAndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var otherConnection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var slot = new TransactionSlot { Value = new ProbeTransaction(otherConnection) };
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection), () => slot.Value);

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*does not belong to the executing connection*",
                    "a prepared command never binds a transaction from another connection");

            var failed = connection.LastCommand;
            failed.DisposeCount.Should().Be(1);
            prepared.LiveStateCount.Should().Be(0);
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);
            prepared.PeekCommand(connection).Should().BeNull();
            failed.Transaction.Should().BeNull("the rejected transaction must never stay attached");

            slot.Value = null;
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(2);
            connection.LastCommand.Should().NotBeSameAs(failed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConnectionPolicy_CommandCreationFailure_EvictsTheEmptyStateAndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.FailCreateCommandTimes = 1;

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<InvalidOperationException>("the CreateCommand failure must surface");

            connection.CreatedCommandCount.Should().Be(0, "no command was ever minted");
            prepared.CreatedCommandCount.Should().Be(0);
            prepared.LiveStateCount.Should().Be(0, "the empty state created by acquisition is evicted");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConnectionPolicy_OpenFailure_CreatesNothing_AndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = new ProbeSqliteConnection($"Data Source={path}");
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.OpenFailure = new InvalidOperationException("Simulated connection-open failure.");

            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
            act.Should().Throw<InvalidOperationException>();

            connection.CreatedCommandCount.Should().Be(0);
            prepared.CreatedCommandCount.Should().Be(0, "the open fails before any command is acquired");
            prepared.LiveStateCount.Should().Be(0);

            connection.OpenFailure = null;
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConnectionPolicy_CancellationBeforeOpen_CreatesNothing_AndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = new ProbeSqliteConnection($"Data Source={path}");
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            using var source = new CancellationTokenSource();
            source.Cancel();

            var act = async () => await executor.OpenResultReaderAsync(prepared, null, source.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();

            connection.CreatedCommandCount.Should().Be(0);
            prepared.CreatedCommandCount.Should().Be(0);
            prepared.LiveStateCount.Should().Be(0);
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConnectionPolicy_CancellationDuringReaderCreation_EvictsAndRecovers()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.IntPrepared("select id from pdca_t333_rows order by id", PreparedCommandCache.Connection);
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));

            connection.ExecuteFailure = new OperationCanceledException("Simulated cancellation during reader creation.");
            connection.FailExecuteReaderAsyncTimes = 1;

            var act = async () => await executor.OpenResultReaderAsync(
                prepared, null, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<OperationCanceledException>();

            var failed = connection.LastCommand;
            failed.DisposeCount.Should().Be(1);
            prepared.LiveStateCount.Should().Be(0);
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0);
            prepared.PeekCommand(connection).Should().BeNull();

            connection.ExecuteFailure = new InvalidOperationException("Simulated command-execution failure.");
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(2);
            connection.LastCommand.Should().NotBeSameAs(failed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConnectionPolicy_CancellationDuringRowRead_ReleasesCleanlyAndTheNextExecutionReusesTheCommand()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id",
                PreparedCommandCache.Connection,
                static record => record.GetInt32(0));
            ProbeDbDataReader? reader = null;
            connection.ReaderWrapper = inner => reader = new ProbeDbDataReader(inner) { CancelOnReadNumber = 2 };
            var enumerator = PreparedCommandHarness.CreateEnumerator(
                prepared, new ProbeConnectionManager(connection), cancellationToken: TestContext.Current.CancellationToken);

            (await enumerator.MoveNextAsync()).Should().BeTrue();
            var command = connection.LastCommand;

            var act = async () => await enumerator.MoveNextAsync();
            await act.Should().ThrowAsync<OperationCanceledException>();
            await enumerator.DisposeAsync();

            reader!.DisposeCount.Should().Be(1, "the canceled reader is disposed exactly once");
            command.DisposeCount.Should().Be(0, "a read cancellation is not a poisoned command: it stays reusable");
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "the stream released its lease");
            prepared.PeekCommand(connection).Should().BeSameAs(command, "the Connection policy keeps its command published");

            command.ReaderWrapper = null;
            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), new ProbeConnectionManager(connection));
            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 });
            prepared.CreatedCommandCount.Should().Be(1, "the next execution reuses the same healthy command");
            prepared.PeekCommand(connection).Should().BeSameAs(command);
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
