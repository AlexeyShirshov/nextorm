using System.Collections.Concurrent;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Regression tests for the process-wide <c>JoinInto</c> identity-selector cache. The cache must be
/// invalidated by <see cref="DataContextCache.Clear"/> together with the metadata it was compiled
/// from, so a later registration re-derives the key choice instead of returning the stale one. The
/// fixture participates in the disabled-parallelization collection because it clears the process-wide
/// cache, which other tests read concurrently.
/// </summary>
[Collection("Query cache controls")]
public sealed class JoinIntoIdentitySelectorCacheTests
{
    public JoinIntoIdentitySelectorCacheTests()
    {
        DataContextCache.Clear();
    }

    private sealed class SingleKeyEntity
    {
        public int Id { get; set; }
    }

    private sealed class CompositeKeyEntity
    {
        public int First { get; set; }
        public int Second { get; set; }
    }

    private sealed class DualKeyEntity
    {
        public int Id { get; set; }
        public int AltId { get; set; }
    }

    private sealed class KeylessEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class NullableKeyEntity
    {
        public string? Code { get; set; }
    }

    private struct StructEntity
    {
        public int Id { get; set; }
    }

    private struct StructParent
    {
        public StructParent()
        {
            Children = new List<GroupChild>();
        }

        public int Id { get; set; }

        public ICollection<GroupChild> Children { get; set; }
    }

    private sealed class GroupParent
    {
        public int Id { get; set; }
        public ICollection<GroupChild> Children { get; set; } = new List<GroupChild>();
    }

    private sealed class GroupChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static void Register<T>(Action<EntityMetadataBuilder<T>>? configure = null)
    {
        var builder = new EntityMetadataBuilder<T>();
        configure?.Invoke(builder);
        DataContextCache.Metadata[typeof(T)] = builder.Build();
    }

    [Fact]
    public void CachesAndRebuildsSingleAndCompositeKeySelectors()
    {
        Register<SingleKeyEntity>();
        Register<CompositeKeyEntity>(b =>
        {
            b.Property(x => x.First).Key();
            b.Property(x => x.Second).Key();
        });

        var single = JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>();
        single.Should().NotBeNull();
        single!(new SingleKeyEntity { Id = 7 }).Should().Be(7);
        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().BeSameAs(single,
            "a successful selector is cached and reused until the cache is cleared");

        var composite = JoinIntoSpecHelpers.BuildIdentitySelector<CompositeKeyEntity>();
        composite.Should().NotBeNull();
        JoinIntoSpecHelpers.BuildIdentitySelector<CompositeKeyEntity>().Should().BeSameAs(composite);

        var first = composite!(new CompositeKeyEntity { First = 1, Second = 2 });
        first.Should().BeOfType<JoinIntoKey>("a composite key is represented structurally");
        first.Should().Be(composite!(new CompositeKeyEntity { First = 1, Second = 2 }));
        first.Should().NotBe(composite!(new CompositeKeyEntity { First = 1, Second = 3 }));
    }

    [Fact]
    public void ClearInvalidatesSelectorsForAllPopulatedTypes()
    {
        Register<SingleKeyEntity>();
        Register<CompositeKeyEntity>(b =>
        {
            b.Property(x => x.First).Key();
            b.Property(x => x.Second).Key();
        });

        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().NotBeNull();
        JoinIntoSpecHelpers.BuildIdentitySelector<CompositeKeyEntity>().Should().NotBeNull();

        DataContextCache.Clear();

        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().BeNull(
            "the global clear must drop every cached selector, not just one type");
        JoinIntoSpecHelpers.BuildIdentitySelector<CompositeKeyEntity>().Should().BeNull();
    }

    [Fact]
    public void ClearRebuildsSelectorAfterKeyConfigurationChanges()
    {
        Register<DualKeyEntity>(b =>
        {
            b.Property(x => x.Id).Key();
            b.Property(x => x.AltId);
        });

        var original = JoinIntoSpecHelpers.BuildIdentitySelector<DualKeyEntity>();
        original.Should().NotBeNull();
        original!(new DualKeyEntity { Id = 10, AltId = 20 }).Should().Be(10);

        // Equivalent metadata: the delegate is rebuilt (a distinct instance) but yields the same key.
        DataContextCache.Clear();
        Register<DualKeyEntity>(b =>
        {
            b.Property(x => x.Id).Key();
            b.Property(x => x.AltId);
        });

        var equivalent = JoinIntoSpecHelpers.BuildIdentitySelector<DualKeyEntity>();
        equivalent.Should().NotBeNull();
        equivalent.Should().NotBeSameAs(original, "the clear must force a rebuild even for equivalent metadata");
        equivalent!(new DualKeyEntity { Id = 10, AltId = 20 }).Should().Be(10);

        // Changed key configuration: the rebuilt selector returns the new key, not the stale cached one.
        DataContextCache.Clear();
        Register<DualKeyEntity>(b =>
        {
            b.Property(x => x.Id);
            b.Property(x => x.AltId).Key();
        });

        var changed = JoinIntoSpecHelpers.BuildIdentitySelector<DualKeyEntity>();
        changed.Should().NotBeNull();
        changed.Should().NotBeSameAs(original);
        changed!(new DualKeyEntity { Id = 10, AltId = 20 }).Should().Be(20,
            "the selector must be re-derived from the current key metadata after a clear");
    }

    [Fact]
    public void MissingMetadataDoesNotPoisonLaterRegistration()
    {
        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().BeNull();

        Register<SingleKeyEntity>();

        var selector = JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>();
        selector.Should().NotBeNull("a null result must not be cached, so a later registration can build");
        selector!(new SingleKeyEntity { Id = 3 }).Should().Be(3);
    }

    [Fact]
    public void KeylessMetadataDoesNotPoisonLaterKeyRegistration()
    {
        Register<KeylessEntity>(b => b.Property(x => x.Name!));

        JoinIntoSpecHelpers.BuildIdentitySelector<KeylessEntity>().Should().BeNull("metadata without a key has no identity");

        Register<KeylessEntity>(b =>
        {
            b.Property(x => x.Id).Key();
            b.Property(x => x.Name!);
        });

        var selector = JoinIntoSpecHelpers.BuildIdentitySelector<KeylessEntity>();
        selector.Should().NotBeNull("the keyless null result must not block a later keyed registration");
        selector!(new KeylessEntity { Id = 5 }).Should().Be(5);
    }

    [Fact]
    public void NullKeyValueIsHandledAndLaterRegistrationStillBuilds()
    {
        Register<NullableKeyEntity>(b => b.Property(x => x.Code!).Key());

        var selector = JoinIntoSpecHelpers.BuildIdentitySelector<NullableKeyEntity>();
        selector.Should().NotBeNull("a nullable reference key may still declare an identity");
        selector!(new NullableKeyEntity { Code = null }).Should().BeNull(
            "a null key value yields a null identity instead of throwing");
        selector!(new NullableKeyEntity { Code = "abc" }).Should().Be("abc");

        DataContextCache.Clear();

        // The global clear drops the null-capable selector; a later keyed registration must still build
        // one and keep handling a null key value.
        Register<NullableKeyEntity>(b => b.Property(x => x.Code!).Key());

        var rebuilt = JoinIntoSpecHelpers.BuildIdentitySelector<NullableKeyEntity>();
        rebuilt.Should().NotBeNull();
        rebuilt.Should().NotBeSameAs(selector);
        rebuilt!(new NullableKeyEntity { Code = null }).Should().BeNull();
        rebuilt!(new NullableKeyEntity { Code = "def" }).Should().Be("def");
    }

    [Fact]
    public void ValueTypeEntitySelectorIsCachedAndInvalidatedAndJoinIntoStitches()
    {
        // Selector level: the process-wide cache compiles a value-type (struct) entity like any
        // reference type, caches it, and the global clear invalidates it.
        Register<StructEntity>();

        var selector = JoinIntoSpecHelpers.BuildIdentitySelector<StructEntity>();
        selector.Should().NotBeNull("a struct entity with a mapped key has an identity selector");
        selector!(new StructEntity { Id = 9 }).Should().Be(9);
        JoinIntoSpecHelpers.BuildIdentitySelector<StructEntity>().Should().BeSameAs(selector);

        DataContextCache.Clear();
        JoinIntoSpecHelpers.BuildIdentitySelector<StructEntity>().Should().BeNull();

        // In-memory JoinInto: a struct parent whose collection is pre-initialized is stitched correctly,
        // with the rebuilt identity selector deduplicating the parents.
        using var ctx = new InMemoryDataContext();
        ctx.From<StructParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .WithData([
                new StructParent { Id = 1 },
                new StructParent { Id = 2 },
            ]);
        ctx.From<GroupChild>()
            .WithData([
                new GroupChild { Id = 10, ParentId = 1 },
                new GroupChild { Id = 11, ParentId = 1 },
                new GroupChild { Id = 12, ParentId = 2 },
            ]);

        var parents = ctx.From<StructParent>()
            .JoinInto(ctx.From<GroupChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
    }

    [Fact]
    public void ClearIsIdempotentForAnEmptyCache()
    {
        DataContextCache.Clear();
        DataContextCache.Clear();

        Register<SingleKeyEntity>();
        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().NotBeNull();

        DataContextCache.Clear();
        DataContextCache.Clear();

        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentBuildAndClearRemainSafe()
    {
        Register<SingleKeyEntity>();
        var expected = new SingleKeyEntity { Id = 42 };

        var failures = new ConcurrentBag<Exception>();
        var invalid = 0;

        var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
            {
                try
                {
                    var selector = JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>();
                    if (selector is not null && !Equals(selector(expected), 42))
                        Interlocked.Increment(ref invalid);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }

                if (i % 5 == 0)
                    DataContextCache.Clear();
            }
        }));

        await Task.WhenAll(workers);

        failures.Should().BeEmpty("concurrent build and clear must not throw");
        invalid.Should().Be(0, "every non-null selector must still describe the registered mapping");

        DataContextCache.Clear();
        JoinIntoSpecHelpers.BuildIdentitySelector<SingleKeyEntity>().Should().BeNull(
            "a final quiescent clear invalidates whatever remains");
    }

    [Fact]
    public void JoinIntoGroupsCorrectlyAfterClear()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<GroupParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .WithData([
                new GroupParent { Id = 1 },
                new GroupParent { Id = 2 },
            ]);
        ctx.From<GroupChild>()
            .WithData([
                new GroupChild { Id = 10, ParentId = 1 },
                new GroupChild { Id = 11, ParentId = 1 },
                new GroupChild { Id = 12, ParentId = 2 },
            ]);

        var before = ctx.From<GroupParent>()
            .JoinInto(ctx.From<GroupChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();
        before.Should().HaveCount(2);
        before.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        before.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);

        DataContextCache.Clear();

        // Re-register after the global clear; the rebuilt selectors must group identically.
        ctx.From<GroupParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<GroupChild>();

        var after = ctx.From<GroupParent>()
            .JoinInto(ctx.From<GroupChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();
        after.Should().HaveCount(2);
        after.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
        after.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
    }
}
