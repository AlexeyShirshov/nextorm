using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation and the in-memory key upsert. No database is involved.
/// </summary>
public class MergeBuilderTests
{
    [Fact]
    public void InMemoryContext_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var toSql = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        toSql.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_Merge_NewRow_ShouldInsert()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<ConventionalEntity>();
        ctx.Data[typeof(ConventionalEntity)] = rows;

        var affected = ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("a");
    }

    [Fact]
    public void InMemory_Merge_ExistingRow_ShouldUpdate()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<ConventionalEntity> { new() { Id = 1, Name = "old" } };
        ctx.Data[typeof(ConventionalEntity)] = rows;

        var affected = ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "new" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(1);
        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("new");
    }

    [Fact]
    public void InMemory_Merge_Batch_ShouldUpsertEachRow()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<ConventionalEntity> { new() { Id = 1, Name = "old" } };
        ctx.Data[typeof(ConventionalEntity)] = rows;

        var affected = ctx.MergeInto<ConventionalEntity>()
            .Using([
                new ConventionalEntity { Id = 1, Name = "new" },
                new ConventionalEntity { Id = 2, Name = "b" },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(2);
        rows.Should().HaveCount(2);
        rows.Single(r => r.Id == 1).Name.Should().Be("new");
        rows.Single(r => r.Id == 2).Name.Should().Be("b");
    }

    [Fact]
    public async Task InMemory_MergeAsync_ShouldUpsert()
    {
        using var ctx = new InMemoryDataContext();
        ctx.Data[typeof(ConventionalEntity)] = new List<ConventionalEntity>();

        var affected = await ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
    }

    [Fact]
    public void InMemory_FullMerge_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void InMemory_Merge_WithoutBranches_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .Merge();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullMerge_UpdateSelectorOnKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatched().ThenUpdate(x => new { x.Id, x.Name })
            .WhenNotMatched().ThenInsert();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void OnKeys_WithoutKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<NoKeyEntity>()
            .Using(new NoKeyEntity { Name = "a" })
            .OnKeys();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task MergeAsync_OnInMemory_ShouldInsert()
    {
        using var ctx = new InMemoryDataContext();
        var rows = new List<ConventionalEntity>();
        ctx.Data[typeof(ConventionalEntity)] = rows;

        await ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
    }

    [Fact]
    public void Using_EmptyBatch_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<ConventionalEntity>().Using([]);

        act.Should().Throw<ArgumentException>();
    }
}
