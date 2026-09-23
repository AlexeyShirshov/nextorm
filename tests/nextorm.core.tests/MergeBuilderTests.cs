using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation and the in-memory rejection of key upserts. No database is involved.
/// </summary>
public class MergeBuilderTests
{
    [Fact]
    public void InMemoryContext_ShouldRejectMerge()
    {
        using var ctx = new InMemoryDataContext();

        var toSql = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        var merge = () => ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        toSql.Should().Throw<NotSupportedException>();
        merge.Should().Throw<NotSupportedException>();
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
    public async Task MergeAsync_OnInMemory_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = async () => await ctx.MergeInto<ConventionalEntity>()
            .Using(new ConventionalEntity { Id = 1, Name = "a" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .MergeAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void Using_EmptyBatch_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.MergeInto<ConventionalEntity>().Using([]);

        act.Should().Throw<ArgumentException>();
    }
}
