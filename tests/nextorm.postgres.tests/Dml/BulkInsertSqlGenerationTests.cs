using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation for the bulk-insert terminals on PostgreSQL. These tests never open a database.
/// </summary>
public class BulkInsertSqlGenerationTests
{
    private static InsertEntity Row(string name, int age) => new() { Name = name, Age = age };

    private static NullableBulkEntity NullableRow(string name, string? note) => new() { Name = name, Note = note };

    [Fact]
    public void BulkInsert_ShouldRenderMultiRowValues()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldRenderOnlyTheFirstBatch()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(1)).Values([Row("a", 1), Row("b", 2)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_ShouldAddOverridingSystemValue()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (id, name, age) overriding system value values (@p0, @p1, @p2)");
    }

    [Fact]
    public void BulkInsert_ModeNative_KeepIdentity_ShouldWriteTheExplicitIdentityColumnThroughCopy()
    {
        using var ctx = new RecordingPostgresContext();

        // Row 3: binary COPY carries the explicit identity column in its column list, so the native path
        // honours KeepIdentity; OVERRIDING SYSTEM VALUE remains the portable form's job.
        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).KeepIdentity())
            .Values([Row("a", 1)])
            .BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Single().CopyCommand.Should().Be("COPY insert_entity (\"id\", \"name\", \"age\") FROM STDIN (FORMAT BINARY)");
    }

    [Fact]
    public void BulkInsert_ModeNative_IgnoreDuplicates_ShouldThrowNamingTheOption()
    {
        using var ctx = new RecordingPostgresContext();

        // Row 5: COPY has no ON CONFLICT form, so an explicit Native request is refused by name.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Native).IgnoreDuplicates())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*PostgreSQL*IgnoreDuplicates=true*");
        ctx.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_ModeAuto_IgnoreDuplicates_ShouldStayOnThePortablePath()
    {
        using var ctx = new RecordingPostgresContext();

        // Auto falls back to the portable statement (the ON CONFLICT DO NOTHING form asserted above)
        // instead of refusing the request, and never enters the native COPY path.
        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates())
            .Values([Row("a", 1)])
            .ValidateTarget();

        act.Should().NotThrow();
        ctx.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldUseOnConflictDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) on conflict do nothing");
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldEmitReturning()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) returning id");
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_WithReturningKey_ShouldEmitOnConflictDoNothingThenReturning()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.IgnoreDuplicates()).Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into insert_entity (name, age) values (@p0, @p1) on conflict do nothing returning id");
    }

    [Fact]
    public void BulkInsert_TableOverride_ShouldTargetOverriddenTable()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverrideWithSchema_ShouldQualifyTarget()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void BulkInsert_TableOverride_WithReturningKey_ShouldUseOverriddenTarget()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Table("staging", "bulk_target")).Values([Row("a", 1)]).ReturningKey<long>().ToSql();

        sql.Should().Be("insert into staging.bulk_target (name, age) values (@p0, @p1) returning id");
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityEntity_ShouldNotAddOverridingSystemValue()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.CreateBulkInsertBuilder<ISimpleEntity>(o => o.KeepIdentity()).Values([new SimpleEntity { Id = 7 }]).ToSql();

        sql.Should().Be("insert into simple_entity (id) values (@p0)");
    }

    [Fact]
    public void BulkInsert_WithSqlServerOnlyFlags_ShouldRefuseOnlyTheSemanticallyUnsupportedFlags()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .CheckConstraints()
                .TableLock()
                .KeepNulls()
                .FireTriggers())
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*TableLock=true*FireTriggers=true*");
    }

    [Fact]
    public void BulkInsert_TableLockTrue_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock()).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*TableLock=true*");
    }

    [Fact]
    public void BulkInsert_TableLockFalse_ShouldBeHonouredByAbsence()
    {
        using var ctx = new RecordingPostgresContext();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TableLock(false)).Values([Row("a", 1)]).BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Should().ContainSingle();
    }

    [Fact]
    public void BulkInsert_CheckConstraintsFalse_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.CheckConstraints(false)).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*CheckConstraints=false*");
    }

    [Fact]
    public void BulkInsert_FireTriggersTrue_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.FireTriggers()).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*FireTriggers=true*");
    }

    [Fact]
    public void BulkInsert_ServerName_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Server("remote")).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
    }

    [Fact]
    public void BulkInsert_DatabaseName_OtherDatabase_ShouldThrow()
    {
        using var ctx = new RecordingPostgresContext();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("other_db")).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*DatabaseName*other_db*current database*");
        ctx.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_DatabaseName_CurrentDatabase_ShouldBeAccepted()
    {
        using var ctx = new RecordingPostgresContext();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Database("nextorm")).Values([Row("a", 1)]).BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Single().CopyCommand.Should().Be("COPY insert_entity (\"name\", \"age\") FROM STDIN (FORMAT BINARY)");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_ShouldAddressTheSessionTableWithoutDdl()
    {
        using var ctx = new RecordingPostgresContext();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("pg_temp.bulk_stage"))
            .Values([Row("a", 1)])
            .BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Single().CopyCommand.Should().Be("COPY pg_temp.bulk_stage (\"name\", \"age\") FROM STDIN (FORMAT BINARY)");
    }

    [Fact]
    public void BulkInsert_LocalTemporary_WithDatabaseName_ShouldThrow()
    {
        using var ctx = new RecordingPostgresContext();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Database("other_db"))
            .Values([Row("a", 1)])
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
    }

    [Fact]
    public void BulkInsert_KeepNullsFalse_ShouldRefuseNonDefaultEquivalentShape()
    {
        using var ctx = new RecordingPostgresContext();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepNulls(false)).Values([Row("a", 1)]).BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*KeepNulls=false*age*");
        ctx.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void BulkInsert_KeepNullsFalse_ShouldAcceptAnAllNullableShape()
    {
        using var ctx = new RecordingPostgresContext();

        var written = ctx.CreateBulkInsertBuilder<INullableBulkEntity>(o => o.KeepNulls(false))
            .Values([NullableRow("a", null)])
            .BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Should().ContainSingle();
    }

    [Fact]
    public void BulkInsert_Timeout_ShouldSetTheImporterTimeoutInsteadOfRefusing()
    {
        using var ctx = new RecordingPostgresContext();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Timeout(TimeSpan.FromSeconds(30)))
            .Values([Row("a", 1)])
            .BulkInsert();

        written.Should().Be(1);
        ctx.Sessions.Single().Timeout.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldSplitCopyIntoBoundedImporters()
    {
        using var ctx = new RecordingPostgresContext();
        var rows = Enumerable.Range(0, 10).Select(i => Row("a", i)).ToList();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(4)).Values(rows).BulkInsert();

        written.Should().Be(10);
        ctx.Sessions.Should().HaveCount(3);
        ctx.Sessions.Select(s => s.Rows.Count).Should().Equal(4, 4, 2);
        ctx.Sessions.Should().OnlyContain(s => s.Completed);
        ctx.Sessions.SelectMany(s => s.Rows).Should().HaveCount(10);
    }

    [Fact]
    public async Task BulkInsertAsync_MaxBatchSize_ShouldCompleteEveryBoundedImporter()
    {
        using var ctx = new RecordingPostgresContext();
        var rows = Enumerable.Range(0, 7).Select(i => Row("a", i)).ToList();

        var written = await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(3)).Values(rows).BulkInsertAsync(TestContext.Current.CancellationToken);

        written.Should().Be(7);
        ctx.Sessions.Select(s => s.Rows.Count).Should().Equal(3, 3, 1);
        ctx.Sessions.Should().OnlyContain(s => s.Completed && s.Disposed);
    }

    [Fact]
    public async Task BulkInsertAsync_ImporterFailure_ShouldDisposeOnlyTheFailedImporter()
    {
        using var ctx = new RecordingPostgresContext(failAtRow: 2);
        var rows = Enumerable.Range(0, 10).Select(i => Row("a", i)).ToList();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(3)).Values(rows).BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*importer failed*");
        ctx.Sessions.Should().ContainSingle();
        ctx.Sessions[0].Completed.Should().BeFalse();
        ctx.Sessions[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAsync_UserCancellation_ShouldWinOverTheDeadline()
    {
        using var cts = new CancellationTokenSource();
        using var ctx = new RecordingPostgresContext(onRow: index =>
        {
            if (index == 2)
                cts.Cancel();
        });

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Timeout(TimeSpan.FromMinutes(5)))
            .Values([Row("a", 1), Row("b", 2), Row("c", 3)])
            .BulkInsertAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertAsync_ExpiredBudget_ShouldThrowTimeoutException()
    {
        using var ctx = new RecordingPostgresContext(onRowAsync: async (index, _) =>
        {
            if (index == 2)
                await Task.Delay(300, TestContext.Current.CancellationToken);
        });

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Timeout(TimeSpan.FromMilliseconds(50)))
            .Values([Row("a", 1), Row("b", 2), Row("c", 3)])
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}

/// <summary>An entity whose every written column is nullable, so <c>KeepNulls=false</c> stays default-equivalent.</summary>
[SqlTable("nullable_bulk_entity")]
public interface INullableBulkEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }

    [Column("note")]
    string? Note { get; set; }
}

/// <summary>Concrete <see cref="INullableBulkEntity"/> used by the PostgreSQL bulk-insert unit tests.</summary>
public sealed class NullableBulkEntity : INullableBulkEntity
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// A PostgreSQL context whose binary COPY importers are recorded in memory instead of being opened,
/// so the unit tests can observe the importer lifecycle (chunks, <c>Complete</c>, disposal) without a
/// live server.
/// </summary>
internal sealed class RecordingPostgresContext : PostgresDataContext
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private readonly List<RecordingCopySession> _sessions = [];
    private readonly int? _failAtRow;
    private readonly Action<int>? _onRow;
    private readonly Func<int, CancellationToken, Task>? _onRowAsync;

    public RecordingPostgresContext(
        int? failAtRow = null,
        Action<int>? onRow = null,
        Func<int, CancellationToken, Task>? onRowAsync = null)
        : base(PlaceholderConnectionString, new DataContextBuilder())
    {
        _failAtRow = failAtRow;
        _onRow = onRow;
        _onRowAsync = onRowAsync;
    }

    public IReadOnlyList<RecordingCopySession> Sessions => _sessions;

    internal override IBinaryCopySession BeginBinaryCopy(string copyCommand, TimeSpan? timeout)
    {
        var session = new RecordingCopySession(copyCommand, timeout, _failAtRow) { OnRow = _onRow, OnRowAsync = _onRowAsync };
        _sessions.Add(session);
        return session;
    }

    internal override Task<IBinaryCopySession> BeginBinaryCopyAsync(string copyCommand, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var session = new RecordingCopySession(copyCommand, timeout, _failAtRow) { OnRow = _onRow, OnRowAsync = _onRowAsync };
        _sessions.Add(session);
        return Task.FromResult<IBinaryCopySession>(session);
    }
}

/// <summary>An in-memory <c>NpgsqlBinaryImporter</c> stand-in that records what the COPY path did.</summary>
internal sealed class RecordingCopySession(string copyCommand, TimeSpan? timeout, int? failAtRow)
    : PostgresDataContext.IBinaryCopySession
{
    private int _rowIndex;

    public string CopyCommand { get; } = copyCommand;

    public TimeSpan? Timeout { get; } = timeout;

    public List<object?[]> Rows { get; } = [];

    public bool Completed { get; private set; }

    public bool Disposed { get; private set; }

    public Action<int>? OnRow { get; init; }

    public Func<int, CancellationToken, Task>? OnRowAsync { get; init; }

    public void WriteRow(object?[] row)
    {
        _rowIndex++;
        OnRow?.Invoke(_rowIndex);
        ThrowIfFailing();
        Rows.Add(row);
    }

    public async Task WriteRowAsync(object?[] row, CancellationToken cancellationToken)
    {
        _rowIndex++;
        OnRow?.Invoke(_rowIndex);
        if (OnRowAsync is not null)
            await OnRowAsync(_rowIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailing();
        Rows.Add(row);
    }

    public long Complete()
    {
        Completed = true;
        return Rows.Count;
    }

    public Task<long> CompleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Completed = true;
        return Task.FromResult((long)Rows.Count);
    }

    public void Dispose() => Disposed = true;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (failAtRow is { } failing && _rowIndex >= failing)
            throw new InvalidOperationException("the importer failed");
    }
}
