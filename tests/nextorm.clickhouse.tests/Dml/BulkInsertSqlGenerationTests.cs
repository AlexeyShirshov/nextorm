using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using NextORM.ClickHouse.Bulk;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL generation and the native <c>InsertBinaryAsync</c> contract for the bulk-insert terminals on
/// ClickHouse. The native-path facts run against the pinned driver with a recording HTTP handler, so the
/// driver calls, their session/parallelism request and the settings copy are observed without a server;
/// the live driver/server certification stays in the integration suite.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static readonly (string Name, string Type)[] InsertEntitySchema =
        [("id", "Int64"), ("name", "String"), ("age", "Int32")];

    private static readonly (string Name, string Type)[] NullableBulkEntitySchema =
        [("id", "Nullable(Int64)"), ("note", "Nullable(String)")];

    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (id, name, age) values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldNotEmitIdentityInsertToggle()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().NotContain("identity_insert");
    }

    // ---------------------------------------------------------------------------------------------
    // Row 5: the false capability is corrected. ClickHouse has no unique constraint and no
    // INSERT IGNORE form, so a duplicate-skipping request is refused by name instead of being
    // silently rendered as a plain INSERT that writes every row.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Dialect_ShouldNotAdvertiseInsertIgnore()
    {
        ClickHouseDialect.Instance.SupportsInsertIgnore.Should().BeFalse();
    }

    [Fact]
    public void Dialect_ShouldAdvertiseTheNativeBulkPath()
    {
        ClickHouseDialect.Instance.SupportsBulkCopy.Should().BeTrue();
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldThrowInsteadOfRenderingAPlainInsert()
    {
        using var ctx = ClickHouseTestContext.Create();

        // Rendering the portable statement is the last chance to catch the request: it is refused
        // instead of producing a plain INSERT. The executing path names the option itself (see below).
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*skip conflicting rows*");
    }

    [Fact]
    public async Task BulkInsert_AutoIgnoreDuplicates_ShouldThrowByNameBeforeTheSourceIsRead()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));
        var pulled = 0;

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates())
            .Values(CountingSource(() => pulled++))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*IgnoreDuplicates*");
        pulled.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_ModeNativeIgnoreDuplicates_ShouldThrowNamingTheOption()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).IgnoreDuplicates())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*IgnoreDuplicates=true*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToList();

        act.Should().Throw<NotSupportedException>();
    }

    // ---------------------------------------------------------------------------------------------
    // Rows 21-22: parallelism and session semantics of the native InsertBinaryAsync path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task BulkInsert_NativeSessionWithParallelism_ShouldThrowByNameBeforeAnyRequest()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionSettings(handler, "sess-parallel"));
        var pulled = 0;

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 2 }))
            .Values(CountingSource(() => pulled++))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*MaxDegreeOfParallelism=2*session*");
        pulled.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkInsert_NativeSessionlessTemporary_ShouldThrowByNameBeforeAnyRequest()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));
        var pulled = 0;

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary))
            .Values(CountingSource(() => pulled++))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*LocalTemporary*session*");
        pulled.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkInsert_NativeUseSessionWithoutAnId_ShouldThrowByNameBeforeAnyRequest()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));
        var pulled = 0;

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { UseSession = true }))
            .Values(CountingSource(() => pulled++))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*UseSession=true*session id*");
        pulled.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BulkInsert_NativeInvalidParallelism_ShouldThrowArgumentOutOfRange(int parallelism)
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = parallelism }))
            .Values([Row("a", 1)])
            .BulkInsert();

        // Row 21: an invalid value is an argument error, not a silent driver default.
        act.Should().Throw<ArgumentOutOfRangeException>();
        handler.InsertCount.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_NativeParallelismWithoutASession_ShouldRunOneDriverCallPerChunk()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.MaxBatchSize(2).ClickHouse(new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 2 }))
            .Values([Row("a", 1), Row("b", 2), Row("c", 3), Row("d", 4), Row("e", 5)])
            .BulkInsert();

        written.Should().Be(5);
        handler.InsertCount.Should().Be(3);
        handler.Requests.Should().NotContain(request => request.Url.Contains("session_id", StringComparison.Ordinal));
    }

    [Fact]
    public void BulkInsert_NativeUseSessionTrue_ShouldCarryTheActualSessionId()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionSettings(handler, "sess-true"));

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { UseSession = true }))
            .Values([Row("a", 1)])
            .BulkInsert();

        written.Should().Be(1);
        handler.InsertRequests.Should().ContainSingle()
            .Which.Url.Should().Contain("session_id=sess-true");
    }

    [Fact]
    public void BulkInsert_NativeUseSessionNull_ShouldPreserveTheConfiguredSession()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionSettings(handler, "sess-preserved"));

        ctx.CreateBulkInsertBuilder<IInsertEntity>()
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        handler.InsertRequests.Should().ContainSingle()
            .Which.Url.Should().Contain("session_id=sess-preserved");
    }

    [Fact]
    public void BulkInsert_NativeUseSessionFalse_ShouldRequestASessionlessInsert()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionSettings(handler, "sess-disabled"));

        ctx.CreateBulkInsertBuilder<IInsertEntity>(
                o => o.ClickHouse(new ClickHouseBulkInsertOptions { UseSession = false }))
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        handler.InsertRequests.Should().ContainSingle()
            .Which.Url.Should().NotContain("session_id");
    }

    [Fact]
    public void BulkInsert_NativeLocalTemporary_ShouldUseTheSameSessionWithoutAnyDdl()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionSettings(handler, "sess-temp"));

        // Row 17: an external, pre-existing temporary table is addressed inside the same actual session
        // id; nextorm never creates or drops it.
        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary))
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        handler.InsertRequests.Should().ContainSingle()
            .Which.Url.Should().Contain("session_id=sess-temp");
        handler.Requests.Should().NotContain(request =>
            request.Body.Contains("CREATE", StringComparison.OrdinalIgnoreCase)
            || request.Body.Contains("DROP", StringComparison.OrdinalIgnoreCase)
            || request.Body.Contains("TRUNCATE", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(2, true, "MaxDegreeOfParallelism")]
    [InlineData(1, false, "UseSession=true")]
    public void PortableFallback_WithANativeOnlyRequest_ShouldThrowByName(
        int parallelism, bool connectionUsesSession, string expected)
    {
        var options = new BulkInsertOptions
        {
            Mode = BulkInsertMode.Auto,
            ClickHouse = new ClickHouseBulkInsertOptions
            {
                MaxDegreeOfParallelism = parallelism,
                UseSession = connectionUsesSession ? null : true,
            },
        };

        var act = () => ClickHouseInsertRequest.ValidatePortableFallback(options, connectionUsesSession);

        act.Should().Throw<NotSupportedException>().WithMessage("*" + expected + "*");
    }

    [Fact]
    public void PortableFallback_WithASessionlessRequestOnASessionConnection_ShouldThrowByName()
    {
        var options = new BulkInsertOptions
        {
            Mode = BulkInsertMode.Auto,
            ClickHouse = new ClickHouseBulkInsertOptions { UseSession = false },
        };

        var act = () => ClickHouseInsertRequest.ValidatePortableFallback(options, connectionUsesSession: true);

        act.Should().Throw<NotSupportedException>().WithMessage("*UseSession=false*");
    }

    [Fact]
    public void PortableFallback_WithTheEquivalentSingleStreamRequest_ShouldBeAccepted()
    {
        var options = new BulkInsertOptions
        {
            Mode = BulkInsertMode.Auto,
            ClickHouse = new ClickHouseBulkInsertOptions { MaxDegreeOfParallelism = 1 },
        };

        var act = () => ClickHouseInsertRequest.ValidatePortableFallback(options, connectionUsesSession: false);

        act.Should().NotThrow();
    }

    // ---------------------------------------------------------------------------------------------
    // Rows 6-9: constraint/lock/nulls/trigger requests. A requested value must be refused by name or
    // honoured; an explicit false is honoured by absence and never becomes a request.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void BulkInsert_CheckConstraintsTrue_ShouldBeAcceptedOnTheNativePath()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.CheckConstraints())
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        handler.InsertCount.Should().Be(1);
    }

    [Fact]
    public void BulkInsert_CheckConstraintsFalse_ShouldThrowByName()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.CheckConstraints(false))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    [Fact]
    public void BulkInsert_TableLockTrue_ShouldThrowByName()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
    }

    [Fact]
    public void BulkInsert_TableLockFalse_ShouldBeHonouredByAbsence()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock(false))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().NotThrow();
        handler.InsertCount.Should().Be(1);
    }

    [Fact]
    public void BulkInsert_FireTriggersTrue_ShouldThrowByName()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.FireTriggers())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers=true*");
    }

    [Fact]
    public void BulkInsert_FireTriggersFalse_ShouldBeHonouredByAbsence()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.FireTriggers(false))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().NotThrow();
        handler.InsertCount.Should().Be(1);
    }

    [Fact]
    public void BulkInsert_KeepNullsTrue_ShouldBeAccepted()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls())
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);
    }

    [Fact]
    public async Task BulkInsert_KeepNullsFalse_OnANonDefaultEquivalentShape_ShouldThrow()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));
        var pulled = 0;

        // id (long) and age (int) are non-nullable, so writing NULL is not equivalent to the DEFAULT.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls(false))
            .Values(CountingSource(() => pulled++))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*KeepNulls=false*");
        pulled.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_KeepNullsFalse_OnAnAllNullableShape_ShouldBeAcceptedWithoutAPartialColumnList()
    {
        var handler = new RecordingClickHouseHandler(NullableBulkEntitySchema);
        using var ctx = ClientContext(SessionlessSettings(handler));

        var written = ctx.CreateBulkInsertBuilder<INullableBulkEntity>(o => o.KeepNulls(false))
            .Values([new NullableBulkEntity { Id = 1, Note = null }])
            .BulkInsert();

        written.Should().Be(1);
        // The whole row is still written: no trick of omitting a NULL column to fake the DEFAULT.
        var sql = ctx.CreateBulkInsertBuilder<INullableBulkEntity>(o => o.KeepNulls(false))
            .Values([new NullableBulkEntity { Id = 2, Note = null }])
            .ToSql();
        sql.Should().Be("insert into nullable_bulk_entity (id, note) values (@p0, @p1)");
    }

    // ---------------------------------------------------------------------------------------------
    // Rows 13-15: schema is the ClickHouse database, rendered as database.table; a server part is
    // refused and the two aliases cannot disagree.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void BulkInsert_DatabaseName_ShouldRenderDatabaseTable()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("staging"))
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Be("insert into staging.insert_entity (id, name, age) values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_SchemaName_ShouldRenderTheSameDatabaseQualifier()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Schema("staging"))
            .Values([Row("a", 1)])
            .ToSql();

        sql.Should().Be("insert into staging.insert_entity (id, name, age) values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_DatabaseAndSchema_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("staging").Schema("other"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*same qualifier*");
    }

    [Fact]
    public void BulkInsert_ServerName_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Server("remote"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void BulkInsert_LocalTemporaryWithDatabaseName_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.LocalTemporary).Database("staging"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*");
    }

    // ---------------------------------------------------------------------------------------------
    // A09/G2: the client/data-source constructors, the full settings copy and the ownership rules.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithClient_ShouldCopyEverySettingAndPreserveTheConfiguredSession()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        var settings = SessionSettings(handler, "sess-copied");
        settings = new ClickHouseClientSettings(settings)
        {
            CustomSettings = new Dictionary<string, object> { ["max_threads"] = 7 },
        };

        using var client = new ClickHouseClient(settings);
        using (var ctx = new ClickHouseDataContext(client, new DataContextBuilder()))
        {
            ctx.CreateBulkInsertBuilder<IInsertEntity>()
                .Values([Row("a", 1)])
                .BulkInsert()
                .Should().Be(1);
        }

        var insert = handler.InsertRequests.Should().ContainSingle().Subject;
        insert.Url.Should().Contain("session_id=sess-copied");
        // A custom setting cannot survive a connection-string round trip, so its presence proves the
        // full settings copy.
        insert.Url.Should().Contain("max_threads=7");
        settings.SessionId.Should().Be("sess-copied");
        settings.UseSession.Should().BeTrue();
        settings.CustomSettings["max_threads"].Should().Be(7);
    }

    [Fact]
    public async Task Constructor_WithClient_ShouldNotDisposeTheCallerClient()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var client = new ClickHouseClient(SessionlessSettings(handler));
        var ctx = new ClickHouseDataContext(client, new DataContextBuilder());

        ctx.Dispose();

        ctx.OwnedClient.Should().BeNull();
        ctx.OwnedClientDisposalCount.Should().Be(0);

        // The caller still owns a usable client.
        var written = await client.InsertBinaryAsync(
            "insert_entity",
            ["id", "name", "age"],
            [new object?[] { 1L, "a", 1 }],
            new InsertOptions { UseSchemaCache = true },
            TestContext.Current.CancellationToken);

        written.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithDataSource_ShouldNotDisposeTheCallerDataSource()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var dataSource = new ClickHouseDataSource(SessionlessSettings(handler));
        var ctx = new ClickHouseDataContext(dataSource, new DataContextBuilder());

        ctx.CreateBulkInsertBuilder<IInsertEntity>()
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        ctx.Dispose();

        ctx.OwnedClient.Should().BeNull();
        ctx.OwnedClientDisposalCount.Should().Be(0);
        dataSource.GetClient().Settings.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConnection_ShouldDisposeTheOwnedClientExactlyOnce()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        var settings = SessionSettings(handler, "sess-owned");
        var connection = new ClickHouseConnection(settings);
        var ctx = new ClickHouseDataContext(connection, new DataContextBuilder());

        ctx.CreateBulkInsertBuilder<IInsertEntity>()
            .Values([Row("a", 1)])
            .BulkInsert()
            .Should().Be(1);

        ctx.OwnedClient.Should().NotBeNull();

        ctx.Dispose();
        ctx.Dispose();

        ctx.OwnedClient.Should().BeNull();
        ctx.OwnedClientDisposalCount.Should().Be(1);
        // The caller-supplied connection is never disposed by the context.
        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void UseClickHouse_WithClient_ShouldCreateAContextOverThatClient()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var client = new ClickHouseClient(SessionlessSettings(handler));

        using var ctx = new DataContextBuilder().UseClickHouse(client).CreateDataContext();

        ctx.Should().BeOfType<ClickHouseDataContext>();
    }

    [Fact]
    public void UseClickHouse_WithDataSource_ShouldCreateAContextOverThatDataSource()
    {
        var handler = new RecordingClickHouseHandler(InsertEntitySchema);
        using var dataSource = new ClickHouseDataSource(SessionlessSettings(handler));

        using var ctx = new DataContextBuilder().UseClickHouse(dataSource).CreateDataContext();

        ctx.Should().BeOfType<ClickHouseDataContext>();
    }

    // ---------------------------------------------------------------------------------------------
    // A12/A15: the bounded adapter. Rows are pulled one chunk at a time, the next chunk is not pulled
    // before the awaited driver call completes, progress is reported only after a completed call, and
    // the consumed/completed counters are what the live measurement reads.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Adapter_ShouldWriteInBoundedChunksAndCountConsumedRowsAndDriverCalls()
    {
        var sends = new List<int>();
        var adapter = new ClickHouseBulkInsertAdapter(
            (chunk, _) =>
            {
                sends.Add(chunk.Count);
                return Task.FromResult(chunk.Count);
            },
            chunkSize: 2);

        using var deadline = BulkInsertDeadline.Start(null, TestContext.Current.CancellationToken);
        var written = await adapter.WriteAsync(Rows(5), progress: null, notifyEvery: 1, deadline);

        written.Should().Be(5);
        sends.Should().Equal(2, 2, 1);
        adapter.RowsConsumed.Should().Be(5);
        adapter.CompletedDriverCalls.Should().Be(3);
        adapter.RowsWritten.Should().Be(5);
    }

    [Fact]
    public void Adapter_ShouldNotPullTheNextChunkBeforeTheDriverCallCompletes()
    {
        var pulled = 0;
        int? pulledAtFirstSend = null;

        IEnumerable<object?[]> Source()
        {
            for (var i = 0; i < 5; i++)
            {
                pulled++;
                yield return [i];
            }
        }

        var adapter = new ClickHouseBulkInsertAdapter(
            (chunk, _) =>
            {
                pulledAtFirstSend ??= pulled;
                return Task.FromResult(chunk.Count);
            },
            chunkSize: 2);

        using var deadline = BulkInsertDeadline.Start(null, CancellationToken.None);
        adapter.Write(Source(), progress: null, notifyEvery: 1, deadline).Should().Be(5);

        // Retention is bounded by the chunk size: at the first driver call only its own chunk was pulled.
        pulledAtFirstSend.Should().Be(2);
    }

    [Fact]
    public void Adapter_ShouldReportProgressOnlyAfterCompletedDriverCalls()
    {
        var reported = new List<int>();
        var adapter = new ClickHouseBulkInsertAdapter(
            (chunk, _) => Task.FromResult(chunk.Count),
            chunkSize: 2);

        using var deadline = BulkInsertDeadline.Start(null, CancellationToken.None);
        adapter.Write(SyncRows(5), reported.Add, notifyEvery: 2, deadline);

        reported.Should().Equal(2, 4, 5);
    }

    [Fact]
    public async Task Adapter_WhenTheDriverCallFails_ShouldNotCountItAsCompleted()
    {
        var adapter = new ClickHouseBulkInsertAdapter(
            (_, _) => throw new InvalidOperationException("driver failure"),
            chunkSize: 2);

        using var deadline = BulkInsertDeadline.Start(null, TestContext.Current.CancellationToken);

        var act = async () => await adapter.WriteAsync(Rows(5), progress: null, notifyEvery: 1, deadline);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("driver failure");
        adapter.RowsConsumed.Should().Be(2);
        adapter.CompletedDriverCalls.Should().Be(0);
        adapter.RowsWritten.Should().Be(0);
    }

    private static async IAsyncEnumerable<object?[]> Rows(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return [i];
        }
    }

    private static IEnumerable<object?[]> SyncRows(int count)
    {
        for (var i = 0; i < count; i++)
            yield return [i];
    }

    private static async IAsyncEnumerable<IInsertEntity> CountingSource(Action onPull)
    {
        onPull();
        await Task.Yield();
        yield return Row("a", 1);
    }

    private static ClickHouseClientSettings SessionlessSettings(RecordingClickHouseHandler handler)
        => new("Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm")
        {
            HttpClient = new HttpClient(handler),
            UseCompression = false,
        };

    private static ClickHouseClientSettings SessionSettings(RecordingClickHouseHandler handler, string sessionId)
        => new("Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm")
        {
            HttpClient = new HttpClient(handler),
            UseCompression = false,
            UseSession = true,
            SessionId = sessionId,
        };

    private static ClickHouseDataContext ClientContext(ClickHouseClientSettings settings)
        => new(new ClickHouseClient(settings), new DataContextBuilder());
}

/// <summary>An entity whose every written column is nullable, so <c>KeepNulls=false</c> stays default-equivalent.</summary>
[SqlTable("nullable_bulk_entity")]
public interface INullableBulkEntity
{
    [Key]
    [Column("id")]
    long? Id { get; set; }

    [Column("note")]
    string? Note { get; set; }
}

/// <summary>Concrete <see cref="INullableBulkEntity"/> used by the ClickHouse bulk-insert unit tests.</summary>
public sealed class NullableBulkEntity : INullableBulkEntity
{
    public long? Id { get; set; }

    public string? Note { get; set; }
}

/// <summary>A recorded driver request: the URL (session and custom settings) and the readable body.</summary>
/// <param name="IsSchemaQuery">Whether the driver was reading the destination schema.</param>
/// <param name="Url">The request URL.</param>
/// <param name="Body">The request body, decoded as text (an insert body is binary, so only its prefix is readable).</param>
public sealed record RecordedClickHouseRequest(bool IsSchemaQuery, string Url, string Body);

/// <summary>
/// Records the pinned driver's HTTP requests and answers the destination-schema query with a
/// <c>RowBinaryWithNamesAndTypes</c> header, so the native <c>InsertBinaryAsync</c> path runs without a
/// server while every driver call, its session/parallelism request and the settings copy stay observable.
/// </summary>
/// <param name="columns">The destination table's columns, as the (name, ClickHouse type) pairs to report.</param>
public sealed class RecordingClickHouseHandler(params (string Name, string Type)[] columns) : HttpMessageHandler
{
    private readonly List<RecordedClickHouseRequest> _requests = [];

    /// <summary>Every recorded request, in order.</summary>
    public IReadOnlyList<RecordedClickHouseRequest> Requests => _requests;

    /// <summary>The insert requests only.</summary>
    public IEnumerable<RecordedClickHouseRequest> InsertRequests => _requests.Where(request => !request.IsSchemaQuery);

    /// <summary>The number of insert requests.</summary>
    public int InsertCount => _requests.Count(request => !request.IsSchemaQuery);

    /// <summary>The number of destination-schema queries.</summary>
    public int SchemaQueryCount => _requests.Count(request => request.IsSchemaQuery);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var isSchemaQuery = body.StartsWith("SELECT", StringComparison.Ordinal);
        _requests.Add(new RecordedClickHouseRequest(isSchemaQuery, request.RequestUri!.ToString(), body));

        if (!isSchemaQuery)
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };

        var content = new ByteArrayContent(Schema());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    // RowBinaryWithNamesAndTypes: the column count, then every name, then every type, then zero rows.
    private byte[] Schema()
    {
        var bytes = new List<byte> { (byte)columns.Length };
        foreach (var (name, _) in columns)
            Append(bytes, Encoding.UTF8.GetBytes(name));
        foreach (var (_, type) in columns)
            Append(bytes, Encoding.UTF8.GetBytes(type));

        return [.. bytes];
    }

    private static void Append(List<byte> target, byte[] value)
    {
        target.Add((byte)value.Length);
        target.AddRange(value);
    }
}
