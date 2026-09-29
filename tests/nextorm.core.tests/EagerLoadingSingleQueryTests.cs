using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Single-query eager loading (<c>AsSingleQuery</c>, #107) on the in-memory provider: the parents and
/// every declared collection are fetched by one denormalized command and stitched; the child's own
/// <c>Where</c> is folded into the join, the mode survives copies, the child query is built exactly once
/// even beyond the split chunk size, and cancellation is honored.
/// </summary>
public class EagerLoadingSingleQueryTests
{
    public sealed class SingleQueryParent
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public ICollection<SingleQueryChild> Children { get; set; } = new List<SingleQueryChild>();
        public ICollection<SingleQueryNote> Notes { get; set; } = new List<SingleQueryNote>();
    }

    public sealed class SingleQueryChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool Active { get; set; }
    }

    public sealed class SingleQueryNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<SingleQueryChild>().WithData(new[]
        {
            new SingleQueryChild { Id = 10, ParentId = 1, Active = true },
            new SingleQueryChild { Id = 11, ParentId = 1, Active = false },
            new SingleQueryChild { Id = 12, ParentId = 2, Active = true },
            new SingleQueryChild { Id = 13, ParentId = 99, Active = true },
        });
        context.From<SingleQueryNote>().WithData(new[]
        {
            new SingleQueryNote { Id = 100, ParentId = 1 },
            new SingleQueryNote { Id = 101, ParentId = 2 },
        });

        return context;
    }

    [Fact]
    public void AsSingleQuery_ToList_FillsChildrenAndAppliesChildFilter()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[]
        {
            new SingleQueryParent { Id = 1, Name = "a" },
            new SingleQueryParent { Id = 2, Name = "b" },
            new SingleQueryParent { Id = 3, Name = "c" },
        });

        var parents = context.From<SingleQueryParent>()
            .LoadWith(p => p.Children, c => c.From<SingleQueryChild>().Where(x => x.Active), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        // If the child filter were dropped the inactive child 11 would reappear here (control).
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void AsSingleQuery_MultipleSpecs_GroupEachCollectionWithoutDuplicateParents()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[]
        {
            new SingleQueryParent { Id = 1 },
            new SingleQueryParent { Id = 2 },
            new SingleQueryParent { Id = 3 },
        });

        var parents = context.From<SingleQueryParent>()
            .LoadWith(p => p.Children, c => c.From<SingleQueryChild>(), p => p.Id, c => c.ParentId)
            .LoadWith(p => p.Notes, c => c.From<SingleQueryNote>(), p => p.Id, n => n.ParentId)
            .AsSingleQuery()
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(100);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[1].Notes.Select(n => n.Id).Should().Equal(101);
        parents[2].Children.Should().BeEmpty();
        parents[2].Notes.Should().BeEmpty();
    }

    [Fact]
    public void AsSingleQuery_BeyondChunkSize_BuildsTheChildQueryOnce()
    {
        var context = new InMemoryDataContext();
        var parentData = new List<SingleQueryParent>();
        var childData = new List<SingleQueryChild>();
        for (var i = 1; i <= 1001; i++)
        {
            parentData.Add(new SingleQueryParent { Id = i });
            childData.Add(new SingleQueryChild { Id = i * 10, ParentId = i, Active = true });
        }

        context.From<SingleQueryParent>().WithData(parentData);
        context.From<SingleQueryChild>().WithData(childData);

        var childQueryCalls = 0;
        var parents = context.From<SingleQueryParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryCalls++;
                    return c.From<SingleQueryChild>();
                },
                p => p.Id,
                c => c.ParentId)
            .AsSingleQuery()
            .OrderBy(p => p.Id)
            .ToList();

        // Split mode would chunk 1001 distinct keys into two child queries; single-query builds the child
        // source once and fetches everything by the one denormalized command (control: must be 1, not 2).
        childQueryCalls.Should().Be(1);
        parents.Should().HaveCount(1001);
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        parents[^1].Children.Single().Id.Should().Be(10010);
    }

    [Fact]
    public void AsSingleQuery_SurvivesACopyAndAFollowingModifier()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[] { new SingleQueryParent { Id = 1 } });

        var parents = context.From<SingleQueryParent>()
            .AsSingleQuery()
            .LoadWith(p => p.Children, c => c.From<SingleQueryChild>(), p => p.Id, c => c.ParentId)
            .Clone()
            .Where(p => p.Id == 1)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public void AsSingleQuery_ToArray_StitchesChildren()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[] { new SingleQueryParent { Id = 1 } });

        var parents = context.From<SingleQueryParent>()
            .LoadWith(p => p.Children, c => c.From<SingleQueryChild>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToArray();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public async Task AsSingleQuery_ToArrayAsync_StitchesChildren()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[] { new SingleQueryParent { Id = 1 } });

        var parents = await context.From<SingleQueryParent>()
            .LoadWith(p => p.Children, c => c.From<SingleQueryChild>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToArrayAsync();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public async Task AsSingleQuery_PreCancelled_DoesNotExecute()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[] { new SingleQueryParent { Id = 1 } });

        var childQueryCalls = 0;
        var builder = context.From<SingleQueryParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryCalls++;
                    return c.From<SingleQueryChild>();
                },
                p => p.Id,
                c => c.ParentId)
            .AsSingleQuery();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await builder.ToListAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        childQueryCalls.Should().Be(0, "a pre-cancelled single-query command must not be executed");
    }

    [Fact]
    public void AsSingleQuery_NonStitchingTerminals_DoNotLoadChildren()
    {
        using var context = CreateContext();
        context.From<SingleQueryParent>().WithData(new[] { new SingleQueryParent { Id = 1 } });

        var childQueryCalls = 0;
        var builder = context.From<SingleQueryParent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryCalls++;
                    return c.From<SingleQueryChild>();
                },
                p => p.Id,
                c => c.ParentId)
            .AsSingleQuery();

        builder.Count().Should().Be(1);
        builder.Any().Should().BeTrue();
        builder.First().Children.Should().BeEmpty();
        builder.ToHashSet().Single().Children.Should().BeEmpty();
        childQueryCalls.Should().Be(0, "scalar and non-stitching terminals must not issue load queries");
    }
}
