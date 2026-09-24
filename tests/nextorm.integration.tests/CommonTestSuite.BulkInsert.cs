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

        var written = ctx.BulkInsertInto<IInsertEntity>().Values(rows).BulkInsert();

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

        var written = ctx.BulkInsertInto<IInsertEntity>(o => o.MaxBatchSize(4)).Values(rows).BulkInsert();

        written.Should().Be(25);

        var read = ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).ToList();
        read.Should().HaveCount(25);
    }

    [Fact]
    public void BulkInsert_EmptySource_ShouldWriteNothing()
    {
        var ctx = _sut.DataProvider;

        var written = ctx.BulkInsertInto<IInsertEntity>().Values(Array.Empty<InsertEntity>()).BulkInsert();

        written.Should().Be(0);
    }

    [Fact]
    public void BulkInsert_NotifyAfter_ShouldReportCumulativeProgress()
    {
        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var rows = Enumerable.Range(0, 25).Select(i => new InsertEntity { Name = marker, Age = i }).ToList();
        var seen = new List<int>();

        ctx.BulkInsertInto<IInsertEntity>(o => o.MaxBatchSize(10).NotifyAfter(10, (n, _) => seen.Add(n)))
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

        var act = () => ctx.BulkInsertInto<IInsertEntity>(o => o
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

        var act = () => ctx.BulkInsertInto<IInsertEntity>(o => o
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

        var keys = ctx.BulkInsertInto<IInsertEntity>().Values(rows).ReturningKey<long>().ToList();

        keys.Should().HaveCount(3);
        keys.Should().OnlyContain(key => key > 0);

        var read = ctx.From<IInsertEntity>().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id }).ToList();
        read.Should().HaveCount(3);
    }

    [Fact]
    public void BulkInsert_IgnoreDuplicates_ShouldSkipConflictingKeys()
    {
        Assert.SkipUnless(Provider.SupportsIgnoreDuplicates, "This provider cannot skip conflicting rows.");

        var ctx = _sut.DataProvider;
        var marker = InsertMarker();
        var id = Random.Shared.NextInt64(8_000_000_000L, 9_000_000_000L);

        var first = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 1 }])
            .BulkInsert();
        first.Should().Be(1);

        var second = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
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

        var inserted = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 1 }])
            .ReturningKey<long>()
            .ToList();
        inserted.Should().ContainSingle().Which.Should().Be(id);

        var skipped = ctx.BulkInsertInto<IInsertEntity>(o => o.KeepIdentity().IgnoreDuplicates())
            .Values([new InsertEntity { Id = id, Name = marker, Age = 2 }])
            .ReturningKey<long>()
            .ToList();
        skipped.Should().BeEmpty();

        var read = ctx.From<IInsertEntity>().Where(x => x.Id == id).Select(x => new { x.Age }).ToList();
        read.Should().ContainSingle().Which.Age.Should().Be(1);
    }
}
