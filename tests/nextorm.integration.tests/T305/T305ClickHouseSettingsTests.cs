using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.ClickHouse.Bulk;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T305 (#305) D6: ClickHouse rows 21-23 (parallelism, session, progress/ownership) plus the A15 bounded
/// adapter measurement, all against the live server. The adapter counters are the measurement surface:
/// <see cref="ClickHouseBulkInsertAdapter.RowsConsumed"/>, <see cref="ClickHouseBulkInsertAdapter.CompletedDriverCalls"/>
/// and <see cref="ClickHouseBulkInsertAdapter.RowsWritten"/>; every driver call below is a real
/// <c>InsertBinaryAsync</c> against the container, and the rows are read back.
/// </summary>
[Collection("ClickHouse")]
public sealed class T305ClickHouseSettingsTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static string SessionConnectionString(string sessionId) =>
        ClickHouseContainer.ConnectionString + $";UseSession=true;SessionId={sessionId}";

    [Fact]
    public void UseSessionTrue_WithoutAnActualSessionId_ShouldBeRefusedBeforeTheSource()
    {
        var source = T305Sql.Counting(T305Sql.InsertRows(T305Sql.Marker("ch_session_missing"), 2));

        var act = () => Context.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .ClickHouse(new ClickHouseBulkInsertOptions { UseSession = true }))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*UseSession=true*session id*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "a flag without an id is never silently downgraded to sessionless: the request is refused before the "
            + "write, after the synchronous terminal buffered the source for validation");
    }

    [Fact]
    public void UseSessionNull_ShouldPreserveTheConfiguredSessionAndSeeItsTemporaryTable()
    {
        var sessionId = "t305-session-" + Guid.NewGuid().ToString("N");
        using var ctx = new ClickHouseDataContext(SessionConnectionString(sessionId), new DataContextBuilder());
        Execute(ctx, "create temporary table t305_sess_rows (id Int32, name Nullable(String)) engine = Memory");

        var written = ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("t305_sess_rows"))
            .Values(T305Sql.DestRows(1, 3))
            .BulkInsert();

        written.Should().Be(3, "null preserves the configured session, and the temporary table lives in it");
        ctx.From("t305_sess_rows").Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void UseSessionFalse_ShouldRequestASessionlessInsertAndRefuseATemporaryTarget()
    {
        var sessionId = "t305-sessionless-" + Guid.NewGuid().ToString("N");
        using var ctx = new ClickHouseDataContext(SessionConnectionString(sessionId), new DataContextBuilder());
        T305Sql.Recreate(ctx, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");

        ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .ClickHouse(new ClickHouseBulkInsertOptions { UseSession = false }))
            .Values(T305Sql.DestRows(11, 2))
            .BulkInsert()
            .Should().Be(2, "false is an explicit sessionless non-temporary insert");

        var source = T305Sql.Counting(T305Sql.DestRows(21, 1));
        var temporary = () => ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .ClickHouse(new ClickHouseBulkInsertOptions { UseSession = false }))
            .Values(source)
            .BulkInsert();
        temporary.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*");
        source.Enumerated.Should().Be(0, "a sessionless insert cannot see a session-local temporary table");
    }

    [Fact]
    public void Parallelism_NullOneAndAboveOne_ShouldWriteSessionless()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");

        var unset = Context.CreateBulkInsertBuilder<IT305BulkDest>()
            .Values(T305Sql.DestRows(1, 2)).BulkInsert();
        var one = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 1 }))
            .Values(T305Sql.DestRows(11, 2)).BulkInsert();
        var parallel = Context.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 3 }))
            .Values(T305Sql.DestRows(21, 2)).BulkInsert();

        unset.Should().Be(2);
        one.Should().Be(2);
        parallel.Should().Be(2);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(6);
    }

    [Fact]
    public void ParallelismAboveOne_WithASession_ShouldBeRefusedBeforeTheSource()
    {
        var sessionId = "t305-parallel-" + Guid.NewGuid().ToString("N");
        using var ctx = new ClickHouseDataContext(SessionConnectionString(sessionId), new DataContextBuilder());
        var source = T305Sql.Counting(T305Sql.DestRows(1, 2));

        var act = () => ctx.CreateBulkInsertBuilder<IT305BulkDest>(o => o
                .ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 2 }))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*MaxDegreeOfParallelism=2*session*");
        source.Enumerated.Should().Be(
            source.RowCount,
            "parallelism above one cannot run inside a session: the request is refused before the write, after the "
            + "synchronous terminal buffered the source for validation");
    }

    [Fact]
    public async Task CallerOwnedClient_ShouldNotBeDisposedByTheContext()
    {
        using var client = new ClickHouseClient(new ClickHouseClientSettings(ClickHouseContainer.ConnectionString));
        var ctx = new ClickHouseDataContext(client, new DataContextBuilder());
        T305Sql.Recreate(ctx, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");

        ctx.CreateBulkInsertBuilder<IT305BulkDest>().Values(T305Sql.DestRows(1, 2)).BulkInsert().Should().Be(2);

        ctx.Dispose();
        ctx.OwnedClient.Should().BeNull();
        ctx.OwnedClientDisposalCount.Should().Be(0);

        // The caller still owns a usable client: the write after the context is gone proves it.
        var written = await client.InsertBinaryAsync(
            "t305_bulk_dest",
            ["id", "name"],
            [new object?[] { 11, "after-dispose" }],
            new InsertOptions { UseSchemaCache = true },
            TestContext.Current.CancellationToken);
        written.Should().Be(1);
    }

    // ---------------------------------------------------------------- A15 bounded adapter measurement

    [Fact]
    public async Task Adapter_ShouldNeverPullAheadOfACompletedDriverCall()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");
        using var client = new ClickHouseClient(new ClickHouseClientSettings(ClickHouseContainer.ConnectionString));

        var pulled = 0;
        var pulledAtCall = new List<int>();
        var adapter = new ClickHouseBulkInsertAdapter(
            async (chunk, cancellationToken) =>
            {
                pulledAtCall.Add(pulled);
                var written = await client.InsertBinaryAsync(
                    "t305_bulk_dest",
                    ["id", "name"],
                    chunk,
                    new InsertOptions { UseSchemaCache = true },
                    cancellationToken).ConfigureAwait(false);
                return (int)written;
            },
            chunkSize: 4);

        IEnumerable<object?[]> Source()
        {
            for (var i = 0; i < 10; i++)
            {
                pulled++;
                yield return [i, "row"];
            }
        }

        using var deadline = BulkInsertDeadline.Start(null, CancellationToken.None);
        var written = adapter.Write(Source(), progress: null, notifyEvery: 1, deadline);

        written.Should().Be(10);
        pulledAtCall.Should().Equal(new[] { 4, 8, 10 }, "the source is never pulled ahead of the awaited driver call");
        adapter.RowsConsumed.Should().Be(10);
        adapter.CompletedDriverCalls.Should().Be(3);
        adapter.RowsWritten.Should().Be(10);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(10);
    }

    [Fact]
    public async Task AdapterAsync_ShouldBoundRetentionAndCountOnlyCompletedCalls()
    {
        T305Sql.Recreate(Context, "t305_bulk_dest", "create table t305_bulk_dest (id Int32, name Nullable(String)) engine = MergeTree order by id");
        using var client = new ClickHouseClient(new ClickHouseClientSettings(ClickHouseContainer.ConnectionString));

        var pulled = 0;
        var pulledAtCall = new List<int>();
        var adapter = new ClickHouseBulkInsertAdapter(
            async (chunk, cancellationToken) =>
            {
                pulledAtCall.Add(pulled);
                var written = await client.InsertBinaryAsync(
                    "t305_bulk_dest",
                    ["id", "name"],
                    chunk,
                    new InsertOptions { UseSchemaCache = true },
                    cancellationToken).ConfigureAwait(false);
                return (int)written;
            },
            chunkSize: 3);

        async IAsyncEnumerable<object?[]> Source()
        {
            for (var i = 0; i < 7; i++)
            {
                pulled++;
                await Task.Yield();
                yield return [i, "row"];
            }
        }

        using var deadline = BulkInsertDeadline.Start(null, TestContext.Current.CancellationToken);
        var written = await adapter.WriteAsync(Source(), progress: null, notifyEvery: 1, deadline);

        written.Should().Be(7);
        pulledAtCall.Should().Equal(3, 6, 7);
        adapter.RowsConsumed.Should().Be(7);
        adapter.CompletedDriverCalls.Should().Be(3);
        adapter.RowsWritten.Should().Be(7);
        Context.From<IT305BulkDest>().Select(x => new { x.Id }).ToList().Should().HaveCount(7);
    }

    [Fact]
    public async Task Adapter_WhenTheDriverCallFails_ShouldNotCountItAsCompleted()
    {
        using var client = new ClickHouseClient(new ClickHouseClientSettings(ClickHouseContainer.ConnectionString));
        var adapter = new ClickHouseBulkInsertAdapter(
            async (chunk, cancellationToken) =>
            {
                var written = await client.InsertBinaryAsync(
                    "t305_missing_table",
                    ["id", "name"],
                    chunk,
                    new InsertOptions { UseSchemaCache = true },
                    cancellationToken).ConfigureAwait(false);
                return (int)written;
            },
            chunkSize: 2);

        using var deadline = BulkInsertDeadline.Start(null, TestContext.Current.CancellationToken);
        var act = async () => await adapter.WriteAsync(SyncFailRows(5), progress: null, notifyEvery: 1, deadline);

        await act.Should().ThrowAsync<Exception>();
        adapter.RowsConsumed.Should().Be(2, "only the failed chunk was pulled; no unbounded read happened");
        adapter.CompletedDriverCalls.Should().Be(0);
        adapter.RowsWritten.Should().Be(0);
    }

    private static async IAsyncEnumerable<object?[]> SyncFailRows(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return [i, "row"];
        }
    }

    private static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
