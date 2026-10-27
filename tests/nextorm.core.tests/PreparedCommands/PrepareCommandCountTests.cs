using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.PreparedCommands;

/// <summary>
/// AC05b-1 of plan r=4 — deterministic counting of prepared-command mints over <b>K = 5</b> executions,
/// with no timing, allocation or log-text observation. The count is read from the existing
/// per-(plan, connection) mint seam <c>DbPreparedQueryCommand&lt;TResult&gt;.CreatedCommandCount</c>
/// (<c>src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:108</c>), incremented inside
/// <c>Acquire</c> right after the one command-creation call site
/// <c>PreparedCommandFactory.Create</c> (<c>:232</c> / <c>:241</c> call, <c>:249</c> increment). The kept
/// numbers are exact integers, so an off-by-one in any of them fails the case; a <c>None</c> policy that
/// mints fewer than <c>K</c> commands cannot satisfy the exact-<c>K</c> assertion either.
/// <para>
/// The owner's "Default" policy has no separate enum member in this project: it is
/// <c>default(PrepareOptions)</c>, i.e. buffered execution with
/// <see cref="PreparedCommandCache.Connection"/>. Every case drives the real production path — a real
/// <see cref="SqliteDataContext"/> over a file-backed SQLite database, the real
/// <c>PrepareOptions</c>/<c>QueryCommand.Prepare</c> entry point and the real <c>DataContext.ToList</c>
/// terminal over the instrumented connection from <see cref="PreparedCommandHarness"/>.
/// </para>
/// </summary>
[Trait("PDCA", "T333")]
public class PrepareCommandCountTests
{
    /// <summary>The number of executions every scenario performs (K = 5).</summary>
    private const int K = 5;

    [Fact]
    [Trait("PDCA", "T333")]
    public void DefaultPolicy_FiveExecutionsOnOneConnection_MintExactlyOneCommand()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var prepared = PrepareDefault(ctx);

            prepared.CachePolicy.Should().Be(PreparedCommandCache.Connection,
                "default(PrepareOptions) is buffered per-connection reuse");
            prepared.CreatedCommandCount.Should().Be(0, "preparation alone mints no ADO.NET command");

            for (var i = 1; i <= K; i++)
            {
                Execute(ctx, prepared, $"execution {i} of K = {K} must return the seeded rows");
                prepared.CreatedCommandCount.Should().Be(1,
                    "the default policy mints exactly one command per (plan, connection), never one per execution");
                connection.CreatedCommandCount.Should().Be(1,
                    "no further ADO.NET command reaches the connection across the K = 5 executions");
            }

            prepared.DisposedCommandCount.Should().Be(0,
                "the per-connection state owns the command, so no execution disposes it");
            prepared.PeekCommand(connection).Should().BeSameAs(connection.LastCommand,
                "every execution reuses the single published command");
            prepared.LiveStateCount.Should().Be(1, "one connection, one per-connection state");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("PDCA", "T333")]
    public void NonePolicy_FiveExecutionsOnTheSameConnection_MintExactlyFiveCommands()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());

            // A separate prepared object for the None policy keeps its counter unambiguous, and the
            // same query shape is planned twice so both objects share the plan but not their state.
            var reuse = PrepareDefault(ctx);
            Execute(ctx, reuse, "the default-policy prepared command must return the seeded rows");
            reuse.CreatedCommandCount.Should().Be(1);
            var reuseBinding = reuse.PeekCommand(connection);
            reuseBinding.Should().NotBeNull();

            var noneCommand = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
            var none = (DbPreparedQueryCommand<int>)noneCommand.Prepare(
                new PrepareOptions { Cache = PreparedCommandCache.None }, TestContext.Current.CancellationToken);
            none.CachePolicy.Should().Be(PreparedCommandCache.None);
            none.Should().NotBeSameAs(reuse, "the two policies are carried by two distinct prepared objects");
            none.CreatedCommandCount.Should().Be(0);
            none.LiveStateCount.Should().Be(0, "preparation creates no per-connection state");

            var mintedBefore = connection.CreatedCommandCount;
            var freshCommands = new List<ProbeSqliteCommand>();
            for (var i = 1; i <= K; i++)
            {
                Execute(ctx, none, $"None execution {i} of K = {K} must return the seeded rows");
                none.CreatedCommandCount.Should().Be(i,
                    "None mints a fresh command on every execution, so after K = 5 executions the count is exactly 5");
                none.DisposedCommandCount.Should().Be(i,
                    "the execution-owned command is disposed exactly once at the end of each execution");
                none.PeekCommand(connection).Should().BeNull("None never publishes a command for reuse");
                none.GetLeaseOwnerForTest(connection).Should().Be(0, "each execution released its lease");
                freshCommands.Add(connection.LastCommand);
            }

            freshCommands.Should().HaveCount(K);
            freshCommands.Should().OnlyHaveUniqueItems("every None execution must use a distinct command");
            freshCommands.Should().OnlyContain(command => command.DisposeCount == 1,
                "each execution-owned command is disposed exactly once");
            connection.CreatedCommandCount.Should().Be(mintedBefore + K,
                "the K = 5 executions minted exactly K more ADO.NET commands");

            none.LiveStateCount.Should().Be(1,
                "only the per-connection state entry exists; it never holds a command under None");
            reuse.CreatedCommandCount.Should().Be(1,
                "the Connection-policy prepared object shares no counter or state with the None one");
            reuse.PeekCommand(connection).Should().BeSameAs(reuseBinding,
                "the None executions never touch the other prepared object's binding");
            reuse.LiveStateCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("PDCA", "T333")]
    public void DefaultPolicy_SecondConnection_MintsExactlyOneMoreCommand()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var first = PreparedCommandHarness.OpenProbe(path);
            using var firstContext = new SqliteDataContext(first, new DataContextBuilder());
            var prepared = PrepareDefault(firstContext);

            Execute(firstContext, prepared, "the first connection must return the seeded rows");
            prepared.CreatedCommandCount.Should().Be(1);
            var firstBinding = prepared.PeekCommand(first);
            firstBinding.Should().NotBeNull();

            using var second = PreparedCommandHarness.OpenProbe(path);
            using var secondContext = new SqliteDataContext(second, new DataContextBuilder());

            for (var i = 1; i <= K; i++)
            {
                Execute(secondContext, prepared, $"second-connection execution {i} of K = {K} must return the seeded rows");
                prepared.CreatedCommandCount.Should().Be(2,
                    "a second connection adds exactly one mint for the same plan, regardless of the K = 5 executions");
                second.CreatedCommandCount.Should().Be(1, "the second connection owns exactly one command");
                first.CreatedCommandCount.Should().Be(1, "the first connection's count is unchanged");
            }

            prepared.PeekCommand(second).Should().NotBeNull().And.NotBeSameAs(firstBinding,
                "each connection keeps its own published command");
            prepared.PeekCommand(first).Should().BeSameAs(firstBinding);
            prepared.LiveStateCount.Should().Be(2, "one per-connection state per connection");
            prepared.DisposedCommandCount.Should().Be(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("PDCA", "T333")]
    public void ClosedAndReopenedConnection_NeverReusesTheStaleBinding_AndMintsExactlyOneMoreCommand()
    {
        var path = PreparedCommandHarness.CreateSeededFile();
        try
        {
            using var connection = PreparedCommandHarness.OpenProbe(path);
            using var ctx = new SqliteDataContext(connection, new DataContextBuilder());
            var prepared = PrepareDefault(ctx);

            Execute(ctx, prepared, "the open connection must return the seeded rows");
            prepared.CreatedCommandCount.Should().Be(1);
            var stale = prepared.PeekCommand(connection);
            stale.Should().NotBeNull();

            // The connection goes away and comes back, and the product's own reset entry (the very call
            // DbConnectionManager.ConnDisposed/DisposeConnection makes for a cached plan) drops the
            // binding. This is the cheap "stale binding" case: it needs no new mechanism.
            connection.Close();
            connection.Open();
            ((IDbCommandHolder)prepared).ResetConnection(connection, ctx);

            prepared.PeekCommand(connection).Should().BeNull("the product drops the closed connection's binding");
            prepared.LiveStateCount.Should().Be(0, "the reset forgets the per-connection state");
            prepared.CreatedCommandCount.Should().Be(1, "resetting the binding mints no command");
            prepared.DisposedCommandCount.Should().Be(0,
                "the reset forgets the binding instead of disposing it: the provider owns the closed connection's commands");
            ((ProbeSqliteCommand)stale!).DisposeCount.Should().Be(1,
                "the provider disposes the commands attached to a closed connection exactly once");

            Execute(ctx, prepared, "the reopened connection must execute the same plan again");
            prepared.CreatedCommandCount.Should().Be(2,
                "exactly one additional mint: the stale binding is never reused for the reopened connection");
            var rebound = prepared.PeekCommand(connection);
            rebound.Should().NotBeNull().And.NotBeSameAs(stale);
            connection.CreatedCommandCount.Should().Be(2, "the reopened connection minted exactly one new command");
            prepared.LiveStateCount.Should().Be(1, "the reopened connection has a fresh state");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DbPreparedQueryCommand<int> PrepareDefault(SqliteDataContext ctx)
    {
        var command = ctx.From<PreparedCommandHarness.PreparedProbeRow>().OrderBy(x => x.Id).Select(x => x.Id);
        return (DbPreparedQueryCommand<int>)command.Prepare(default(PrepareOptions), TestContext.Current.CancellationToken);
    }

    private static void Execute(SqliteDataContext ctx, IPreparedQueryCommand<int> prepared, string because)
        => ctx.ToList(prepared, ReadOnlySpan<object?>.Empty).Should().Equal(new[] { 1, 2 }, because);
}
