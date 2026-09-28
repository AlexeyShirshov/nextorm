using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Regression coverage for the single-query (<c>AsSingleQuery</c>) eager-load P1s: unsupported child
/// query shapes are rejected instead of silently dropped, parent/child <c>IgnoreFilters</c> decisions
/// are independent (split semantics), compositions that cannot carry the loader are rejected, a keyless
/// parent is rejected with guidance (split still works) and a key type without an equality operator is
/// rejected. Duplicate keys/children and mode preservation are pinned here too.
/// </summary>
public class EagerLoadingSingleQueryP1Tests
{
    public sealed class P1Parent
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public ICollection<P1Child> Children { get; set; } = new List<P1Child>();
    }

    public sealed class P1Child
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool Active { get; set; }
    }

    public sealed class P1Note
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class KeylessParent
    {
        public string Name { get; set; } = "";
        public ICollection<KeylessChild> Children { get; set; } = new List<KeylessChild>();
    }

    public sealed class KeylessChild
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public readonly struct NoEqKey
    {
        public int Value { get; init; }
    }

    public sealed class NoEqParent
    {
        public NoEqKey Key { get; init; }
        public ICollection<NoEqChild> Children { get; set; } = new List<NoEqChild>();
    }

    public sealed class NoEqChild
    {
        public NoEqKey Key { get; init; }
    }

    public sealed class NoEqRefKey
    {
        public int Value { get; init; }
    }

    public sealed class NoEqRefParent
    {
        public NoEqRefKey Key { get; init; } = new();
        public ICollection<NoEqRefChild> Children { get; set; } = new List<NoEqRefChild>();
    }

    public sealed class NoEqRefChild
    {
        public NoEqRefKey Key { get; init; } = new();
    }

    public sealed class FilteredP1Parent
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public ICollection<FilteredP1Child> Children { get; set; } = new List<FilteredP1Child>();
        public ICollection<FilteredP1Note> Notes { get; set; } = new List<FilteredP1Note>();
    }

    public sealed class FilteredP1Child
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool IsDeleted { get; set; }
    }

    public sealed class FilteredP1Note
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool IsDeleted { get; set; }
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<P1Child>().WithData(new[]
        {
            new P1Child { Id = 10, ParentId = 1 },
            new P1Child { Id = 11, ParentId = 1 },
            new P1Child { Id = 12, ParentId = 2 },
        });
        context.From<P1Note>().WithData(new[]
        {
            new P1Note { Id = 100, ParentId = 1 },
        });
        context.From<P1Parent>().WithData(new[]
        {
            new P1Parent { Id = 1, Name = "a" },
            new P1Parent { Id = 2, Name = "b" },
            new P1Parent { Id = 3, Name = "c" },
        });

        return context;
    }

    [Fact]
    public void AsSingleQuery_ChildOrderBy_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*OrderBy*").WithMessage("*split-query*");
    }

    [Fact]
    public void AsSingleQuery_ChildLimit_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Limit(1), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
    }

    [Fact]
    public void AsSingleQuery_ChildDistinct_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Distinct(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Distinct*");
    }

    [Fact]
    public void SplitQuery_ChildOrderByIsHonored()
    {
        using var context = CreateContext();

        var parents = context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().OrderByDescending(x => x.Id), p => p.Id, c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 11, 10 }, "split honors the child OrderBy");
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
    }

    [Fact]
    public void SingleQuery_ModeAndLoadSpecs_ArePreservedAcrossCopyAndModifier()
    {
        using var context = CreateContext();

        var builder = context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery();

        builder.SingleQuery.Should().BeTrue();
        builder.LoadSpecs.Should().HaveCount(1);

        var copy = builder.Clone().Where(p => p.Id == 1);

        copy.SingleQuery.Should().BeTrue("the single-query mode must survive a copy");
        copy.LoadSpecs.Should().HaveCount(1, "the load specification must survive a copy");

        var parents = copy.ToList();
        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public void LoadWith_ThenSelect_IsRejectedInsteadOfDroppingTheLoader()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .Select(p => p.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Select*");
    }

    [Fact]
    public void LoadWith_ThenJoin_IsRejectedInsteadOfDroppingTheLoader()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .Join(context.From<P1Note>(), (p, n) => p.Id == n.ParentId)
            .Select(p => p.Item1.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Join*");
    }

    [Fact]
    public void AsSingleQuery_ThenAs_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .AsSingleQuery()
            .As(p => new { p.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*AsSingleQuery*As*");
    }

    [Fact]
    public void AsSingleQuery_ThenJoin_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .AsSingleQuery()
            .Join(context.From<P1Note>(), (p, n) => p.Id == n.ParentId)
            .Select(p => p.Item1.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*AsSingleQuery*Join*");
    }

    [Fact]
    public void AsSingleQuery_KeylessParent_IsRejectedWithGuidance()
    {
        using var context = new InMemoryDataContext();
        context.From<KeylessChild>().WithData(new[]
        {
            new KeylessChild { Id = 1, Name = "a" },
            new KeylessChild { Id = 2, Name = "a" },
        });
        context.From<KeylessParent>().WithData(new[] { new KeylessParent { Name = "a" } });

        var act = () => context.From<KeylessParent>()
            .LoadWith(p => p.Children, c => c.From<KeylessChild>(), p => p.Name, c => c.Name)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*mapped key*")
            .WithMessage("*split-query*");
    }

    [Fact]
    public void SplitQuery_KeylessParent_StillWorks()
    {
        using var context = new InMemoryDataContext();
        context.From<KeylessChild>().WithData(new[]
        {
            new KeylessChild { Id = 1, Name = "a" },
            new KeylessChild { Id = 2, Name = "a" },
            new KeylessChild { Id = 3, Name = "z" },
        });
        context.From<KeylessParent>().WithData(new[] { new KeylessParent { Name = "a" } });

        var parents = context.From<KeylessParent>()
            .LoadWith(p => p.Children, c => c.From<KeylessChild>(), p => p.Name, c => c.Name)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void AsSingleQuery_KeyWithoutEqualityOperator_IsRejected()
    {
        using var context = new InMemoryDataContext();
        context.From<NoEqChild>().WithData(new[]
        {
            new NoEqChild { Key = new NoEqKey { Value = 1 } },
        });
        context.From<NoEqParent>().WithData(new[] { new NoEqParent { Key = new NoEqKey { Value = 1 } } });

        var act = () => context.From<NoEqParent>()
            .LoadWith(p => p.Children, c => c.From<NoEqChild>(), p => p.Key, c => c.Key)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(NoEqKey)}*")
            .WithMessage("*equality operator*");
    }

    [Fact]
    public void SplitQuery_KeyWithoutEqualityOperator_StillWorks()
    {
        using var context = new InMemoryDataContext();
        context.From<NoEqChild>().WithData(new[]
        {
            new NoEqChild { Key = new NoEqKey { Value = 1 } },
            new NoEqChild { Key = new NoEqKey { Value = 2 } },
        });
        context.From<NoEqParent>().WithData(new[] { new NoEqParent { Key = new NoEqKey { Value = 1 } } });

        var parents = context.From<NoEqParent>()
            .LoadWith(p => p.Children, c => c.From<NoEqChild>(), p => p.Key, c => c.Key)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().HaveCount(1);
    }

    [Fact]
    public void AsSingleQuery_ParentIgnoreFilters_KeepsChildGlobalFilters()
    {
        using var context = new InMemoryDataContext();
        context.From<FilteredP1Child>(b => b.HasQueryFilter(c => !c.IsDeleted)).WithData(new[]
        {
            new FilteredP1Child { Id = 10, ParentId = 1 },
            new FilteredP1Child { Id = 11, ParentId = 1, IsDeleted = true },
            new FilteredP1Child { Id = 12, ParentId = 2 },
        });
        context.From<FilteredP1Parent>(b => b.HasQueryFilter(p => !p.IsDeleted)).WithData(new[]
        {
            new FilteredP1Parent { Id = 1 },
            new FilteredP1Parent { Id = 2, IsDeleted = true },
        });

        var parents = context.From<FilteredP1Parent>()
            .LoadWith(p => p.Children, c => c.From<FilteredP1Child>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .IgnoreFilters()
            .OrderBy(p => p.Id)
            .ToList();

        // Parent filter disabled: the deleted parent reappears. Child filter still applies: child 11 stays out.
        parents.Select(p => p.Id).Should().Equal(1, 2);
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
        parents[1].Children.Select(c => c.Id).Should().Equal(12);
    }

    [Fact]
    public void AsSingleQuery_ChildIgnoreFilters_KeepsParentGlobalFilters()
    {
        using var context = new InMemoryDataContext();
        context.From<FilteredP1Child>(b => b.HasQueryFilter(c => !c.IsDeleted)).WithData(new[]
        {
            new FilteredP1Child { Id = 10, ParentId = 1 },
            new FilteredP1Child { Id = 11, ParentId = 1, IsDeleted = true },
            new FilteredP1Child { Id = 12, ParentId = 2 },
        });
        context.From<FilteredP1Parent>(b => b.HasQueryFilter(p => !p.IsDeleted)).WithData(new[]
        {
            new FilteredP1Parent { Id = 1 },
            new FilteredP1Parent { Id = 2, IsDeleted = true },
        });

        var parents = context.From<FilteredP1Parent>()
            .LoadWith(p => p.Children, c => c.From<FilteredP1Child>().IgnoreFilters(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .OrderBy(p => p.Id)
            .ToList();

        // Parent filter still applies: only parent 1. Child filter disabled: the deleted child reappears.
        parents.Select(p => p.Id).Should().Equal(1);
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public void AsSingleQuery_DuplicateParentKeys_GroupChildrenPerMaterializedParent()
    {
        using var context = new InMemoryDataContext();
        context.From<P1Child>().WithData(new[]
        {
            new P1Child { Id = 10, ParentId = 1 },
            new P1Child { Id = 11, ParentId = 1 },
        });
        context.From<P1Parent>().WithData(new[]
        {
            new P1Parent { Id = 1 },
            new P1Parent { Id = 1 },
        });

        var parents = context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        // Deduplication is by the mapped parent key, so two rows with the same key collapse to one parent
        // that owns the full collection (the denormalized rows cannot distinguish the duplicate rows).
        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10, 11);
    }

    [Fact]
    public async Task SplitQuery_CancelledBetweenChunks_StopsBeforeTheSecondChildQuery()
    {
        var context = new InMemoryDataContext();
        var parentData = new List<P1Parent>();
        var childData = new List<P1Child>();
        for (var i = 1; i <= 1001; i++)
        {
            parentData.Add(new P1Parent { Id = i });
            childData.Add(new P1Child { Id = i * 10, ParentId = i });
        }

        context.From<P1Parent>().WithData(parentData);
        context.From<P1Child>().WithData(childData);

        using var cts = new CancellationTokenSource();
        var childQueryCalls = 0;
        var builder = context.From<P1Parent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryCalls++;
                    cts.Cancel();
                    return c.From<P1Child>();
                },
                p => p.Id,
                c => c.ParentId);

        var act = () => builder.ToListAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        childQueryCalls.Should().Be(1, "cancellation between chunks must not start the second child query");
    }

    [Fact]
    public async Task SingleQuery_CancelledWhileBuilding_DoesNotExecuteTheCommand()
    {
        using var context = CreateContext();
        using var cts = new CancellationTokenSource();
        var builder = context.From<P1Parent>()
            .LoadWith(
                p => p.Children,
                c =>
                {
                    cts.Cancel();
                    return c.From<P1Child>();
                },
                p => p.Id,
                c => c.ParentId)
            .AsSingleQuery();

        var act = () => builder.ToListAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SingleQuery_AllNonStitchingTerminals_DoNotLoadChildren()
    {
        using var context = CreateContext();
        var childQueryCalls = 0;
        var builder = context.From<P1Parent>()
            .Where(p => p.Id == 1)
            .LoadWith(
                p => p.Children,
                c =>
                {
                    childQueryCalls++;
                    return c.From<P1Child>();
                },
                p => p.Id,
                c => c.ParentId)
            .AsSingleQuery();

        builder.ToEnumerable().ToList().Should().ContainSingle().Which.Children.Should().BeEmpty();
        builder.ToDictionary(p => p.Id).Should().ContainKey(1);
        builder.Single().Children.Should().BeEmpty();
        builder.SingleOrDefault()!.Children.Should().BeEmpty();
        builder.ToHashSet().Single().Children.Should().BeEmpty();

        var streamed = new List<P1Parent>();
        await foreach (var parent in builder.ToAsyncEnumerable())
            streamed.Add(parent);

        streamed.Should().ContainSingle().Which.Children.Should().BeEmpty();
        childQueryCalls.Should().Be(0, "non-stitching terminals must not issue eager-load queries");
    }

    [Fact]
    public void AsSingleQuery_RepeatedChildRows_AreDeduplicated()
    {
        using var context = new InMemoryDataContext();
        context.From<P1Child>().WithData(new[]
        {
            new P1Child { Id = 10, ParentId = 1 },
            new P1Child { Id = 10, ParentId = 1 },
        });
        context.From<P1Parent>().WithData(new[] { new P1Parent { Id = 1 } });

        var parents = context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().ContainSingle().Which.Id.Should().Be(10);
    }

    [Fact]
    public void JoinInto_KeylessParent_UsesReferenceFallback()
    {
        using var context = new InMemoryDataContext();
        context.From<KeylessChild>().WithData(new[]
        {
            new KeylessChild { Id = 1, Name = "a" },
            new KeylessChild { Id = 2, Name = "a" },
        });
        context.From<KeylessParent>().WithData(new[] { new KeylessParent { Name = "a" } });

        var parents = context.From<KeylessParent>()
            .JoinInto(context.From<KeylessChild>(), (p, c) => p.Name == c.Name, p => p.Children, p => p.Name, c => c.Name)
            .ToList();

        parents.Should().ContainSingle("plain JoinInto keeps the #105 reference-identity fallback");
        parents[0].Children.Select(c => c.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void AsSingleQuery_KeylessParent_EmptyResult_IsRejected()
    {
        using var context = new InMemoryDataContext();
        context.From<KeylessChild>().WithData(Array.Empty<KeylessChild>());
        context.From<KeylessParent>().WithData(Array.Empty<KeylessParent>());

        var act = () => context.From<KeylessParent>()
            .LoadWith(p => p.Children, c => c.From<KeylessChild>(), p => p.Name, c => c.Name)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*mapped key*")
            .WithMessage("*split-query*");
    }

    [Fact]
    public void AsSingleQuery_ReferenceKeyWithoutEqualityOperator_IsRejected()
    {
        using var context = new InMemoryDataContext();
        context.From<NoEqRefChild>().WithData(new[] { new NoEqRefChild { Key = new NoEqRefKey { Value = 1 } } });
        context.From<NoEqRefParent>().WithData(new[] { new NoEqRefParent { Key = new NoEqRefKey { Value = 1 } } });

        var act = () => context.From<NoEqRefParent>()
            .LoadWith(p => p.Children, c => c.From<NoEqRefChild>(), p => p.Key, c => c.Key)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(NoEqRefKey)}*")
            .WithMessage("*equality operator*");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void AsSingleQuery_TwoSpecs_PerSideIgnoreFilters(bool parentIgnore, bool childIgnore, bool noteIgnore)
    {
        using var context = new InMemoryDataContext();
        context.From<FilteredP1Child>(b => b.HasQueryFilter(c => !c.IsDeleted)).WithData(new[]
        {
            new FilteredP1Child { Id = 10, ParentId = 1 },
            new FilteredP1Child { Id = 11, ParentId = 1, IsDeleted = true },
            new FilteredP1Child { Id = 12, ParentId = 2 },
        });
        context.From<FilteredP1Note>(b => b.HasQueryFilter(n => !n.IsDeleted)).WithData(new[]
        {
            new FilteredP1Note { Id = 100, ParentId = 1 },
            new FilteredP1Note { Id = 101, ParentId = 1, IsDeleted = true },
            new FilteredP1Note { Id = 102, ParentId = 2 },
        });
        context.From<FilteredP1Parent>(b => b.HasQueryFilter(p => !p.IsDeleted)).WithData(new[]
        {
            new FilteredP1Parent { Id = 1 },
            new FilteredP1Parent { Id = 2, IsDeleted = true },
        });

        var builder = context.From<FilteredP1Parent>()
            .LoadWith(
                p => p.Children,
                c => childIgnore ? c.From<FilteredP1Child>().IgnoreFilters() : c.From<FilteredP1Child>(),
                p => p.Id,
                c => c.ParentId)
            .LoadWith(
                p => p.Notes,
                c => noteIgnore ? c.From<FilteredP1Note>().IgnoreFilters() : c.From<FilteredP1Note>(),
                p => p.Id,
                n => n.ParentId)
            .AsSingleQuery();

        if (parentIgnore)
            builder = builder.IgnoreFilters();

        var parents = builder.OrderBy(p => p.Id).ToList();

        parents.Select(p => p.Id).Should().Equal(parentIgnore ? new[] { 1, 2 } : new[] { 1 });
        parents[0].Children.Select(c => c.Id).Should().Equal(childIgnore ? new[] { 10, 11 } : new[] { 10 });
        parents[0].Notes.Select(n => n.Id).Should().Equal(noteIgnore ? new[] { 100, 101 } : new[] { 100 });
    }

    [Fact]
    public void AsSingleQuery_ChildJoin_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().SemiJoin(c.From<P1Note>(), (x, n) => x.Id == n.ParentId), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Join*").WithMessage("*split-query*");
    }

    [Fact]
    public void AsSingleQuery_ChildPreWhere_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().PreWhere(x => x.Id > 0), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's PreWhere*");
    }

    [Fact]
    public void AsSingleQuery_ChildArrayJoin_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().ArrayJoin(x => new[] { x.Id }), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's ArrayJoin*");
    }

    [Fact]
    public void AsSingleQuery_ChildFinal_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Final(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Final*");
    }

    [Fact]
    public void AsSingleQuery_ChildWindow_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Window("w"), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Window*");
    }

    [Fact]
    public void AsSingleQuery_ChildSettings_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Settings(("max_threads", "2")), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Settings*");
    }

    [Fact]
    public void AsSingleQuery_ChildRowLock_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().ForUpdate(), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's ForUpdate/ForShare*");
    }

    [Fact]
    public void AsSingleQuery_ChildTableHint_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().WithTableHint("nolock"), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's TableHint*");
    }

    [Fact]
    public void AsSingleQuery_ChildTableSample_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(o => o.TableSample(10)), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's TableSample*");
    }

    [Fact]
    public void AsSingleQuery_ChildSample_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(o => o.Sample(0.5)), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Sample*");
    }

    [Fact]
    public void AsSingleQuery_ChildForSystemTime_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().ForSystemTime(TemporalClause.All()), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's ForSystemTime*");
    }

    [Fact]
    public void AsSingleQuery_ChildDerivedSource_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From(c.From<P1Child>().Where(x => x.Active).ToCommand()), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's derived (As) source*");
    }

    [Fact]
    public void ArrayJoinElement_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .ArrayJoinElement(p => new[] { p.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*ArrayJoinElement*");
    }

    [Fact]
    public void LeftArrayJoinElement_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .LeftArrayJoinElement(p => new[] { p.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*LeftArrayJoinElement*");
    }

    [Fact]
    public void Pivot_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .Pivot(PivotAggregate.Sum, p => p.Id, p => p.Name, PivotValue.Create("x"));

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Pivot*");
    }

    [Fact]
    public void Unpivot_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .Unpivot("v", "n", UnpivotColumn.Create("Name"));

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Unpivot*");
    }

    [Fact]
    public void SelectMany_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .SelectMany(p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*SelectMany*");
    }

    [Fact]
    public void GroupJoin_AfterLoadWith_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>(), p => p.Id, c => c.ParentId)
            .GroupJoin(context.From<P1Note>(), p => p.Id, n => n.ParentId, (p, notes) => p);

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*GroupJoin*");
    }

    [Fact]
    public void AsSingleQuery_ChildGroupBy_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().GroupBy(x => x.ParentId), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's GroupBy*");
    }

    [Fact]
    public void AsSingleQuery_ChildHaving_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().Having(x => x.ParentId > 0), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's Having*");
    }

    [Fact]
    public void AsSingleQuery_ChildDistinctOn_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().DistinctOn(x => x.ParentId), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's DistinctOn*");
    }

    [Fact]
    public void AsSingleQuery_ChildLimitBy_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().LimitBy(2, x => x.ParentId), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's LimitBy*");
    }

    [Fact]
    public void AsSingleQuery_ChildIndexHint_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().WithIndex("ix_child"), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's IndexHint*");
    }

    [Fact]
    public void AsSingleQuery_ChildTablesInScopeHint_IsRejected()
    {
        using var context = CreateContext();

        var act = () => context.From<P1Parent>()
            .LoadWith(p => p.Children, c => c.From<P1Child>().WithTablesInScopeHint("x"), p => p.Id, c => c.ParentId)
            .AsSingleQuery()
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*child query's TablesInScopeHint*");
    }
}
