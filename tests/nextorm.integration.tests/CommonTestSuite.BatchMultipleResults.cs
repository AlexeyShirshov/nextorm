using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Batch_MultipleResults_ShouldMaterialiseEverySetInOrder()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var first = "batch-multi-a-" + Guid.NewGuid().ToString("N");
        var second = "batch-multi-b-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.CreateInsertBuilder<IDeleteEntity>().Values([
            new DeleteEntity { Id = id, Name = first, Age = 1 },
            new DeleteEntity { Id = id + 1, Name = second, Age = 2 },
        ]).Insert();

        var result = ctx.CreateBatchBuilder()
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Name == first).Select(x => x.Name))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Name == second).Select(x => x.Name))
            .Execute();

        result.ResultSetCount.Should().Be(2);
        result.Read<string>().Should().Equal(first);
        result.Read<string>().Should().Equal(second);
    }

    [Fact]
    public async Task Batch_MultipleResultsAsync_ShouldMaterialiseEverySetInOrder()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var first = "batch-multi-async-a-" + Guid.NewGuid().ToString("N");
        var second = "batch-multi-async-b-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.CreateInsertBuilder<IDeleteEntity>().Values([
            new DeleteEntity { Id = id, Name = first, Age = 1 },
            new DeleteEntity { Id = id + 1, Name = second, Age = 2 },
        ]).Insert();

        var result = await ctx.CreateBatchBuilder()
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Name == first).Select(x => x.Name))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Name == second).Select(x => x.Name))
            .ExecuteAsync(TestContext.Current.CancellationToken);

        result.ResultSetCount.Should().Be(2);
        result.Read<string>().Should().Equal(first);
        result.Read<string>().Should().Equal(second);
    }

    [Fact]
    public void Batch_SideEffectThenMultipleResults_ShouldMaterialiseEverySetInOrder()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var first = "batch-multi-side-a-" + Guid.NewGuid().ToString("N");
        var second = "batch-multi-side-b-" + Guid.NewGuid().ToString("N");
        var updated = "batch-multi-side-updated-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.CreateInsertBuilder<IDeleteEntity>().Values([
            new DeleteEntity { Id = id, Name = first, Age = 1 },
            new DeleteEntity { Id = id + 1, Name = second, Age = 2 },
        ]).Insert();

        var result = ctx.CreateBatchBuilder()
            .Update(ctx.CreateUpdateBuilder<IDeleteEntity>().Set(x => x.Name, updated).Where(x => x.Id == id))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Id == id + 1).Select(x => x.Name))
            .Execute();

        result.ResultSetCount.Should().Be(2);
        result.Read<string>().Should().Equal(updated);
        result.Read<string>().Should().Equal(second);
    }

    [Fact]
    public void Batch_SideEffect_ShouldExposeOneDriverRecordsAffected_NotTheResultSetCount()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        Assert.SkipUnless(Provider.SupportsAffectedRowMetadata, "This provider's driver does not report an affected-row total.");

        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var updated = "batch-ra-" + Guid.NewGuid().ToString("N");

        ctx.CreateInsertBuilder<IDeleteEntity>().Values([
            new DeleteEntity { Id = id, Name = "batch-ra-seed", Age = 1 },
        ]).Insert();

        var result = ctx.CreateBatchBuilder()
            .Update(ctx.CreateUpdateBuilder<IDeleteEntity>().Set(x => x.Name, updated).Where(x => x.Id == id))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Age))
            .Execute();

        // Two result sets, one updated row: the count is the driver's DML total, never the set count.
        result.ResultSetCount.Should().Be(2);
        result.RecordsAffected.Should().Be(1);

        result.Read<string>().Should().Equal(updated);
        result.Read<int>().Should().Equal(1);
    }

    [Fact]
    public async Task Batch_SideEffectAsync_ShouldExposeOneDriverRecordsAffected()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        Assert.SkipUnless(Provider.SupportsAffectedRowMetadata, "This provider's driver does not report an affected-row total.");

        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var updated = "batch-ra-async-" + Guid.NewGuid().ToString("N");

        ctx.CreateInsertBuilder<IDeleteEntity>().Values([
            new DeleteEntity { Id = id, Name = "batch-ra-async-seed", Age = 2 },
        ]).Insert();

        var result = await ctx.CreateBatchBuilder()
            .Update(ctx.CreateUpdateBuilder<IDeleteEntity>().Set(x => x.Name, updated).Where(x => x.Id == id))
            .AddQuery(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name))
            .ExecuteAsync(TestContext.Current.CancellationToken);

        result.RecordsAffected.Should().Be(1);
        result.Read<string>().Should().Equal(updated);
    }
}
