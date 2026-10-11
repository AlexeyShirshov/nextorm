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
    public async Task LoadWith_SplitAsync_PreCancelled_ThrowsWithoutLoadingChildren()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToListAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void LoadWith_Split_ToArray_StitchesChildren()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        var parents = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToArray();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public async Task LoadWith_Split_ToArrayAsync_StitchesChildren()
    {
        var context = CreateContext();
        context.From<ParentEntity>().WithData(new[] { new ParentEntity { Id = 1 } });

        var parents = await context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToArrayAsync();

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

        var act = () => ((IEagerLoadSpec<PartiallyReadOnlyParent>)spec).Execute(context, QueryFilterScope.None, parents);

        act.Should().Throw<NotSupportedException>().WithMessage("*not settable*");
        filled.Children.Should().ContainSingle().Which.Should().BeSameAs(sentinel);
    }

    // --- #209: value/string key linkage, the no-eager-load command contract, and the single-query
    // --- child-shape guard (V06/V08/V09).

    [Fact]
    public void LoadWith_StringKeys_LinkByValueAndSkipNullKeys()
    {
        var context = new InMemoryDataContext();
        // Distinct string instances with equal values prove the linkage is value-based, not reference-based.
        var keyA = new string("a".ToCharArray());
        var keyB = new string("b".ToCharArray());
        context.From<NullableKeyChild>().WithData(new[]
        {
            new NullableKeyChild { Id = 1, Key = new string("a".ToCharArray()) },
            new NullableKeyChild { Id = 2, Key = null! },
            new NullableKeyChild { Id = 3, Key = new string("b".ToCharArray()) },
        });
        context.From<NullableKeyParent>().WithData(new[]
        {
            new NullableKeyParent { Id = 1, Key = keyA },
            new NullableKeyParent { Id = 2, Key = keyB },
        });

        var parents = context.From<NullableKeyParent>()
            .LoadWith(p => p.Children, c => c.From<NullableKeyChild>(), p => p.Key, c => c.Key)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Children.Select(c => c.Id).Should().Equal(1);
        parents[1].Children.Select(c => c.Id).Should().Equal(3);
        parents.SelectMany(p => p.Children).Should().NotContain(c => c.Id == 2, "a null child key never matches a parent key");
    }

    [Fact]
    public void NoEagerLoadDeclaration_CarriesNoStrategyOrShape()
    {
        var context = CreateContext();

        var cmd = context.From<ParentEntity>().ToCommand();

        cmd.EagerLoadMode.Should().BeNull("a command with no eager loads has no strategy override");
        cmd.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.Default, "no eager loads means no resolved strategy");
        cmd.EagerLoadShape.IsEmpty.Should().BeTrue("no eager loads means the empty shape");
    }

    [Fact]
    public void OneLoad_CarriesANonEmptyStrategyAndShape()
    {
        var context = CreateContext();

        var cmd = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .ToCommand();

        cmd.EagerLoadMode.Should().BeNull("an omitted mode inherits the context default, so the override stays null");
        cmd.EagerLoadShape.IsEmpty.Should().BeFalse("a declared load populates the structural shape");
    }

    [Fact]
    public void Split_EmptyCollectionAndMissingOptionalChild_AreMaterializedAsEmpty()
    {
        var context = new InMemoryDataContext();
        // Child 13 belongs to parent 99 (an orphan): no parent matches it.
        context.From<ChildEntity>().WithData(new[]
        {
            new ChildEntity { Id = 10, ParentId = 1 },
            new ChildEntity { Id = 13, ParentId = 99 },
        });
        context.From<ParentEntity>().WithData(new[]
        {
            new ParentEntity { Id = 1 },
            new ParentEntity { Id = 2 },
        });

        var parents = context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>(), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(10);
        parents[1].Children.Should().BeEmpty("a parent with no matching child keeps an empty collection");
        parents.SelectMany(p => p.Children).Should().NotContain(c => c.Id == 13, "an orphan child is never attached to an unrelated parent");
    }

    [Fact]
    public void SingleQuery_UnsupportedChildModifier_IsRejectedAsAGuard()
    {
        var context = CreateContext();

        var act = () => context.From<ParentEntity>()
            .LoadWith(p => p.Children, c => c.From<ChildEntity>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*");
    }
}
