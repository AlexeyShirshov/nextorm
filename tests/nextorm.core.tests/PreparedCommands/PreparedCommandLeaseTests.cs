using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextORM.Core;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC10 — different connections have independent execution leases. An overlapping execution of the same
/// prepared object on the same connection fails promptly with the documented exception, before the
/// provider command is entered a second time, and succeeds again once the first stream is disposed.
/// Executions on distinct connections proceed concurrently (both readers stay open at the same
/// barrier), which is the behavioural proof that no monitor/lock is held across I/O; a source-level
/// check complements it.
/// </summary>
[Trait("PDCA", "T333")]
public class PreparedCommandLeaseTests
{
    [Theory]
    [InlineData(PreparedCommandCache.Connection)]
    [InlineData(PreparedCommandCache.None)]
    public void SameConnectionOverlap_ThrowsPromptlyAndRecoversAfterTheStreamIsDisposed(PreparedCommandCache cache)
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id", cache, static record => record.GetInt32(0));
            var connectionManager = new ProbeConnectionManager(connection);
            var enumerator = PreparedCommandHarness.CreateEnumerator(
                prepared, connectionManager, cancellationToken: TestContext.Current.CancellationToken);

            enumerator.MoveNext().Should().BeTrue("the first stream opens the reader and holds the lease");
            var live = connection.LastCommand;
            live.ExecuteReaderCallCount.Should().Be(1);
            prepared.GetLeaseOwnerForTest(connection).Should().NotBe(0, "an open stream owns the connection lease");

            var executor = PreparedCommandHarness.CreateExecutor(new ProbeHarnessContext(), connectionManager);
            var act = () => executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*already executing on the same connection*")
                .WithMessage("*different connection*", "the message must name the same-connection restriction and its remedy");
            live.ExecuteReaderCallCount.Should().Be(1,
                "the guard must fire before the provider command is entered a second time");
            prepared.CreatedCommandCount.Should().Be(1, "the rejected overlap must not mint a command");
            prepared.GetLeaseOwnerForTest(connection).Should().NotBe(0, "the first stream still owns its lease");

            enumerator.Dispose();
            prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "disposing the stream releases the lease");

            ExecuteIds(executor, prepared).Should().Equal(new[] { 1, 2 }, "the same connection executes again after the release");
            prepared.CreatedCommandCount.Should().Be(cache == PreparedCommandCache.Connection ? 1 : 2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(PreparedCommandCache.Connection)]
    [InlineData(PreparedCommandCache.None)]
    public async Task DifferentConnections_RunConcurrentlyWithBothReadersOpen(PreparedCommandCache cache)
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var first = PreparedCommandHarness.OpenProbe(path);
            using var second = PreparedCommandHarness.OpenProbe(path);
            var prepared = PreparedCommandHarness.FactoryPrepared(
                "select id from pdca_t333_rows order by id", cache, static record => record.GetInt32(0));
            using var barrier = new Barrier(2);
            var cancellationToken = TestContext.Current.CancellationToken;

            void Stream(ProbeSqliteConnection connection)
            {
                var enumerator = PreparedCommandHarness.CreateEnumerator(
                    prepared, new ProbeConnectionManager(connection), cancellationToken: cancellationToken);
                try
                {
                    enumerator.MoveNext().Should().BeTrue("each connection opens its own reader");
                    barrier.SignalAndWait(TimeSpan.FromSeconds(30)).Should().BeTrue(
                        "both readers must be open at the same time: a lock held across I/O would deadlock here");
                    connection.LastCommand.ExecuteReaderCallCount.Should().Be(1);
                }
                finally
                {
                    enumerator.Dispose();
                }
            }

            var firstStream = Task.Run(() => Stream(first), cancellationToken);
            var secondStream = Task.Run(() => Stream(second), cancellationToken);

            await Task.WhenAll(firstStream, secondStream);

            prepared.CreatedCommandCount.Should().Be(2, "each connection minted its own command");
            prepared.LiveStateCount.Should().Be(2, "each connection has an independent state");
            first.LastCommand.DisposeCount.Should().Be(cache == PreparedCommandCache.None ? 1 : 0);
            second.LastCommand.DisposeCount.Should().Be(cache == PreparedCommandCache.None ? 1 : 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PreparedCommandState_UsesNoMonitorLockOrSemaphore()
    {
        string[] files =
        [
            "src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs",
            "src/nextorm.core/DataContext/Cache/CommandLease.cs",
            "src/nextorm.core/DataContext/Cache/CommandState.cs",
            "src/nextorm.core/DataContext/Cache/PreparedCommandFactory.cs",
        ];

        var root = RepoRoot();
        var forbidden = new List<string>();
        foreach (var relative in files)
        {
            var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(file).Should().BeTrue($"{relative} is part of the prepared-command state machine");

            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken);
            var tree = syntax.GetRoot(TestContext.Current.CancellationToken);

            foreach (var lockStatement in tree.DescendantNodes().OfType<LockStatementSyntax>())
                forbidden.Add($"{relative}: lock statement at {lockStatement.GetLocation().GetLineSpan().StartLinePosition}");

            foreach (var name in tree.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (name.Identifier.Text is "Monitor" or "SemaphoreSlim" or "Semaphore" or "Mutex")
                    forbidden.Add($"{relative}: {name.Identifier.Text} usage");
            }
        }

        forbidden.Should().BeEmpty("the lease is CAS-only; a monitor held across I/O would serialize connections");
    }

    private static List<int> ExecuteIds(QueryExecutor executor, DbPreparedQueryCommand<int> prepared)
    {
        using var owner = executor.OpenResultReader(prepared, ReadOnlySpan<object?>.Empty);
        var ids = new List<int>();
        while (owner.Reader.Read())
            ids.Add(owner.Reader.GetInt32(0));
        return ids;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nextorm.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
