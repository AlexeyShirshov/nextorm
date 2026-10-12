using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D01 (#213) red regressions and scalar controls for the JoinInto remainder: composite 1:N / 1:1 keys
/// (declared/local relationship metadata), composite junction many-to-many, and a many-to-many
/// declaration composed into the SingleQuery dispatcher. The composite / M:N-under-SingleQuery cases are
/// the RED regressions for the still-unsupported shapes (they must throw against the unmodified build);
/// the scalar cases are controls that already pass and must keep passing.
/// </summary>
public class JoinIntoCompositeRemainderTests
{
    public JoinIntoCompositeRemainderTests()
    {
        DataContextCache.Clear();
    }

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    /// <summary>
    /// Publishes a metadata entry for <typeparamref name="T"/> that carries the injected relationship and
    /// excludes the navigation property from the mapped columns (the declared-relationship builder would
    /// have done that, but the composite key cannot be expressed through the fluent surface).
    /// </summary>
    private static void RegisterParent<T>(PropertyInfo? navigation, IRelationshipMetadata relationship)
    {
        var properties = new EntityMetadataBuilder<T>().Build().Properties
            .Where(p => navigation is null || p.PropertyInfo != navigation)
            .ToList();

        DataContextCache.Metadata[typeof(T)] =
            new EntityMetadata(typeof(T).Name, properties, relationships: [relationship]);
    }

    /// <summary>
    /// A relationship whose foreign/principal keys are supplied as explicit lists, so a composite key can
    /// be injected without the (single-column only) fluent declaration surface.
    /// </summary>
    private sealed class InjectedRelationship : IRelationshipMetadata
    {
        public InjectedRelationship(
            RelationshipKind kind,
            Type declaringType,
            Type relatedType,
            PropertyInfo? navigation,
            bool isCollection,
            IReadOnlyList<IPropertyMetadata> foreignKey,
            IReadOnlyList<IPropertyMetadata> principalKey)
        {
            Kind = kind;
            DeclaringType = declaringType;
            RelatedType = relatedType;
            Navigation = navigation;
            IsCollection = isCollection;
            ForeignKey = foreignKey;
            PrincipalKey = principalKey;
        }

        public RelationshipKind Kind { get; }
        public Type DeclaringType { get; }
        public Type RelatedType { get; }
        public PropertyInfo? Navigation { get; }
        public bool IsCollection { get; }
        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }
        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
        public IRelationshipMetadata? Inverse => null;
    }

    // ---------------------------------------------------------------------------------------------
    // RED regressions: composite parent/child keys announced as supported but not yet assembled.
    // ---------------------------------------------------------------------------------------------

    public sealed class CompositeOneToManyParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<CompositeOneToManyChild> Children { get; set; } = new List<CompositeOneToManyChild>();
    }

    public sealed class CompositeOneToManyChild
    {
        public int TenantId { get; set; }
        public int ParentId { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void CompositeOneToMany_ShouldGroupChildrenByFullKey()
    {
        var parentProps = Props<CompositeOneToManyParent>();
        var childProps = Props<CompositeOneToManyChild>();
        var navigation = typeof(CompositeOneToManyParent).GetProperty(nameof(CompositeOneToManyParent.Children));
        RegisterParent<CompositeOneToManyParent>(navigation, new InjectedRelationship(
            RelationshipKind.OneToMany,
            typeof(CompositeOneToManyParent),
            typeof(CompositeOneToManyChild),
            navigation,
            isCollection: true,
            foreignKey: [Prop(childProps, nameof(CompositeOneToManyChild.TenantId)), Prop(childProps, nameof(CompositeOneToManyChild.ParentId))],
            principalKey: [Prop(parentProps, nameof(CompositeOneToManyParent.TenantId)), Prop(parentProps, nameof(CompositeOneToManyParent.Id))]));

        using var ctx = new InMemoryDataContext();
        ctx.From<CompositeOneToManyParent>().WithData([
            new CompositeOneToManyParent { TenantId = 1, Id = 1, Name = "p" },
        ]);
        ctx.From<CompositeOneToManyChild>().WithData([
            new CompositeOneToManyChild { TenantId = 1, ParentId = 1, Id = 10, Name = "match" },
            // Same parent id but a different tenant: the full composite key must reject the cross row.
            new CompositeOneToManyChild { TenantId = 2, ParentId = 1, Id = 20, Name = "cross" },
        ]);

        var parents = ctx.From<CompositeOneToManyParent>()
            .JoinInto(
                ctx.From<CompositeOneToManyChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 }, "only the full composite-key match is grouped");
    }

    public sealed class CompositeOneToOneParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public CompositeOneToOneChild? Profile { get; set; }
    }

    public sealed class CompositeOneToOneChild
    {
        public int TenantId { get; set; }
        public int ParentId { get; set; }
        public int Id { get; set; }
    }

    [Fact]
    public void CompositeOneToOne_ShouldAssignByFullKey()
    {
        var parentProps = Props<CompositeOneToOneParent>();
        var childProps = Props<CompositeOneToOneChild>();
        var navigation = typeof(CompositeOneToOneParent).GetProperty(nameof(CompositeOneToOneParent.Profile));
        RegisterParent<CompositeOneToOneParent>(navigation, new InjectedRelationship(
            RelationshipKind.OneToOne,
            typeof(CompositeOneToOneParent),
            typeof(CompositeOneToOneChild),
            navigation,
            isCollection: false,
            foreignKey: [Prop(childProps, nameof(CompositeOneToOneChild.TenantId)), Prop(childProps, nameof(CompositeOneToOneChild.ParentId))],
            principalKey: [Prop(parentProps, nameof(CompositeOneToOneParent.TenantId)), Prop(parentProps, nameof(CompositeOneToOneParent.Id))]));

        using var ctx = new InMemoryDataContext();
        ctx.From<CompositeOneToOneParent>().WithData([
            new CompositeOneToOneParent { TenantId = 1, Id = 1 },
        ]);
        ctx.From<CompositeOneToOneChild>().WithData([
            new CompositeOneToOneChild { TenantId = 1, ParentId = 1, Id = 50 },
            new CompositeOneToOneChild { TenantId = 2, ParentId = 1, Id = 60 },
        ]);

        var parents = ctx.From<CompositeOneToOneParent>()
            .JoinInto(
                ctx.From<CompositeOneToOneChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Profile)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Profile.Should().NotBeNull();
        parents[0].Profile!.Id.Should().Be(50, "the full composite key selects the matching reference");
    }

    public sealed class CompositeJunctionParent
    {
        public int Id { get; set; }
        public ICollection<CompositeJunctionChild> Children { get; set; } = new List<CompositeJunctionChild>();
    }

    public sealed class CompositeJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class CompositeJunctionLink
    {
        public int ParentId { get; set; }
        public int ParentId2 { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ShouldStitchByFullJunctionKey()
    {
        var parentProps = Props<CompositeJunctionParent>();
        var childProps = Props<CompositeJunctionChild>();
        var linkProps = Props<CompositeJunctionLink>();
        var parentKey = Prop(parentProps, nameof(CompositeJunctionParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(CompositeJunctionChild.Id)).PropertyInfo;
        var navigation = typeof(CompositeJunctionParent).GetProperty(nameof(CompositeJunctionParent.Children));

        DataContextCache.Metadata[typeof(CompositeJunctionParent)] = new EntityMetadata(
            typeof(CompositeJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(CompositeJunctionParent),
                    typeof(CompositeJunctionChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    parentKey,
                    typeof(CompositeJunctionChild),
                    typeof(CompositeJunctionParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(CompositeJunctionLink),
                        ParentKey = [parentKey],
                        ChildKey = [childKey],
                        JunctionParentForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ParentId)).PropertyInfo, Prop(linkProps, nameof(CompositeJunctionLink.ParentId2)).PropertyInfo],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ChildId)).PropertyInfo],
                    }),
            ]);

        using var ctx = new InMemoryDataContext();
        ctx.From<CompositeJunctionParent>().WithData([new CompositeJunctionParent { Id = 1 }]);
        ctx.From<CompositeJunctionChild>().WithData([new CompositeJunctionChild { Id = 10 }]);
        ctx.From<CompositeJunctionLink>().WithData([new CompositeJunctionLink { ParentId = 1, ParentId2 = 7, ChildId = 10 }]);

        var parents = ctx.From<CompositeJunctionParent>()
            .JoinInto(ctx.From<CompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(10);
    }

    public sealed class SingleQueryRemainderParent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<SingleQueryRemainderChild> Children { get; set; } = new List<SingleQueryRemainderChild>();
        public ICollection<SingleQueryRemainderNote> Notes { get; set; } = new List<SingleQueryRemainderNote>();
    }

    public sealed class SingleQueryRemainderChild
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class SingleQueryRemainderLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class SingleQueryRemainderNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Text { get; set; }
    }

    private static InMemoryDataContext CreateSingleQueryRemainderContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<SingleQueryRemainderParent>(b => b
                .HasManyThrough<SingleQueryRemainderChild, SingleQueryRemainderLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
                .HasMany(p => p.Notes, n => n.ParentId))
            .WithData([new SingleQueryRemainderParent { Id = 1, Name = "p1" }]);
        ctx.From<SingleQueryRemainderChild>().WithData([
            new SingleQueryRemainderChild { Id = 10, Name = "c1" },
            new SingleQueryRemainderChild { Id = 11, Name = "c2" },
        ]);
        ctx.From<SingleQueryRemainderLink>().WithData([
            new SingleQueryRemainderLink { ParentId = 1, ChildId = 10 },
            new SingleQueryRemainderLink { ParentId = 1, ChildId = 11 },
        ]);
        ctx.From<SingleQueryRemainderNote>().WithData([new SingleQueryRemainderNote { Id = 20, ParentId = 1, Text = "n" }]);
        return ctx;
    }

    [Fact]
    public void ManyToMany_SingleQuery_ShouldStitchBothCollections()
    {
        using var ctx = CreateSingleQueryRemainderContext();

        var parents = ctx.From<SingleQueryRemainderParent>()
            .JoinInto(ctx.From<SingleQueryRemainderChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<SingleQueryRemainderNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(20);
    }

    [Fact]
    public async Task ManyToMany_SingleQuery_Async_ShouldStitchBothCollections()
    {
        using var ctx = CreateSingleQueryRemainderContext();

        var parents = await ctx.From<SingleQueryRemainderParent>()
            .JoinInto(ctx.From<SingleQueryRemainderChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<SingleQueryRemainderNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).OrderBy(id => id).Should().Equal(10, 11);
        parents[0].Notes.Select(n => n.Id).Should().Equal(20);
    }

    // ---------------------------------------------------------------------------------------------
    // Scalar controls: already supported, must stay green (guard against a behaviour-drift fix).
    // ---------------------------------------------------------------------------------------------

    private static InMemoryDataContext CreateScalarManyToManyContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ManyToManyJoinParent>(b => b
                .HasManyThrough<ManyToManyJoinChild, ManyToManyJoinLink, int, int>(
                    p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId))
            .WithData([
                new ManyToManyJoinParent { Id = 1, Name = "p1" },
                new ManyToManyJoinParent { Id = 2, Name = "p2" },
            ]);
        ctx.From<ManyToManyJoinChild>().WithData([
            new ManyToManyJoinChild { Id = 10, Name = "keep" },
            new ManyToManyJoinChild { Id = 11, Name = "drop" },
        ]);
        ctx.From<ManyToManyJoinLink>().WithData([
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 10 },
            new ManyToManyJoinLink { ParentId = 1, ChildId = 11 },
        ]);
        return ctx;
    }

    [Fact]
    public void ScalarManyToMany_Standalone_ShouldPreserveDuplicateLinks()
    {
        using var ctx = CreateScalarManyToManyContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Children.Where(c => c.Id == 10).Should().HaveCount(2, "duplicate junction rows are distinct occurrences");
        parents[1].Children.Should().BeEmpty("a LEFT many-to-many join keeps a parent without a link");
    }

    [Fact]
    public void ScalarManyToMany_InnerAndFilter_ShouldApply()
    {
        using var ctx = CreateScalarManyToManyContext();

        var parents = ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => c.Name == "keep", p => p.Children, JoinType.Inner)
            .ToList();

        parents.Should().ContainSingle("INNER drops the parent without a matching link; the filter drops child 11");
        parents[0].Children.Should().OnlyContain(c => c.Id == 10);
    }

    [Fact]
    public async Task ScalarManyToMany_Async_ShouldStitch()
    {
        using var ctx = CreateScalarManyToManyContext();

        var parents = await ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
            .ToListAsync(TestContext.Current.CancellationToken);

        parents.Should().HaveCount(2);
        parents[0].Children.Should().HaveCount(3);
    }

    [Fact]
    public void ScalarManyToMany_NonListTerminal_ShouldCountParents()
    {
        using var ctx = CreateScalarManyToManyContext();

        var joined = ctx.From<ManyToManyJoinParent>()
            .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children);

        joined.Count().Should().Be(2, "the non-list terminal counts parents, not denormalized rows");
    }

    [Fact]
    public void ScalarManyToMany_RepeatedCachedExecution_ShouldStayStable()
    {
        using var ctx = CreateScalarManyToManyContext();

        for (var i = 0; i < 3; i++)
        {
            var parents = ctx.From<ManyToManyJoinParent>()
                .JoinInto(ctx.From<ManyToManyJoinChild>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2);
            parents[0].Children.Should().HaveCount(3);
            parents[1].Children.Should().BeEmpty();
        }
    }

    public sealed class CompositeIdentityParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<CompositeIdentityChild> Children { get; set; } = new List<CompositeIdentityChild>();
    }

    public sealed class CompositeIdentityChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void CompositeParentIdentity_SameFirstComponentDifferentLater_ShouldStayDistinct()
    {
        // The parent declares a composite key (TenantId + Id). Two parents share the first component but
        // differ in the later one; deduplicating by the complete identity keeps them separate.
        var builder = new EntityMetadataBuilder<CompositeIdentityParent>();
        builder.Property(p => p.TenantId).Key();
        builder.Property(p => p.Id).Key();
        var parentProps = builder.Build().Properties;
        var childProps = Props<CompositeIdentityChild>();
        var navigation = typeof(CompositeIdentityParent).GetProperty(nameof(CompositeIdentityParent.Children));

        var properties = parentProps.Where(p => p.PropertyInfo != navigation).ToList();
        DataContextCache.Metadata[typeof(CompositeIdentityParent)] = new EntityMetadata(
            typeof(CompositeIdentityParent).Name,
            properties,
            relationships:
            [
                new InjectedRelationship(
                    RelationshipKind.OneToMany,
                    typeof(CompositeIdentityParent),
                    typeof(CompositeIdentityChild),
                    navigation,
                    isCollection: true,
                    foreignKey: [Prop(childProps, nameof(CompositeIdentityChild.ParentId))],
                    principalKey: [Prop(parentProps, nameof(CompositeIdentityParent.Id))]),
            ]);

        using var ctx = new InMemoryDataContext();
        ctx.From<CompositeIdentityParent>().WithData([
            new CompositeIdentityParent { TenantId = 1, Id = 1 },
            new CompositeIdentityParent { TenantId = 1, Id = 2 },
        ]);
        ctx.From<CompositeIdentityChild>().WithData([
            new CompositeIdentityChild { Id = 10, ParentId = 1 },
            new CompositeIdentityChild { Id = 11, ParentId = 2 },
        ]);

        var parents = ctx.From<CompositeIdentityParent>()
            .JoinInto(ctx.From<CompositeIdentityChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2, "same first key component with a different later component must stay separate");
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);
    }
}
