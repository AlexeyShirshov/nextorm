using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void BulkInsert_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).BulkInsert();

        written.Should().Be(25);

        var read = ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList();
        read.Should().HaveCount(25);
    }

    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldPersistEveryRow()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(4)).Values(rows).BulkInsert();

        written.Should().Be(25);

        var read = ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList();
        read.Should().HaveCount(25);
    }

    [Fact]
    public void BulkInsert_EmptySource_ShouldWriteNothing()
    {
        var ctx = _sut.DataProvider;

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(Array.Empty<InsertEntity>()).BulkInsert();

        written.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_NotifyAfter_ShouldReportCumulativeProgress()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();
        var seen = new List<int>();

        ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(10).NotifyAfter(10, (n, _) => seen.Add(n)))
            .Values(rows)
            .BulkInsert();

        seen.Should().NotBeEmpty();
        seen.Should().Contain(10).And.Contain(20);
        seen.Should().OnlyContain(v => v <= 25);
        seen.Should().BeInAscendingOrder();

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList().Should().HaveCount(25);
    }

    [Fact]
    public void BulkInsert_NotifyAfterThrows_ShouldPropagate()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .MaxBatchSize(10)
                .NotifyAfter(1, (_, _) => throw new InvalidOperationException("boom")))
            .Values(rows)
            .BulkInsert();

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void BulkInsert_ProgressCancellation_ShouldThrowOperationCanceled()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();
        using var cts = new CancellationTokenSource();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .MaxBatchSize(10)
                .ProgressCancellationTokenSource(cts)
                .NotifyAfter(10, (_, _) => cts.Cancel()))
            .Values(rows)
            .BulkInsert();

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void BulkInsert_ReturningKey_ShouldReturnGeneratedKeys()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return inserted rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 3).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

        var keys = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).ReturningKey<long>().ToList();

        keys.Should().HaveCount(3);
        keys.Should().OnlyContain(key => key > 0);

        var read = ctx.From<IInsertEntity>().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id }).ToList();
        read.Should().HaveCount(3);
    }

    [Fact]
    public void BulkInsert_DestinationOverride_ShouldWriteToOverriddenTable()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 5).Select(i => new BulkDestinationEntity { Name = marker, Age = i }).ToList();

        var written = ctx.CreateBulkInsertBuilder<IBulkDestinationEntity>(o => o.Table("insert_entity")).Values(rows).BulkInsert();

        written.Should().Be(5);

        var read = ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList();
        read.Should().HaveCount(5);
    }

    [Fact]
    public void BulkInsert_KeepIdentity_OnNonIdentityTable_ShouldNotEmitIdentityInsert()
    {
        var ctx = _sut.DataProvider;
        var first = Random.Shared.Next(1_000_000, 2_000_000_000);
        int[] ids = [first, first + 1];
        var rows = ids.Select(id => new DeleteEntity { Id = id, Name = "keep_identity", Age = id % 100 }).ToList();

        try
        {
            var written = ctx.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity()).Values(rows).BulkInsert();

            written.Should().Be(2);

            var read = ctx.From<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id }).ToList();
            read.Should().HaveCount(2);
        }
        finally
        {
            ctx.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldSkipConflictingKeys()
    {
        Assert.SkipUnless(Provider.SupportsIgnoreDuplicates, "This provider cannot skip conflicting rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var id = Random.Shared.NextInt64(8_000_000_000L, 9_000_000_000L);

        var first = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 1 }])
            .BulkInsert();
        first.Should().Be(1);

        var second = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 2 }])
            .BulkInsert();
        second.Should().Be(0);

        var read = ctx.From<IInsertEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList();
        read.Should().ContainSingle().Which.Age.Should().Be(1);
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_WithReturning_ShouldReturnOnlyInsertedRows()
    {
        Assert.SkipUnless(
            Provider.SupportsIgnoreDuplicates && Provider.SupportsInsertReturning,
            "This provider cannot both skip conflicting rows and return written rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var id = Random.Shared.NextInt64(9_000_000_000L, 10_000_000_000L);

        var inserted = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 1 }])
            .ReturningKey<long>()
            .ToList();
        inserted.Should().ContainSingle().Which.Should().Be(id);

        var skipped = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 2 }])
            .ReturningKey<long>()
            .ToList();
        skipped.Should().BeEmpty();

        var read = ctx.From<IInsertEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList();
        read.Should().ContainSingle().Which.Age.Should().Be(1);
    }

    // ================================================================ T305 (#305) D6 matrix, cross-provider cells
    //
    // The 23-row provider matrix is certified per provider in the T305 classes; the facts below are the
    // contracts that hold on every provider of the shared suite (SQL Server, PostgreSQL, MySQL,
    // ClickHouse, SQLite) and read the rows back from the live database, never only the rendered SQL.

    /// <summary>
    /// Issue requirement 3: a bulk call site that sets <em>no</em> options keeps the unchanged call shape
    /// and writes the same rows. The no-option statement must be exactly the plain historical
    /// <c>insert ... values</c> form (no identity toggle, no conflict clause, no provider hint), identical
    /// to an explicitly empty option set and to the explicitly portable rendering.
    /// </summary>
    [Fact]
    public void BulkInsert_DefaultPathControl_ShouldKeepTheHistoricalCallShapeAndRows()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 25);

        var implicitSql = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).ToSql();
        var emptyOptionsSql = ctx.CreateBulkInsertBuilder<IInsertEntity>(new BulkInsertOptionsBuilder().Build()).Values(rows).ToSql();
        var portableSql = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable)).Values(rows).ToSql();

        implicitSql.Should().StartWith("insert into ");
        implicitSql.Should().NotContain("identity_insert").And.NotContain("overriding").And.NotContain("ignore")
            .And.NotContain("on conflict").And.NotContain("settings");
        implicitSql.Should().Be(emptyOptionsSql, "an explicitly empty option set must not change the statement");
        implicitSql.Should().Be(portableSql, "the no-option call keeps the unchanged SQL statement shape");

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(rows).BulkInsert();

        written.Should().Be(25);
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(25);
    }

    /// <summary>Row 3, <c>KeepIdentity=true</c>: the explicit identity values reach the table on every path.</summary>
    [Fact]
    public void BulkInsert_KeepIdentityTrue_ShouldWriteTheExplicitIdentityValues()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var first = Random.Shared.NextInt64(4_100_000_000_000L, 4_200_000_000_000L);
        long[] ids = [first, first + 1, first + 2];
        var rows = ids.Select((id, i) => new InsertEntity { Id = id, Name = marker, Age = i }).ToList();

        try
        {
            var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.KeepIdentity()).Values(rows).BulkInsert();

            written.Should().Be(3);
            ctx.From<IInsertEntity>().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id }).ToList()
                .Select(x => x.Id).Should().BeEquivalentTo(ids);
        }
        finally
        {
            ctx.CreateDeleteBuilder<IInsertEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    /// <summary>Row 4: with no identity column in the mapping the request is a documented no-effect.</summary>
    [Fact]
    public void BulkInsert_KeepIdentity_WithoutAnIdentityColumn_ShouldBeAHonouredNoEffect()
    {
        var ctx = _sut.DataProvider;
        var first = Random.Shared.Next(1_700_000_000, 1_950_000_000);
        int[] ids = [first, first + 1];

        try
        {
            var written = ctx.CreateBulkInsertBuilder<IDeleteEntity>(o => o.KeepIdentity())
                .Values(ids.Select(id => new DeleteEntity { Id = id, Name = "keep_identity_no_column", Age = id % 100 }).ToList())
                .BulkInsert();

            written.Should().Be(2);
            ctx.From<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id }).ToList().Should().HaveCount(2);
        }
        finally
        {
            ctx.CreateDeleteBuilder<IDeleteEntity>().Where(x => ids.Contains(x.Id)).Delete();
        }
    }

    /// <summary>Row 11: <c>MaxParameters</c> splits the portable statement at the exact parameter count.</summary>
    [Fact]
    public void BulkInsert_MaxParameters_ShouldSplitWithoutDroppingRows()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 25);

        // Two written columns and three bound values per row (the provider adds its own identity handling),
        // so a six-parameter statement carries two rows and the write needs several statements.
        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).MaxParameters(6))
            .Values(rows)
            .BulkInsert();

        written.Should().Be(25);
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(25);
    }

    /// <summary>Row 11: <c>MaxSqlLength</c> splits on the rendered statement length, never silently dropping rows.</summary>
    [Fact]
    public void BulkInsert_MaxSqlLength_ShouldSplitWithoutDroppingRows()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 25);

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Mode(BulkInsertMode.Portable).MaxSqlLength(150))
            .Values(rows)
            .BulkInsert();

        written.Should().Be(25);
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(25);
    }

    /// <summary>Row 10: <c>MaxBatchSize</c> really splits the write (progress after each completed batch).</summary>
    [Fact]
    public void BulkInsert_MaxBatchSize_ShouldSplitAtEveryBoundary()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 23);
        var seen = new List<int>();

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.MaxBatchSize(7).NotifyAfter(7, (n, _) => seen.Add(n)))
            .Values(rows)
            .BulkInsert();

        written.Should().Be(23);
        seen.Should().Contain(7).And.Contain(14).And.Contain(21);
        seen.Should().BeInAscendingOrder();
        seen.Should().OnlyContain(n => n <= 23);
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(23);
    }

    /// <summary>Row 16: the default <c>Regular</c> target can be requested explicitly without changing anything.</summary>
    [Fact]
    public void BulkInsert_RegularTarget_ShouldWriteToTheOrdinaryTable()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 5);

        var written = ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.TargetKind(BulkInsertTargetKind.Regular))
            .Values(rows)
            .BulkInsert();

        written.Should().Be(5);
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(5);
    }

    /// <summary>Row 15: <c>ServerName</c> is refused by name on every provider before a row is read.</summary>
    [Fact]
    public void BulkInsert_ServerName_ShouldBeRefusedBeforeTheSourceIsRead()
    {
        var ctx = _sut.DataProvider;
        var source = new T305CountingSource(T305Rows(InsertMarker(), 2));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Server("remote"))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*ServerName*");
        source.Enumerated.Should().Be(0);
    }

    /// <summary>Row 17: a local temporary target never carries a database qualification.</summary>
    [Fact]
    public void BulkInsert_LocalTemporaryWithDatabaseName_ShouldBeRefusedBeforeTheSourceIsRead()
    {
        var ctx = _sut.DataProvider;
        var source = new T305CountingSource(T305Rows(InsertMarker(), 2));

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .TargetKind(BulkInsertTargetKind.LocalTemporary)
                .Table("t305_temp_rows")
                .Database("other"))
            .Values(source)
            .BulkInsert();

        act.Should().Throw<NotSupportedException>().WithMessage("*LocalTemporary*DatabaseName*");
        source.Enumerated.Should().Be(0);
    }

    /// <summary>Row 12: one monotonic budget from execution entry; an expired budget is a timeout, not a replay.</summary>
    [Fact]
    public void BulkInsert_ExpiredTimeout_ShouldThrowTimeoutExceptionWithoutAReplay()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();

        var act = () => ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o.Timeout(TimeSpan.FromMilliseconds(20)))
            .Values(T305SlowRows(marker, 5, TimeSpan.FromMilliseconds(60)))
            .BulkInsert();

        act.Should().Throw<TimeoutException>();

        // The budget is one monotonic span and the terminal never replays a row (A12 promises no rollback,
        // so a batch already handed to the driver may have landed); what must not happen is a second write of
        // the same rows or a silently swallowed timeout.
        var written = ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList();
        written.Should().HaveCountLessThanOrEqualTo(5, "no row is written twice when the budget expires");
        written.Select(row => row.Age).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Row 23: the asynchronous terminal awaits the write, keeps the caller's token and connection usable.</summary>
    [Fact]
    public async Task BulkInsertAsync_ShouldAwaitTheWriteAndLeaveTheConnectionUsable()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = T305Rows(marker, 25);
        using var progressCancellation = new CancellationTokenSource();
        var seen = new List<int>();

        var written = await ctx.CreateBulkInsertBuilder<IInsertEntity>(o => o
                .MaxBatchSize(9)
                .ProgressCancellationTokenSource(progressCancellation)
                .NotifyAfter(9, (n, token) =>
                {
                    token.IsCancellationRequested.Should().BeFalse();
                    seen.Add(n);
                }))
            .Values(T305AsyncRows(rows))
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        written.Should().Be(25);
        seen.Should().NotBeEmpty();
        progressCancellation.IsCancellationRequested.Should().BeFalse("the caller-owned token source is never cancelled by nextorm");
        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Age }).ToList().Should().HaveCount(25);

        var control = ctx.CreateBulkInsertBuilder<IInsertEntity>().Values(T305Rows(marker + "_2", 1)).BulkInsert();
        control.Should().Be(1, "the connection stays usable after the awaited write");
    }

    private static List<InsertEntity> T305Rows(string marker, int count) =>
        Enumerable.Range(0, count).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();

    private static IEnumerable<InsertEntity> T305SlowRows(string marker, int count, TimeSpan delay)
    {
        for (var i = 0; i < count; i++)
        {
            Thread.Sleep(delay);
            yield return new InsertEntity { Name = marker, Age = i };
        }
    }

    private static async IAsyncEnumerable<InsertEntity> T305AsyncRows(IEnumerable<InsertEntity> rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return row;
        }
    }

    /// <summary>Counts the rows nextorm pulled, proving a refusal happens before the source is read.</summary>
    private sealed class T305CountingSource(IEnumerable<InsertEntity> rows) : IEnumerable<InsertEntity>
    {
        public int Enumerated { get; private set; }

        public IEnumerator<InsertEntity> GetEnumerator()
        {
            foreach (var row in rows)
            {
                Enumerated++;
                yield return row;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
