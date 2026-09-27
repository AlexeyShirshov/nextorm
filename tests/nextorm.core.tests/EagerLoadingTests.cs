using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focal coverage for split-query eager loading (<c>LoadWith</c>, #95) on the in-memory provider:
/// two-round-trip loading, per-parent grouping and order, assignment into null/settable and read-only
/// collections, the async terminal, and the <c>ToCommand</c> boundary.
/// </summary>
public class EagerLoadingTests
{
    public sealed class ParentEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public ICollection<ChildEntity> Children { get; set; } = new List<ChildEntity>();
    }

    public sealed class NullCollectionParent
    {
        public int Id { get; set; }
        public ICollection<ChildEntity> Children { get; set; } = null!;
    }

    public sealed class ReadOnlyParent
    {
        public int Id { get; set; }
        public ICollection<ChildEntity> Children { get; } = null!;
    }

    public sealed class PartiallyReadOnlyParent
    {
        public PartiallyReadOnlyParent(ICollection<ChildEntity>? children) => Children = children!;

        public int Id { get; set; }
        public ICollection<ChildEntity> Children { get; }
    }

    public sealed class NullableKeyParent
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public ICollection<NullableKeyChild> Children { get; set; } = new List<NullableKeyChild>();
    }

    public sealed class NullableKeyChild
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
    }

    public sealed class ChildEntity
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<ChildEntity>().WithData(new[]
        {
            new ChildEntity { Id = 10, ParentId = 1 },
            new ChildEntity { Id = 11, ParentId = 1 },
            new ChildEntity { Id = 12, ParentId = 2 },
            new ChildEntity { Id = 13, ParentId = 99 },
        });
        return context;
    }

    [Fact]
    public void LoadWith_ToList_FillsChildCollectionsInParentAndChildOrder()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[]
        {
            new ParentEntity { Id = 1, Name = "a" },
            new ParentEntity { Id = 2, Name = "b" },
            new ParentEntity { Id = 3, Name = "c" },
        });

        var parents = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void LoadWith_DuplicateParentKeys_ShareTheSameChildren()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[]
        {
            new ParentEntity { Id = 1 },
            new ParentEntity { Id = 1 },
        });

        var parents = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Should().OnlyContain(p => p.Children.Count == 2);
    }

    [Fact]
    public void LoadWith_AssignsFreshList_WhenCollectionIsNull()
    {
        var context = CreateContext();
        context.From<NullCollectionParent>().WithData(new[] { new NullCollectionParent { Id = 2 } });

        var parents = context.From<NullCollectionParent>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().ContainSingle(c => c.Id == 12);
    }

    [Fact]
    public void LoadWith_ReadOnlyNullCollection_Throws()
    {
        var context = CreateContext();
        context.From<ReadOnlyParent>().WithData(new[] { new ReadOnlyParent { Id = 1 } });

        var act = () => context.From<ReadOnlyParent>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*not settable*");
    }

    [Fact]
    public async Task LoadWith_ToListAsync_FillsChildCollections()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        var parents = await context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToListAsync();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public void LoadWith_ToCommand_IgnoresTheLoadSpec()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        var parents = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToCommand()
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void LoadWith_DuplicateCollectionMember_Throws()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        var builder = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId);

        var act = () => builder.LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already has an eager-load declaration*");
    }

    [Fact]
    public void AddChildren_NullChildKey_IsIgnoredAndDoesNotCrash()
    {
        var spec = new EagerLoadSpec<NullableKeyParent, NullableKeyChild, string>(
            p => p.Children, c => c.From<NullableKeyChild>(), p => p.Key, c => c.Key);
        var grouped = new Dictionary<string, List<NullableKeyChild>>();
        var children = new List<NullableKeyChild>
        {
            new() { Id = 1, Key = null! },
            new() { Id = 2, Key = "a" },
        };

        spec.AddChildren(grouped, children);

        grouped.Should().HaveCount(1);
        grouped["a"].Should().ContainSingle(c => c.Id == 2);
    }

    [Fact]
    public void Execute_ReadOnlyNullTarget_ThrowsBeforeMutatingEarlierParents()
    {
        var context = CreateContext();
        var spec = new EagerLoadSpec<PartiallyReadOnlyParent, ChildEntity, int>(
            p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId);
        var sentinel = new ChildEntity { Id = 999, ParentId = 1 };
        var filled = new PartiallyReadOnlyParent(new List<ChildEntity> { sentinel }) { Id = 1 };
        var broken = new PartiallyReadOnlyParent(null) { Id = 2 };
        IReadOnlyList<PartiallyReadOnlyParent> parents = new[] { filled, broken };

        var act = () => ((IEagerLoadSpec<PartiallyReadOnlyParent>)spec).Execute(context, parents);

        act.Should().Throw<NotSupportedException>().WithMessage("*not settable*");
        filled.Children.Should().ContainSingle().Which.Should().BeSameAs(sentinel);
    }
}
