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
}
