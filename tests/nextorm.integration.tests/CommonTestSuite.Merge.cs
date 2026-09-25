using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static int MergeKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    [Fact]
    public void Merge_ShouldInsertWhenMissing()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var rows = ctx.From<IMergeEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
        rows[0].Age.Should().Be(1);
    }

    [Fact]
    public void Merge_ShouldUpdateWhenPresent()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var rows = ctx.From<IMergeEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
        rows[0].Age.Should().Be(2);
    }

    [Fact]
    public void Merge_Batch_ShouldInsertAndUpdate()
    {
        var ctx = _sut.DataProvider;
        var existingId = MergeKey();
        var newId = existingId + 1;
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = existingId, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using([
                new MergeEntity { Id = existingId, Name = marker, Age = 7 },
                new MergeEntity { Id = newId, Name = marker, Age = 8 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        var rows = ctx.From<IMergeEntity>()
            .Where(x => x.Id == existingId || x.Id == newId)
            .Select(x => new { x.Id, x.Name, x.Age })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Single(r => r.Id == existingId).Age.Should().Be(7);
        rows.Single(r => r.Id == newId).Age.Should().Be(8);
    }

    [Fact]
    public void Merge_ConditionalMatchedBranch_ShouldUpdateOnlyWhenConditionHolds()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();

        if (!((DataContext)ctx).Dialect.SupportsMergeConditionalBranches)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = id, Name = "x", Age = 1 })
                .OnKeys()
                .WhenMatched((t, s) => t.Age < s.Age).ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "keep", Age = 9 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = "low", Age = 5 })
            .OnKeys()
            .WhenMatched((t, s) => t.Age < s.Age).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var afterLow = ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        afterLow.Name.Should().Be("keep");
        afterLow.Age.Should().Be(9);

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = "high", Age = 20 })
            .OnKeys()
            .WhenMatched((t, s) => t.Age < s.Age).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var afterHigh = ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        afterHigh.Name.Should().Be("high");
        afterHigh.Age.Should().Be(20);
    }

    [Fact]
    public void Merge_OnCondition_ShouldMatchBySearchCondition()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();

        if (!((DataContext)ctx).Dialect.SupportsMergeConditionalBranches)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = id, Name = "x", Age = 1 })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = "new", Age = 2 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var row = ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        row.Name.Should().Be("new");
        row.Age.Should().Be(2);
    }

    [Fact]
    public void Merge_QuerySourceWithParameter_ShouldExecuteOnce()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();

        if (!((DataContext)ctx).Dialect.SupportsMergeStatement)
        {
            var sourceIdUnsupported = id;
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(ctx.From<IMergeEntity>().Where(x => x.Id == sourceIdUnsupported))
                .OnKeys()
                .WhenMatched().ThenUpdate()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "src", Age = 3 })
            .Insert();

        var sourceId = id;
        ctx.MergeInto<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>().Where(x => x.Id == sourceId))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        var row = ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        row.Name.Should().Be("src");
        row.Age.Should().Be(3);
    }

    [Fact]
    public void Merge_ExplicitColumns_ShouldUpdateAndInsert()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        if (!((DataContext)ctx).Dialect.SupportsMergeStatement)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = id, Name = "x", Age = 1 })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate(d => new { d.Name, d.Age })
                .WhenNotMatched().ThenInsert(d => new { d.Id, d.Name, d.Age })
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 9 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate(d => new { d.Name, d.Age })
            .WhenNotMatched().ThenInsert(d => new { d.Id, d.Name, d.Age })
            .Merge();

        var row = ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        row.Name.Should().Be(marker);
        row.Age.Should().Be(9);
    }

    [Fact]
    public void Merge_ThenDelete_ShouldRemoveMatchedRow()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();

        if (!((DataContext)ctx).Dialect.SupportsMergeDelete)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = id, Name = "x", Age = 1 })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenDelete()
                .WhenNotMatched().ThenInsert()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = id, Name = "del", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = "del", Age = 1 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenDelete()
            .WhenNotMatched().ThenInsert()
            .Merge();

        ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void Merge_WhenNotMatchedBySource_ShouldDeleteStaleTarget()
    {
        var ctx = _sut.DataProvider;
        var sourceId = MergeKey();
        var staleId = sourceId + 1;

        if (!((DataContext)ctx).Dialect.SupportsMergeBySourceDelete)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = sourceId, Name = "s", Age = 2 })
                .On((t, s) => t.Id == s.Id)
                .WhenMatched().ThenUpdate()
                .WhenNotMatchedBySource().ThenDelete()
                .Merge();
            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        ctx.InsertInto<IMergeEntity>().Values([
            new MergeEntity { Id = sourceId, Name = "old", Age = 1 },
            new MergeEntity { Id = staleId, Name = "stale", Age = 1 },
        ]).Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = sourceId, Name = "fresh", Age = 5 })
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatchedBySource().ThenDelete()
            .Merge();

        ctx.From<IMergeEntity>().Where(x => x.Id == staleId).Select(x => x.Id).ToList().Should().BeEmpty();
        ctx.From<IMergeEntity>().Where(x => x.Id == sourceId).Select(x => x.Name).Single().Should().Be("fresh");
    }

    [Fact]
    public async Task MergeAsync_ShouldUpsert()
    {
        var ctx = _sut.DataProvider;
        var id = MergeKey();
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        await ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 4 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken);

        ctx.From<IMergeEntity>().Where(x => x.Id == id).Select(x => x.Name).Single().Should().Be(marker);
    }

    [Fact]
    public void Merge_ToSql_ShouldRenderMerge()
    {
        var sql = _sut.DataProvider.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = MergeKey(), Name = "x", Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().ContainEquivalentOf("insert");
    }

    [Fact]
    public void Merge_Returning_ShouldReturnWrittenRow()
    {
        var ctx = _sut.DataProvider;
        var dialect = ((DataContext)ctx).Dialect;
        var id = MergeKey();
        var marker = "mrg_" + Guid.NewGuid().ToString("N");

        if (!dialect.SupportsReturning && !dialect.SupportsOutput)
        {
            var unsupported = () => ctx.MergeInto<IMergeEntity>()
                .Using(new MergeEntity { Id = id, Name = "x", Age = 1 })
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Returning(x => new { x.Id, x.Name })
                .ToList();

            unsupported.Should().Throw<NotSupportedException>();
            return;
        }

        var inserted = ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = marker, Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToList();

        inserted.Should().ContainSingle();
        inserted[0].Id.Should().Be(id);
        inserted[0].Name.Should().Be(marker);

        var updatedName = marker + "_2";
        var updated = ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = id, Name = updatedName, Age = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToList();

        updated.Should().ContainSingle();
        updated[0].Id.Should().Be(id);
        updated[0].Name.Should().Be(updatedName);
    }
}
