using System.Collections.Concurrent;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Reproductions and guards for the #105 metadata candidates raised in review: the thread-safe lazy
/// resolution of a shared relationship, the <c>string</c>-as-<c>IEnumerable&lt;char&gt;</c> trap in the
/// attribute scan, nested-selector member resolution, and member-less (fallback) declaration de-duplication.
/// </summary>
public class RelationshipMetadataCandidateTests
{
    public RelationshipMetadataCandidateTests()
    {
        DataContextCache.Clear();
    }

    public sealed class ConcParent
    {
        public int Id { get; set; }
        public ICollection<ConcChild> Children { get; set; } = new List<ConcChild>();
    }

    public sealed class ConcChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ConcParent? Parent { get; set; }
    }

    [Fact]
    public void ConcurrentKeyAndInverseReads_ShouldAlwaysObserveTheFullyResolvedValue()
    {
        var parent = new EntityMetadataBuilder<ConcParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();
        var child = new EntityMetadataBuilder<ConcChild>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build();

        DataContextCache.Metadata[typeof(ConcParent)] = parent;
        DataContextCache.Metadata[typeof(ConcChild)] = child;

        var relationship = parent.Relationships[0];
        var failures = new ConcurrentQueue<string>();
        var observedForeignKeys = new ConcurrentBag<IReadOnlyList<IPropertyMetadata>>();
        var observedPrincipalKeys = new ConcurrentBag<IReadOnlyList<IPropertyMetadata>>();

        Parallel.For(0, 10_000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            var foreignKey = relationship.ForeignKey;
            observedForeignKeys.Add(foreignKey);
            if (foreignKey.Count != 1 || foreignKey[0].PropertyInfo.Name != nameof(ConcChild.ParentId))
                failures.Enqueue("foreign key torn");

            var principalKey = relationship.PrincipalKey;
            observedPrincipalKeys.Add(principalKey);
            if (principalKey.Count != 1 || principalKey[0].PropertyInfo.Name != nameof(ConcParent.Id))
                failures.Enqueue("principal key torn");

            var inverse = relationship.Inverse;
            if (inverse is null || !ReferenceEquals(inverse, child.Relationships[0]))
                failures.Enqueue("inverse torn");
        });

        failures.Should().BeEmpty();

        // The lazy factory runs once (ExecutionAndPublication) and every concurrent reader observes the
        // same published instance, never a re-resolved or half-built value. The factory's internal race
        // window cannot be forced from a test without a seam; this pins the observable publication.
        observedForeignKeys.Should().OnlyContain(fk => ReferenceEquals(fk, observedForeignKeys.First()));
        observedPrincipalKeys.Should().OnlyContain(pk => ReferenceEquals(pk, observedPrincipalKeys.First()));
    }

    [Fact]
    public void Inverse_ReadBeforeTheRelatedTypeIsRegistered_IsCachedAsNull()
    {
        var parent = new EntityMetadataBuilder<ConcParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();
        DataContextCache.Metadata[typeof(ConcParent)] = parent;

        // The related side is not registered yet; Inverse is resolved (once) here and the null result is
        // cached, so a later registration must be completed before the first Inverse read.
        parent.Relationships[0].Inverse.Should().BeNull();

        var child = new EntityMetadataBuilder<ConcChild>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build();
        DataContextCache.Metadata[typeof(ConcChild)] = child;

        parent.Relationships[0].Inverse.Should().BeNull(
            "Inverse is lazily resolved exactly once, so both sides must be registered before it is read");
    }

    public sealed class StringNavParent
    {
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(StringNavChild.ParentId))]
        public string? Label { get; set; }
    }

    public sealed class StringNavChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void RelationshipAttribute_OnStringProperty_ShouldBeRejectedAsAReferenceNavigation()
    {
        Action act = () => new EntityMetadataBuilder<StringNavParent>().Build();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*collection or a reference-typed navigation*");
    }

    public sealed class NestedParent
    {
        public int ParentId { get; set; }
        public NestedHolder Holder { get; set; } = new();
    }

    public sealed class NestedHolder
    {
        public NestedChild Child { get; set; } = new();
    }

    public sealed class NestedChild
    {
        public int Id { get; set; }
    }

    [Fact]
    public void HasOne_WithNestedSelector_ShouldResolveTheSelectedOutermostMember()
    {
        var metadata = new EntityMetadataBuilder<NestedParent>()
            .HasOne(p => p.Holder.Child, p => p.ParentId)
            .Build();

        metadata.Relationships[0].Navigation!.Name.Should().Be(nameof(NestedHolder.Child));
    }

    public sealed class DedupParent
    {
        public int Id { get; set; }
    }

    public sealed class DedupChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void DuplicateFallbackDeclarations_AreIndependent_NoNavigationToDeduplicateBy()
    {
        var metadata = new EntityMetadataBuilder<DedupParent>()
            .HasMany<DedupChild, int>(c => c.ParentId, p => p.Id)
            .HasMany<DedupChild, int>(c => c.ParentId, p => p.Id)
            .Build();

        // A navigation-less declaration has no navigation property to key de-duplication on; each call is
        // an independent declaration and both carry the same resolved keys, so keeping both is harmless.
        metadata.Relationships.Should().HaveCount(2);
    }
}
