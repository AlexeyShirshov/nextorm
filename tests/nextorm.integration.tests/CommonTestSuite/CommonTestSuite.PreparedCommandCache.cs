using System.Collections.Concurrent;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T333 (#333) D6b — live/provider closure for the prepared-command cache contract. The core unit
/// suite (<c>tests/nextorm.core.tests/PreparedCommands</c>) defines the ownership, identity and
/// lease contract against instrumented in-process commands; this suite asserts the same behaviour
/// against real provider commands: every fact runs once per concrete provider subclass — SQLite,
/// PostgreSQL, SQL Server and MySQL through <see cref="CommonTestSuite"/>, and ClickHouse through its
/// own <see cref="ClickHousePreparedCommandCacheTests"/> subclass, because <c>ClickHouseIntegrationTests</c>
/// derives from <see cref="ProviderTestSuite"/> directly and therefore never inherits the shared
/// partials. Each fact observes real rows, not just "did not throw".
///
/// <para>
/// Every prepared command is created through the public surface (<c>QueryCommand.Prepare</c> with
/// <see cref="PrepareOptions"/>) from a throwaway provider context and then executed by other
/// contexts, which is exactly the cross-context reuse the prepared object promises. The internal
/// diagnostics (<c>PeekCommand</c>, <c>CreatedCommandCount</c>, <c>GetLeaseOwnerForTest</c>,
/// <c>PendingBatch</c>) are visible because <c>nextorm.core.csproj</c> already grants
/// <c>InternalsVisibleTo("nextorm.integration.tests")</c>; no project change was needed.
/// </para>
///
/// <para>
/// Because one prepared instance is <b>static</b> and shared by every fact of this class, every
/// counter assertion is a delta measured inside the fact, and the per-connection identity is asserted
/// on the connection object created by that fact. MariaDB has no <c>MariaDbTestProvider</c> and is
/// therefore outside this suite by construction: MySQL evidence is never presented as MariaDB
/// evidence.
/// </para>
/// </summary>
public abstract class PreparedCommandCacheSuite : ProviderTestSuite
{
    /// <summary>One prepared command per (provider, policy, streaming) triple, prepared once per run.</summary>
    private static readonly ConcurrentDictionary<string, IPreparedQueryCommand<int>> LivePreparedCommands = new();

    private const string SameConnectionRejection = "*already executing on the same connection*";

    // -------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------

    /// <summary>The parameterised query every live fact executes: one seeded row per parameter value.</summary>
    private static QueryCommand<int> BuildByIdQuery(IDataContext ctx)
        => ctx.From<ISimpleEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .Select(x => x.Id);

    /// <summary>
    /// Returns the process-wide prepared command for this provider and form, preparing it exactly once
    /// from a throwaway context (preparation needs a dialect, never a connection).
    /// </summary>
    private IPreparedQueryCommand<int> LivePrepared(PrepareOptions options)
        => LivePreparedCommands.GetOrAdd(
            $"{Provider.Name}|{(int)options.Cache}|{options.Streaming}",
            _ => PrepareLive(options));

    private IPreparedQueryCommand<int> PrepareLive(PrepareOptions options)
    {
        Provider.EnsureSeeded();
        using var preparingContext = Provider.CreateContext();
        return BuildByIdQuery(preparingContext).Prepare(options, TestContext.Current.CancellationToken);
    }

    private static List<int> ExecuteIds(IDataContext ctx, DbPreparedQueryCommand<int> prepared, int value)
        => ctx.ToList(prepared, new object?[] { value });

    private static DbConnection ConnectionOf(IDataContext ctx) => ((DataContext)ctx).GetConnection();

    /// <summary>The SQL the planner renders for the same query without an explicit <c>Prepare</c>.</summary>
    private static string OrdinarySql(IDataContext ctx, bool streaming)
    {
        var prepared = (DbPreparedQueryCommand<int>)((DataContext)ctx).GetPreparedQueryCommand(
            BuildByIdQuery(ctx), streaming, storeInCache: false, CancellationToken.None);
        return prepared.SqlStmt ?? string.Empty;
    }

    private static async Task<List<int>> StreamIds(
        IDataContext ctx, IPreparedQueryCommand<int> prepared, int value, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        await using var enumerator = ctx.CreateAsyncEnumerator(prepared, new object[] { value }, cancellationToken);
        while (await enumerator.MoveNextAsync())
            ids.Add(enumerator.Current);
        return ids;
    }

    // -------------------------------------------------------------------------------------------
    // AC01 — one static prepared command, two contexts, two live connections, distinct parameters
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// AC01: the shared prepared command executes concurrently on two distinct live connections with a
    /// distinct parameter value per side, barrier-coordinated over repeated rounds. Each side must see
    /// only its own row: a shared parameter collection or a substituted reader would surface the other
    /// side's value.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Theory]
    [InlineData(PreparedCommandCache.Connection)]
    [InlineData(PreparedCommandCache.None)]
    public async Task PreparedCommand_TwoLiveConnections_InterleavedRounds_KeepParametersAndReadersIsolated(
        PreparedCommandCache cache)
    {
        var prepared = (DbPreparedQueryCommand<int>)LivePrepared(new PrepareOptions { Cache = cache });
        using var firstContext = Provider.CreateContext();
        using var secondContext = Provider.CreateContext();
        using var gate = new Barrier(2);
        var cancellationToken = TestContext.Current.CancellationToken;

        void Run(IDataContext ctx, int value)
        {
            for (var round = 0; round < 8; round++)
            {
                gate.SignalAndWait(TimeSpan.FromSeconds(30)).Should().BeTrue(
                    "both connections must reach the execution boundary together to expose interleaving");
                ExecuteIds(ctx, prepared, value).Should().Equal(new[] { value },
                    "each connection binds its own parameter value and reads its own reader");
            }
        }

        await Task.WhenAll(
            Task.Run(() => Run(firstContext, 1), cancellationToken),
            Task.Run(() => Run(secondContext, 2), cancellationToken));

        // No parameter bleed survives the interleaving: a later single-threaded read on each side still
        // returns that side's own row.
        ExecuteIds(firstContext, prepared, 3).Should().Equal(3);
        ExecuteIds(secondContext, prepared, 4).Should().Equal(4);

        prepared.CachePolicy.Should().Be(cache);
        prepared.GetLeaseOwnerForTest(ConnectionOf(firstContext)).Should().Be(0,
            "every interleaved execution released its lease");
        prepared.GetLeaseOwnerForTest(ConnectionOf(secondContext)).Should().Be(0);
    }

    // -------------------------------------------------------------------------------------------
    // AC02 — same-connection identity, distinct connection objects with equal connection strings
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// AC02: successive completed executions on one connection run the identical underlying command,
    /// while a second connection object — with an equal connection string — gets its own command and
    /// its own correct results. This is the negative control for a string-keyed cache.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Fact]
    public void PreparedCommand_ConnectionPolicy_ReusesOneCommandPerConnectionObject_DespiteEqualStrings()
    {
        var prepared = (DbPreparedQueryCommand<int>)LivePrepared(new PrepareOptions());
        using var firstContext = Provider.CreateContext();
        using var secondContext = Provider.CreateContext();
        var firstConnection = ConnectionOf(firstContext);
        var secondConnection = ConnectionOf(secondContext);

        firstConnection.ConnectionString.Should().Be(secondConnection.ConnectionString,
            "both contexts address the same database, so the connection strings are equal");
        firstConnection.Should().NotBeSameAs(secondConnection,
            "the per-connection state is keyed on the connection object, not on its string");

        var createdBefore = prepared.CreatedCommandCount;

        DbCommand? published = null;
        for (var i = 1; i <= 3; i++)
        {
            ExecuteIds(firstContext, prepared, i).Should().Equal(new[] { i }, "each execution binds its own parameter value");
            prepared.CreatedCommandCount.Should().Be(createdBefore + 1,
                "three executions on one connection mint one command");
            var current = prepared.PeekCommand(firstConnection);
            current.Should().NotBeNull("the Connection policy keeps its command published for the connection");
            if (published is null)
                published = current;
            else
                current.Should().BeSameAs(published, "successive executions must run the identical command");
        }

        prepared.GetLeaseOwnerForTest(firstConnection).Should().Be(0, "a completed execution releases its lease");

        ExecuteIds(secondContext, prepared, 7).Should().Equal(7);
        prepared.CreatedCommandCount.Should().Be(createdBefore + 2,
            "the distinct connection object mints its own command");
        prepared.PeekCommand(secondConnection).Should().NotBeNull()
            .And.NotBeSameAs(published,
                "equal connection strings must never collapse two connection objects into one state");
        prepared.PeekCommand(firstConnection).Should().BeSameAs(published,
            "the first connection keeps exactly its own command");
        prepared.GetLeaseOwnerForTest(secondConnection).Should().Be(0);
    }

    // -------------------------------------------------------------------------------------------
    // AC03 — None: a fresh command per execution, disposed exactly once (buffered and streaming)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// AC03: under <see cref="PreparedCommandCache.None"/> every execution mints a fresh provider
    /// command and disposes it exactly once — buffered, after a complete streaming enumeration, and
    /// after an early stream disposal. The retry after the abandoned stream must still return the right
    /// rows, which is the observable proof that the abandoned execution released both its command and
    /// its connection lease.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Fact]
    public async Task PreparedCommand_NonePolicy_BufferedAndStreaming_MintAndDisposeOneCommandPerExecution()
    {
        var buffered = (DbPreparedQueryCommand<int>)LivePrepared(new PrepareOptions { Cache = PreparedCommandCache.None });
        using var ctx = Provider.CreateContext();
        var connection = ConnectionOf(ctx);
        var createdBefore = buffered.CreatedCommandCount;
        var disposedBefore = buffered.DisposedCommandCount;

        // Buffered: three executions, three commands, three disposals, nothing published.
        for (var i = 1; i <= 3; i++)
        {
            ExecuteIds(ctx, buffered, i).Should().Equal(i);
            buffered.CreatedCommandCount.Should().Be(createdBefore + i, "every execution mints a fresh command");
            buffered.DisposedCommandCount.Should().Be(disposedBefore + i,
                "the finished execution disposes exactly its own command");
            buffered.PeekCommand(connection).Should().BeNull("the None policy never publishes a command");
            buffered.GetLeaseOwnerForTest(connection).Should().Be(0);
        }

        // Streaming: the command stays owned by the live stream and is disposed exactly once at the end.
        var streaming = (DbPreparedQueryCommand<int>)LivePrepared(
            new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true });
        var streamCreatedBefore = streaming.CreatedCommandCount;
        var streamDisposedBefore = streaming.DisposedCommandCount;

        var enumerator = ctx.CreateAsyncEnumerator(streaming, new object[] { 5 }, TestContext.Current.CancellationToken);
        try
        {
            var ids = new List<int>();
            while (await enumerator.MoveNextAsync())
                ids.Add(enumerator.Current);

            ids.Should().Equal(5);
            streaming.CreatedCommandCount.Should().Be(streamCreatedBefore + 1);
            streaming.DisposedCommandCount.Should().Be(streamDisposedBefore,
                "the live stream still owns its command while it is readable");
            streaming.GetLeaseOwnerForTest(connection).Should().NotBe(0);
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        streaming.DisposedCommandCount.Should().Be(streamDisposedBefore + 1,
            "the finished stream disposes its command exactly once");
        streaming.PeekCommand(connection).Should().BeNull("no command is retained under None");
        streaming.GetLeaseOwnerForTest(connection).Should().Be(0, "the stream released its lease");

        // Early disposal mid-stream: the abandoned command is disposed once and the next execution works.
        var earlyCreatedBefore = streaming.CreatedCommandCount;
        var earlyDisposedBefore = streaming.DisposedCommandCount;
        var early = ctx.CreateAsyncEnumerator(streaming, new object[] { 6 }, TestContext.Current.CancellationToken);
        (await early.MoveNextAsync()).Should().BeTrue("the first row opens the reader");
        early.Current.Should().Be(6);
        streaming.GetLeaseOwnerForTest(connection).Should().NotBe(0, "an open stream owns the connection lease");
        await early.DisposeAsync();
        streaming.DisposedCommandCount.Should().Be(earlyDisposedBefore + 1,
            "an abandoned stream disposes its command exactly once");
        streaming.GetLeaseOwnerForTest(connection).Should().Be(0, "abandoning the stream releases the lease");

        ExecuteIds(ctx, streaming, 8).Should().Equal(new[] { 8 }, "a released lease lets the same prepared command execute again");
        streaming.CreatedCommandCount.Should().Be(earlyCreatedBefore + 2,
            "the abandoned stream and the retry each mint their own fresh command");
        streaming.DisposedCommandCount.Should().Be(earlyDisposedBefore + 2);
    }

    // -------------------------------------------------------------------------------------------
    // AC10 — same-connection fail-fast and recovery; different connections are not serialized
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// AC10 (a): with a stream open on the connection, a second execution of the same prepared object on
    /// that connection fails promptly with the documented exception and does not mint a command; once
    /// the stream is disposed the same connection executes successfully again.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Theory]
    [InlineData(PreparedCommandCache.Connection)]
    [InlineData(PreparedCommandCache.None)]
    public async Task PreparedCommand_SameConnectionOverlap_FailsFastAndRecoversAfterStreamDisposal(
        PreparedCommandCache cache)
    {
        var prepared = (DbPreparedQueryCommand<int>)LivePrepared(
            new PrepareOptions { Cache = cache, Streaming = true });
        using var ctx = Provider.CreateContext();
        var connection = ConnectionOf(ctx);
        var createdBefore = prepared.CreatedCommandCount;

        var enumerator = ctx.CreateAsyncEnumerator(prepared, new object[] { 1 }, TestContext.Current.CancellationToken);
        try
        {
            (await enumerator.MoveNextAsync()).Should().BeTrue("the first stream opens the reader and holds the lease");
            enumerator.Current.Should().Be(1);
            prepared.GetLeaseOwnerForTest(connection).Should().NotBe(0, "an open stream owns the connection lease");
            prepared.CreatedCommandCount.Should().Be(createdBefore + 1);

            var act = () => ctx.ToList(prepared, new object?[] { 2 });
            act.Should().Throw<InvalidOperationException>()
                .WithMessage(SameConnectionRejection)
                .WithMessage("*different connection*",
                    "the message must name the same-connection restriction and its remedy");
            prepared.CreatedCommandCount.Should().Be(createdBefore + 1,
                "the rejected overlap must not mint a command");
            prepared.GetLeaseOwnerForTest(connection).Should().NotBe(0, "the first stream still owns its lease");
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        prepared.GetLeaseOwnerForTest(connection).Should().Be(0, "disposing the stream releases the lease");
        ExecuteIds(ctx, prepared, 3).Should().Equal(new[] { 3 }, "the same connection executes again after the release");
    }

    /// <summary>
    /// AC10 (b): two threads on two distinct connections stream the same prepared object concurrently.
    /// Both readers stay open at the barrier and both materialise their whole result — a lock held
    /// across I/O would either deadlock at the barrier or make one side observe the other's reader.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Theory]
    [InlineData(PreparedCommandCache.Connection)]
    [InlineData(PreparedCommandCache.None)]
    public async Task PreparedCommand_DifferentConnections_StreamConcurrentlyWithBothReadersOpen(
        PreparedCommandCache cache)
    {
        var prepared = (DbPreparedQueryCommand<int>)LivePrepared(
            new PrepareOptions { Cache = cache, Streaming = true });
        using var firstContext = Provider.CreateContext();
        using var secondContext = Provider.CreateContext();
        using var gate = new Barrier(2);
        var cancellationToken = TestContext.Current.CancellationToken;

        async Task StreamAsync(IDataContext ctx, int value)
        {
            await using var enumerator = ctx.CreateAsyncEnumerator(prepared, new object[] { value }, cancellationToken);
            (await enumerator.MoveNextAsync()).Should().BeTrue("each connection opens its own reader");
            enumerator.Current.Should().Be(value);

            gate.SignalAndWait(TimeSpan.FromSeconds(30)).Should().BeTrue(
                "both readers must be open at the same time: a lock held across I/O would deadlock here");

            var rest = new List<int>();
            while (await enumerator.MoveNextAsync())
                rest.Add(enumerator.Current);
            rest.Should().BeEmpty("the query projects exactly one row per parameter value");
        }

        await Task.WhenAll(
            Task.Run(() => StreamAsync(firstContext, 1), cancellationToken),
            Task.Run(() => StreamAsync(secondContext, 2), cancellationToken));

        prepared.GetLeaseOwnerForTest(ConnectionOf(firstContext)).Should().Be(0);
        prepared.GetLeaseOwnerForTest(ConnectionOf(secondContext)).Should().Be(0);
        ExecuteIds(firstContext, prepared, 9).Should().Equal(new[] { 9 }, "the first connection is still usable");
        ExecuteIds(secondContext, prepared, 10).Should().Equal(new[] { 10 }, "the second connection is still usable");
    }

    // -------------------------------------------------------------------------------------------
    // AC12 — provider × policy × buffered/streaming matrix, and the pending-batch path
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// AC12: every supported provider × policy × buffered/streaming cell preserves the SQL, the
    /// parameter binding and the results of the same query executed without an explicit preparation.
    /// The oracle is the planner's own ordinary preparation of an identical query, which is the same
    /// path an unprepared terminal takes.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Theory]
    [InlineData(PreparedCommandCache.Connection, false)]
    [InlineData(PreparedCommandCache.Connection, true)]
    [InlineData(PreparedCommandCache.None, false)]
    [InlineData(PreparedCommandCache.None, true)]
    public async Task PreparedCommand_ProviderFormMatrix_MatchesOrdinaryExecution(
        PreparedCommandCache cache, bool streaming)
    {
        var prepared = (DbPreparedQueryCommand<int>)LivePrepared(
            new PrepareOptions { Cache = cache, Streaming = streaming });
        using var ctx = Provider.CreateContext();

        prepared.SqlStmt.Should().NotBeNullOrWhiteSpace();
        prepared.SqlStmt.Should().Be(OrdinarySql(ctx, streaming),
            "preparation must not change the SQL the planner renders for the same query");
        prepared.CachePolicy.Should().Be(cache);
        prepared.StreamingEnabled.Should().Be(streaming,
            "the Streaming option is the only thing that promotes the row enumerator");

        // Parameters and results, against the same query executed without an explicit preparation.
        ExecuteIds(ctx, prepared, 3).Should().Equal(3);
        ExecuteIds(ctx, prepared, 4).Should().Equal(4);
        ctx.From<ISimpleEntity>().Where(x => x.Id == 3).Select(x => x.Id).ToList().Should().Equal(new[] { 3 },
            "the prepared parameterised read matches the unprepared literal read");

        if (streaming)
        {
            var streamed = await StreamIds(ctx, prepared, 5, TestContext.Current.CancellationToken);
            streamed.Should().Equal(new[] { 5 }, "the streaming form returns the same rows as the buffered form");
        }
    }

    /// <summary>
    /// AC12 (pending batch): a query reading a lazy temporary-table source carries an immutable
    /// <c>PendingBatch</c> that executes DROP + CREATE TEMPORARY TABLE + read. A prepared command of
    /// that shape must run through the batch path and return the materialised rows. Where the provider
    /// cannot materialise a temporary table, the existing fail-closed rejection must be preserved —
    /// nothing is enabled here, and the rejection is the recorded evidence for that cell.
    /// </summary>
    [Trait("PDCA", "T333")]
    [Fact]
    public void PreparedCommand_PendingBatch_TempTableSource_ExecutesOrKeepsTheExistingRejection()
    {
        using var ctx = Provider.CreateContext();

        if (!Provider.SupportsTemporaryCreateTableAsSelect)
        {
            var act = () =>
            {
                var rejected = BuildTempTableQuery(ctx).Prepare(new PrepareOptions(), TestContext.Current.CancellationToken);
                ctx.ToList(rejected);
            };

            act.Should().Throw<NotSupportedException>(
                "this provider cannot materialise a query into a temporary table, and preparation must not enable it");
            return;
        }

        var prepared = (DbPreparedQueryCommand<int>)BuildTempTableQuery(ctx).Prepare(new PrepareOptions(), TestContext.Current.CancellationToken);
        prepared.PendingBatch.Should().NotBeNull("a lazy temporary-table source is executed through the batch plan");

        var ids = ctx.ToList(prepared).OrderBy(id => id).ToList();
        ids.Should().Equal(new[] { 1, 2, 3 }, "the pending batch materialises the source and reads it back");
    }

    private static QueryCommand<int> BuildTempTableQuery(IDataContext ctx)
    {
        var source = ctx.From<ISimpleEntity>()
            .Where(x => x.Id <= 3)
            .Select(x => new { x.Id })
            .AsTempTable();
        return ctx.From(source).Select(t => t.GetInt32("id"));
    }
}

/// <summary>
/// T333 live facts for the shared prepared-command cache contract. A fact declared in
/// <see cref="PreparedCommandCacheSuite"/> runs once per concrete provider subclass; the four providers
/// that use the shared suite get it through <see cref="CommonTestSuite"/>, and ClickHouse gets it here
/// because its own integration class derives from <see cref="ProviderTestSuite"/> directly.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHousePreparedCommandCacheTests : PreparedCommandCacheSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;
}
