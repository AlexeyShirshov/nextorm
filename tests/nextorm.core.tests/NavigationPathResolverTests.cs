using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D3 contract tests for the internal <see cref="NavigationPathResolver"/> (#148-A): exact
/// navigation-member-chain resolution against an explicit <see cref="NavigationResolutionScope"/>, using
/// real relationship metadata built with <see cref="EntityMetadataBuilder{T}"/> (no parallel fake
/// relationship system). Every positive row asserts the resolved path; every negative row is a
/// fail-closed <see cref="NotSupportedException"/> (or <see cref="ArgumentNullException"/> for a null
/// argument). The tests assert behavior, never file/line text.
/// </summary>
[Collection("Query cache controls")]
public class NavigationPathResolverTests
{
    public NavigationPathResolverTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------------

    public sealed class ParentA
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public ICollection<ChildA> Children { get; set; } = new List<ChildA>();
    }

    public sealed class ChildA
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ParentA? Parent { get; set; }
    }

    public sealed class OwnerA
    {
        public int Id { get; set; }
        public ProfileA? Profile { get; set; }
    }

    public sealed class ProfileA
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
    }

    public sealed class DualParent
    {
        public int Id { get; set; }
        public int PrimaryChildId { get; set; }
        public int SecondaryChildId { get; set; }
        public DualChild? Primary { get; set; }
        public DualChild? Secondary { get; set; }
    }

    public sealed class DualChild
    {
        public int Id { get; set; }
    }

    public sealed class NodeA
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public NodeA? Parent { get; set; }
        public ICollection<NodeA> Children { get; set; } = new List<NodeA>();
    }

    public sealed class M2MParent
    {
        public int Id { get; set; }
        public List<M2MChild> Children { get; set; } = new();
    }

    public sealed class M2MChild
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class M2MLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class DualJunctionParent
    {
        public int Id { get; set; }
        public List<M2MChild> A { get; set; } = new();
        public List<M2MChild> B { get; set; } = new();
    }

    public sealed class JunctionLinkA
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class JunctionLinkB
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class ClashParent
    {
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(ClashChild.OtherParentId))]
        public ICollection<ClashChild> Children { get; set; } = new List<ClashChild>();
    }

    public sealed class ClashChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int OtherParentId { get; set; }
    }

    public class HiddenBase
    {
        public int Id { get; set; }
        public HiddenChild? Nav { get; set; }
    }

    public sealed class HiddenDerived : HiddenBase
    {
        public new HiddenChild? Nav { get; set; }
        public int DerivedId { get; set; }
    }

    public sealed class HiddenChild
    {
        public int Id { get; set; }
    }

    public sealed class KeylessParent
    {
        public string Name { get; set; } = "";
        public ICollection<KeylessChild> Children { get; set; } = new List<KeylessChild>();
    }

    public sealed class KeylessChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class NarrowParent
    {
        public int Id { get; set; }
        public ICollection<NarrowChild> Children { get; set; } = new List<NarrowChild>();
    }

    public sealed class NarrowChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public struct ValueEntity
    {
        public int Id { get; set; }
    }

    public class ConvBase
    {
        public ConvChild? Child { get; set; }
    }

    public sealed class ConvDerived : ConvBase
    {
        public int ChildId { get; set; }
    }

    public sealed class ConvChild
    {
        public int Id { get; set; }
    }

    public sealed class ConvertibleEntity
    {
        public int Id { get; set; }
    }

    public sealed class ConversionTarget
    {
        public static implicit operator ConversionTarget(ConvertibleEntity entity) => new();
    }

    public static ParentA StaticRoot { get; set; } = new();

    public static class GetterProbe
    {
        public static int Count;
        public static void Reset() => Count = 0;
    }

    public sealed class ThrowingParent
    {
        public int Id { get; set; }

        public ICollection<ThrowingChild> Children
        {
            get
            {
                GetterProbe.Count++;
                throw new InvalidOperationException("the navigation getter must never be read");
            }
            set { }
        }
    }

    public sealed class ThrowingChild
    {
        public int Id { get; set; }

        public int ParentId
        {
            get
            {
                GetterProbe.Count++;
                throw new InvalidOperationException("the foreign-key getter must never be read");
            }
            set { }
        }

        public ThrowingParent? Parent
        {
            get
            {
                GetterProbe.Count++;
                throw new InvalidOperationException("the reference getter must never be read");
            }
            set { }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static NavigationResolutionScope AnyScope() => new(new object(), Array.Empty<NavigationSourceBinding>());

    private static NavigationResolutionScope ScopeWith(object scopeIdentity, ParameterExpression anchor, Type entityType)
        => new(scopeIdentity, new[] { new NavigationSourceBinding(scopeIdentity, anchor, anchor, entityType) });

    private static NavigationResolutionScope ScopeWith(object scopeIdentity, params NavigationSourceBinding[] roots)
        => new(scopeIdentity, roots);

    private static IEntityMetadata Register<T>(EntityMetadataBuilder<T> builder)
    {
        var metadata = builder.Build();
        DataContextCache.Metadata[typeof(T)] = metadata;
        return metadata;
    }

    private static ResolvedNavigationPath Resolve(Expression path, object scopeIdentity, ParameterExpression anchor, Type entityType)
        => NavigationPathResolver.Resolve(path, ScopeWith(scopeIdentity, anchor, entityType));

    private static Expression Member(Expression instance, string name) => Expression.Property(instance, name);

    public static IEnumerable<ChildA> ArbitraryCall(IEnumerable<ChildA> children) => children;

    // ---------------------------------------------------------------------------------------------
    // Root binding, scope visibility, source identity
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_BoundEntityParameter_AndExplicitlyBoundJoinedAlias_ReturnsOrderedExactMembers()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));
        Register(new EntityMetadataBuilder<ChildA>().HasOne(c => c.Parent, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var a = Expression.Parameter(typeof(ChildA), "a");
        var scope = ScopeWith(scopeId,
            new NavigationSourceBinding(scopeId, p, p, typeof(ParentA)),
            new NavigationSourceBinding(scopeId, a, a, typeof(ChildA)));

        var path = NavigationPathResolver.Resolve(Member(Member(a, nameof(ChildA.Parent)), nameof(ParentA.Children)), scope);

        path.RootEntityType.Should().Be(typeof(ChildA));
        path.RootBinding.SourceIdentity.Should().BeSameAs(a);
        path.RootBinding.OwningScope.Should().BeSameAs(scopeId);
        path.Hops.Should().HaveCount(2);
        Assert.Equal(typeof(ChildA).GetProperty(nameof(ChildA.Parent)), path.Hops[0].Navigation);
        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Children)), path.Hops[1].Navigation);
        path.Hops[0].Kind.Should().Be(RelationshipKind.ManyToOne);
        path.Hops[1].Kind.Should().Be(RelationshipKind.OneToMany);
    }

    [Fact]
    public void Resolve_UnboundParameter_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var other = Expression.Parameter(typeof(ParentA), "other");
        var scope = ScopeWith(new object(), p, typeof(ParentA));

        Action act = () => NavigationPathResolver.Resolve(Member(other, nameof(ParentA.Children)), scope);

        act.Should().Throw<NotSupportedException>().WithMessage("*not bound to a visible source*");
    }

    [Fact]
    public void Resolve_SameTypeDifferentSource_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p1 = Expression.Parameter(typeof(ParentA), "p1");
        var p2 = Expression.Parameter(typeof(ParentA), "p2");
        var scope = ScopeWith(new object(), p1, typeof(ParentA));

        Action act = () => NavigationPathResolver.Resolve(Member(p2, nameof(ParentA.Children)), scope);

        act.Should().Throw<NotSupportedException>().WithMessage("*not bound to a visible source*");
    }

    [Fact]
    public void Resolve_VisibleOuterBinding_Succeeds()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var path = Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));

        path.Hops.Should().ContainSingle();
        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Children)), path.Hops[0].Navigation);
    }

    [Fact]
    public void Resolve_SiblingOrUnregisteredScopeBinding_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var ownerScope = new object();
        var resolvingScope = new object();
        var binding = new NavigationSourceBinding(ownerScope, p, p, typeof(ParentA));
        var scope = ScopeWith(resolvingScope, binding);

        Action act = () => NavigationPathResolver.Resolve(Member(p, nameof(ParentA.Children)), scope);

        act.Should().Throw<NotSupportedException>().WithMessage("*sibling or unregistered scope*");
    }

    [Fact]
    public void Resolve_UnregisteredScopeBinding_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");

        Action act = () => Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));
        act.Should().NotThrow();

        var scope = ScopeWith(new object());
        Action missing = () => NavigationPathResolver.Resolve(Member(p, nameof(ParentA.Children)), scope);
        missing.Should().Throw<NotSupportedException>().WithMessage("*not bound to a visible source*");
    }

    [Fact]
    public void Resolve_SamePathRepeated_UsesReuseCompatibleIdentity()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var expression = Member(p, nameof(ParentA.Children));

        var first = Resolve(expression, scopeId, p, typeof(ParentA));
        var second = Resolve(expression, scopeId, p, typeof(ParentA));

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Resolve_TwoAliases_ProduceDistinctIdentities()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p1 = Expression.Parameter(typeof(ParentA), "p1");
        var p2 = Expression.Parameter(typeof(ParentA), "p2");
        var scope = ScopeWith(scopeId,
            new NavigationSourceBinding(scopeId, p1, p1, typeof(ParentA)),
            new NavigationSourceBinding(scopeId, p2, p2, typeof(ParentA)));

        var first = NavigationPathResolver.Resolve(Member(p1, nameof(ParentA.Children)), scope);
        var second = NavigationPathResolver.Resolve(Member(p2, nameof(ParentA.Children)), scope);

        first.RootBinding.SameIdentity(second.RootBinding).Should().BeFalse();
        first.Equals(second).Should().BeFalse();
    }

    [Fact]
    public void Resolve_DualSameTypeNavigations_ProduceDistinctHopIdentities()
    {
        Register(new EntityMetadataBuilder<DualParent>()
            .HasOne(p => p.Primary, p => p.PrimaryChildId)
            .HasOne(p => p.Secondary, p => p.SecondaryChildId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(DualParent), "p");
        var scope = ScopeWith(scopeId, p, typeof(DualParent));

        var primary = NavigationPathResolver.Resolve(Member(p, nameof(DualParent.Primary)), scope);
        var secondary = NavigationPathResolver.Resolve(Member(p, nameof(DualParent.Secondary)), scope);

        Assert.Equal(typeof(DualParent).GetProperty(nameof(DualParent.Primary)), primary.Hops[0].Navigation);
        Assert.Equal(typeof(DualParent).GetProperty(nameof(DualParent.Secondary)), secondary.Hops[0].Navigation);
        primary.Hops[0].SameIdentity(secondary.Hops[0]).Should().BeFalse();
        primary.Equals(secondary).Should().BeFalse();
    }

    [Fact]
    public void Resolve_SameClrTypeDifferentAnchorOrScope_HasDistinctBindingIdentity()
    {
        var scopeA = new object();
        var scopeB = new object();
        var p1 = Expression.Parameter(typeof(ParentA), "p1");
        var p2 = Expression.Parameter(typeof(ParentA), "p2");

        var first = new NavigationSourceBinding(scopeA, p1, p1, typeof(ParentA));
        var sameAnchorDifferentScope = new NavigationSourceBinding(scopeB, p1, p1, typeof(ParentA));
        var differentAnchorSameScope = new NavigationSourceBinding(scopeA, p2, p2, typeof(ParentA));

        first.SameIdentity(sameAnchorDifferentScope).Should().BeFalse();
        first.SameIdentity(differentAnchorSameScope).Should().BeFalse();
        first.SameIdentity(new NavigationSourceBinding(scopeA, p1, p1, typeof(ParentA))).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ScopesWithDistinctIdentity_ProduceDistinctPaths()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var expression = Member(p, nameof(ParentA.Children));

        var first = Resolve(expression, new object(), p, typeof(ParentA));
        var second = Resolve(expression, new object(), p, typeof(ParentA));

        first.Equals(second).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // Cardinality, direction and exact keys
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_HasOne_ProducesDependentToPrincipalWithExactKeys()
    {
        Register(new EntityMetadataBuilder<ChildA>().HasOne(c => c.Parent, c => c.ParentId));

        var c = Expression.Parameter(typeof(ChildA), "c");
        var path = Resolve(Member(c, nameof(ChildA.Parent)), new object(), c, typeof(ChildA));
        var hop = path.Hops[0];

        hop.Kind.Should().Be(RelationshipKind.ManyToOne);
        hop.IsCollection.Should().BeFalse();
        hop.Direction.Should().Be(NavigationDirection.DependentToPrincipal);
        hop.DeclaringType.Should().Be(typeof(ChildA));
        hop.RelatedType.Should().Be(typeof(ParentA));
        Assert.Equal(typeof(ChildA).GetProperty(nameof(ChildA.ParentId)), hop.PrimaryLeg.ForeignKey.PropertyInfo);
        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Id)), hop.PrimaryLeg.PrincipalKey.PropertyInfo);
        hop.SecondaryLeg.Should().BeNull();
    }

    [Fact]
    public void Resolve_HasOneToOne_ProducesPrincipalToDependentWithExactKeys()
    {
        Register(new EntityMetadataBuilder<OwnerA>()
            .HasOneToOne(o => o.Profile, o => o.Id, p => p.OwnerId));

        var o = Expression.Parameter(typeof(OwnerA), "o");
        var path = Resolve(Member(o, nameof(OwnerA.Profile)), new object(), o, typeof(OwnerA));
        var hop = path.Hops[0];

        hop.Kind.Should().Be(RelationshipKind.OneToOne);
        hop.IsCollection.Should().BeFalse();
        // HasOneToOne declares the principal side: the FK lives on the related (dependent) type
        // (ProfileA.OwnerId) and the principal key on the declaring type (OwnerA.Id).
        hop.Direction.Should().Be(NavigationDirection.PrincipalToDependent);
        Assert.Equal(typeof(ProfileA).GetProperty(nameof(ProfileA.OwnerId)), hop.PrimaryLeg.ForeignKey.PropertyInfo);
        Assert.Equal(typeof(OwnerA).GetProperty(nameof(OwnerA.Id)), hop.PrimaryLeg.PrincipalKey.PropertyInfo);
    }

    [Fact]
    public void Resolve_Direction_MatchesForeignKeySidePerKind()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));
        Register(new EntityMetadataBuilder<ChildA>().HasOne(c => c.Parent, c => c.ParentId));
        Register(new EntityMetadataBuilder<OwnerA>()
            .HasOneToOne(o => o.Profile, o => o.Id, p => p.OwnerId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var c = Expression.Parameter(typeof(ChildA), "c");
        var o = Expression.Parameter(typeof(OwnerA), "o");

        // HasMany: the FK (ChildA.ParentId) is on the related side, so principal -> dependent.
        Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA))
            .Hops[0].Direction.Should().Be(NavigationDirection.PrincipalToDependent);

        // HasOne: the FK (ChildA.ParentId) is on the declaring side, so dependent -> principal.
        Resolve(Member(c, nameof(ChildA.Parent)), new object(), c, typeof(ChildA))
            .Hops[0].Direction.Should().Be(NavigationDirection.DependentToPrincipal);

        // HasOneToOne: the FK (ProfileA.OwnerId) is on the related side, so principal -> dependent.
        // This row fails on the previously inverted hardcoded DependentToPrincipal.
        Resolve(Member(o, nameof(OwnerA.Profile)), new object(), o, typeof(OwnerA))
            .Hops[0].Direction.Should().Be(NavigationDirection.PrincipalToDependent);
    }

    [Fact]
    public void Resolve_HasMany_ProducesPrincipalToDependentWithExactKeys()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var path = Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));
        var hop = path.Hops[0];

        hop.Kind.Should().Be(RelationshipKind.OneToMany);
        hop.IsCollection.Should().BeTrue();
        hop.Direction.Should().Be(NavigationDirection.PrincipalToDependent);
        Assert.Equal(typeof(ChildA).GetProperty(nameof(ChildA.ParentId)), hop.PrimaryLeg.ForeignKey.PropertyInfo);
        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Id)), hop.PrimaryLeg.PrincipalKey.PropertyInfo);
    }

    // ---------------------------------------------------------------------------------------------
    // Chains and collection position
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_ReferenceChain_ResolvesEachHopInOrder()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));
        Register(new EntityMetadataBuilder<ChildA>().HasOne(c => c.Parent, c => c.ParentId));

        var c = Expression.Parameter(typeof(ChildA), "c");
        var expression = Member(Member(c, nameof(ChildA.Parent)), nameof(ParentA.Children));
        var path = Resolve(expression, new object(), c, typeof(ChildA));

        path.Hops.Should().HaveCount(2);
        Assert.Equal(typeof(ChildA).GetProperty(nameof(ChildA.Parent)), path.Hops[0].Navigation);
        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Children)), path.Hops[1].Navigation);
        path.RootEntityType.Should().Be(typeof(ChildA));
    }

    [Fact]
    public void Resolve_FiniteSelfReferenceChain_ResolvesEachHopWithoutRecursion()
    {
        Register(new EntityMetadataBuilder<NodeA>()
            .HasOne(n => n.Parent, n => n.ParentId)
            .HasMany(n => n.Children, c => c.ParentId));

        var n = Expression.Parameter(typeof(NodeA), "n");
        var expression = Member(Member(Member(n, nameof(NodeA.Parent)), nameof(NodeA.Parent)), nameof(NodeA.Parent));
        var path = Resolve(expression, new object(), n, typeof(NodeA));

        path.Hops.Should().HaveCount(3);
        path.Hops.Should().OnlyContain(hop => hop.Navigation!.Name == nameof(NodeA.Parent));
        path.Hops.Should().OnlyContain(hop => hop.Kind == RelationshipKind.ManyToOne);
    }

    [Fact]
    public void Resolve_CollectionAsFinalHop_Succeeds()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var path = Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));

        path.FinalHop!.IsCollection.Should().BeTrue();
        path.FinalHop.Direction.Should().Be(NavigationDirection.PrincipalToDependent);
    }

    [Fact]
    public void Resolve_CollectionAsIntermediateHop_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var expression = Member(Member(p, nameof(ParentA.Children)), nameof(ICollection<ChildA>.Count));

        Action act = () => Resolve(expression, new object(), p, typeof(ParentA));

        act.Should().Throw<NotSupportedException>().WithMessage("*collection and cannot be an intermediate hop*");
    }

    [Fact]
    public void Resolve_RootOnlyParameter_ProducesEmptyPathWithNullFinalHop()
    {
        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");

        var path = Resolve(p, scopeId, p, typeof(ParentA));

        path.Hops.Should().BeEmpty();
        path.FinalHop.Should().BeNull();
        path.RootEntityType.Should().Be(typeof(ParentA));
        path.RootBinding.SourceIdentity.Should().BeSameAs(p);
    }

    // ---------------------------------------------------------------------------------------------
    // Many-to-many junctions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_ManyToMany_PreservesJunctionAndBothKeyLegs()
    {
        Register(new EntityMetadataBuilder<M2MParent>()
            .HasManyThrough<M2MChild, M2MLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        var p = Expression.Parameter(typeof(M2MParent), "p");
        var path = Resolve(Member(p, nameof(M2MParent.Children)), new object(), p, typeof(M2MParent));
        var hop = path.Hops[0];

        hop.Kind.Should().Be(RelationshipKind.ManyToMany);
        hop.IsManyToMany.Should().BeTrue();
        hop.IsCollection.Should().BeTrue();
        hop.Direction.Should().Be(NavigationDirection.ThroughJunction);
        hop.JunctionType.Should().Be(typeof(M2MLink));
        hop.JunctionMetadata.Should().NotBeNull();

        Assert.Equal(typeof(M2MLink).GetProperty(nameof(M2MLink.ChildId)), hop.PrimaryLeg.ForeignKey.PropertyInfo);
        Assert.Equal(typeof(M2MChild).GetProperty(nameof(M2MChild.Id)), hop.PrimaryLeg.PrincipalKey.PropertyInfo);
        hop.SecondaryLeg.Should().NotBeNull();
        Assert.Equal(typeof(M2MLink).GetProperty(nameof(M2MLink.ParentId)), hop.SecondaryLeg!.ForeignKey.PropertyInfo);
        Assert.Equal(typeof(M2MParent).GetProperty(nameof(M2MParent.Id)), hop.SecondaryLeg.PrincipalKey.PropertyInfo);
    }

    [Fact]
    public void Resolve_SameRelatedTypeThroughDifferentJunctions_PreservesBothAndDiffers()
    {
        Register(new EntityMetadataBuilder<DualJunctionParent>()
            .HasManyThrough<M2MChild, JunctionLinkA, int, int>(
                p => p.A, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasManyThrough<M2MChild, JunctionLinkB, int, int>(
                p => p.B, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        var p = Expression.Parameter(typeof(DualJunctionParent), "p");
        var scope = ScopeWith(new object(), p, typeof(DualJunctionParent));

        var first = NavigationPathResolver.Resolve(Member(p, nameof(DualJunctionParent.A)), scope);
        var second = NavigationPathResolver.Resolve(Member(p, nameof(DualJunctionParent.B)), scope);

        first.Hops[0].JunctionType.Should().Be(typeof(JunctionLinkA));
        second.Hops[0].JunctionType.Should().Be(typeof(JunctionLinkB));
        first.Hops[0].RelatedType.Should().Be(typeof(M2MChild));
        second.Hops[0].RelatedType.Should().Be(typeof(M2MChild));
        first.Hops[0].SameIdentity(second.Hops[0]).Should().BeFalse();
        first.Equals(second).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // Metadata precedence and cache ownership (P1)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_ConfiguredFluentMapping_WinsOverAttribute()
    {
        Register(new EntityMetadataBuilder<ClashParent>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ClashParent), "p");
        var path = Resolve(Member(p, nameof(ClashParent.Children)), new object(), p, typeof(ClashParent));

        // the configured fluent mapping wins over the [Relationship] attribute
        Assert.Equal(typeof(ClashChild).GetProperty(nameof(ClashChild.ParentId)),
            path.Hops[0].PrimaryLeg.ForeignKey.PropertyInfo);
    }

    [Fact]
    public void Resolve_DoesNotSeedOrOverwriteTheConfiguredMetadataCache()
    {
        var configured = Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));
        var countBefore = DataContextCache.Metadata.Count;

        var p = Expression.Parameter(typeof(ParentA), "p");
        var path = Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));

        path.Hops.Should().ContainSingle();
        DataContextCache.Metadata.Count.Should().Be(countBefore);
        DataContextCache.Metadata[typeof(ParentA)].Should().BeSameAs(configured);
        DataContextCache.Metadata.ContainsKey(typeof(ChildA)).Should().BeFalse(
            "the resolver auto-resolves related metadata into its own private cache, never the configured one");
        DataContextCache.TvpMetadata.ContainsKey(typeof(ChildA)).Should().BeTrue();
    }

    [Fact]
    public void Resolve_AutoResolvedTypes_AreNotWrittenToConfiguredCache()
    {
        Register(new EntityMetadataBuilder<M2MParent>()
            .HasManyThrough<M2MChild, M2MLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));

        var p = Expression.Parameter(typeof(M2MParent), "p");
        _ = Resolve(Member(p, nameof(M2MParent.Children)), new object(), p, typeof(M2MParent));

        DataContextCache.Metadata.ContainsKey(typeof(M2MChild)).Should().BeFalse();
        DataContextCache.Metadata.ContainsKey(typeof(M2MLink)).Should().BeFalse();
        DataContextCache.AutoPublishedJunctionMetadata.Should().BeEmpty(
            "the resolver never auto-publishes a junction into the configured cache");
    }

    // ---------------------------------------------------------------------------------------------
    // Member identity: undeclared, hidden and wrong declaring type
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_UndeclaredMember_Throws()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        Action act = () => Resolve(Member(p, nameof(ParentA.Name)), new object(), p, typeof(ParentA));

        act.Should().Throw<NotSupportedException>().WithMessage("*not a declared navigation relationship*");
    }

    [Fact]
    public void Resolve_HiddenSameNameMember_Throws()
    {
        DataContextCache.Metadata[typeof(HiddenDerived)] = new EntityMetadataBuilder<HiddenDerived>()
            .HasOne<HiddenChild, int>(d => ((HiddenBase)d).Nav, d => d.DerivedId)
            .Build();

        var d = Expression.Parameter(typeof(HiddenDerived), "d");
        var hidden = typeof(HiddenDerived).GetProperty(nameof(HiddenDerived.Nav))!;

        Action act = () => Resolve(Expression.Property(d, hidden), new object(), d, typeof(HiddenDerived));

        act.Should().Throw<NotSupportedException>().WithMessage("*not a declared navigation relationship*");
    }

    [Fact]
    public void Resolve_HiddenMember_DeclaredBaseMemberStillResolves()
    {
        DataContextCache.Metadata[typeof(HiddenDerived)] = new EntityMetadataBuilder<HiddenDerived>()
            .HasOne<HiddenChild, int>(d => ((HiddenBase)d).Nav, d => d.DerivedId)
            .Build();

        var d = Expression.Parameter(typeof(HiddenDerived), "d");
        var declared = typeof(HiddenBase).GetProperty(nameof(HiddenBase.Nav))!;
        var path = Resolve(Expression.Property(d, declared), new object(), d, typeof(HiddenDerived));

        path.Hops[0].Navigation.Should().BeSameAs(declared);
    }

    // ---------------------------------------------------------------------------------------------
    // Key resolution failures
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_MissingPrincipalKey_Throws()
    {
        Register(new EntityMetadataBuilder<KeylessParent>().HasMany(p => p.Children, c => c.ParentId));
        Register(new EntityMetadataBuilder<KeylessChild>());

        var p = Expression.Parameter(typeof(KeylessParent), "p");
        Action act = () => Resolve(Member(p, nameof(KeylessParent.Children)), new object(), p, typeof(KeylessParent));

        act.Should().Throw<NotSupportedException>().WithMessage("*no principal key*");
    }

    [Fact]
    public void Resolve_UnmappedForeignKey_Throws()
    {
        Register(new EntityMetadataBuilder<NarrowParent>().HasMany(p => p.Children, c => c.ParentId));
        var narrowBuilder = new EntityMetadataBuilder<NarrowChild>();
        _ = narrowBuilder.Property(c => c.Id);
        DataContextCache.Metadata[typeof(NarrowChild)] = narrowBuilder.Build();

        var p = Expression.Parameter(typeof(NarrowParent), "p");
        Action act = () => Resolve(Member(p, nameof(NarrowParent.Children)), new object(), p, typeof(NarrowParent));

        act.Should().Throw<NotSupportedException>().WithMessage("*not mapped on*");
    }

    // The composite-key fail-closed boundary (RequireSingleKey and the many-to-many junction check) is
    // unreachable through the real metadata model, which exposes single-column keys only
    // (RelationshipMetadata's constructor takes a single PropertyInfo and ResolveKeys builds
    // one-element lists). It is recorded as a guard, not forced through a parallel fake relationship
    // system: NavigationPathResolver.RequireSingleKey (NavigationPathResolver.cs:290-295) rejects
    // ForeignKey.Count != 1 || PrincipalKey.Count != 1, and ResolveJunctionHop
    // (NavigationPathResolver.cs:260-267) rejects a composite junction key.

    // ---------------------------------------------------------------------------------------------
    // Conversions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_IdentityConversion_IsAccepted()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var children = Member(p, nameof(ParentA.Children));
        var path = Resolve(Expression.Convert(children, children.Type), new object(), p, typeof(ParentA));

        Assert.Equal(typeof(ParentA).GetProperty(nameof(ParentA.Children)), path.Hops[0].Navigation);
    }

    [Fact]
    public void Resolve_SafeReferenceUpcast_IsAcceptedAndPreservesDeclaredMember()
    {
        DataContextCache.Metadata[typeof(ConvDerived)] = new EntityMetadataBuilder<ConvDerived>()
            .HasOne<ConvChild, int>(d => ((ConvBase)d).Child, d => d.ChildId)
            .Build();
        Register(new EntityMetadataBuilder<ConvChild>());

        var d = Expression.Parameter(typeof(ConvDerived), "d");
        var declared = typeof(ConvBase).GetProperty(nameof(ConvBase.Child))!;
        var expression = Expression.Property(Expression.Convert(d, typeof(ConvBase)), declared);
        var path = Resolve(expression, new object(), d, typeof(ConvDerived));

        path.Hops[0].Navigation.Should().BeSameAs(declared);
        path.Hops[0].DeclaringType.Should().Be(typeof(ConvDerived));
    }

    [Fact]
    public void Resolve_Downcast_Throws()
    {
        var b = Expression.Parameter(typeof(ConvBase), "b");
        var derived = typeof(ConvDerived).GetProperty(nameof(ConvBase.Child))!;
        var expression = Expression.Property(Expression.Convert(b, typeof(ConvDerived)), derived);

        Action act = () => Resolve(expression, new object(), b, typeof(ConvBase));

        act.Should().Throw<NotSupportedException>().WithMessage("*not an identity or reference upcast*");
    }

    [Fact]
    public void Resolve_UserDefinedConversion_Throws()
    {
        var e = Expression.Parameter(typeof(ConvertibleEntity), "e");
        var expression = Expression.Convert(e, typeof(ConversionTarget));

        Action act = () => Resolve(expression, new object(), e, typeof(ConvertibleEntity));

        act.Should().Throw<NotSupportedException>().WithMessage("*user-defined conversion*");
    }

    [Fact]
    public void Resolve_BoxedValueConversion_Throws()
    {
        var expression = Expression.Convert(Expression.Constant(1), typeof(object));

        Action act = () => NavigationPathResolver.Resolve(expression, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*not an identity or reference upcast*");
    }

    [Fact]
    public void Resolve_ValueTypeRootWithMember_Throws()
    {
        var v = Expression.Parameter(typeof(ValueEntity), "v");
        var expression = Expression.Property(v, nameof(ValueEntity.Id));

        Action act = () => Resolve(expression, new object(), v, typeof(ValueEntity));

        act.Should().Throw<NotSupportedException>().WithMessage("*not a declared navigation relationship*");
    }

    // ---------------------------------------------------------------------------------------------
    // Non-parameter roots and unsupported operand shapes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_CapturedConstantRoot_Throws()
    {
        Action act = () => NavigationPathResolver.Resolve(Expression.Constant(new ParentA()), AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*captured or constant navigation root*");
    }

    [Fact]
    public void Resolve_ConstantEnumerableRoot_Throws()
    {
        Action act = () => NavigationPathResolver.Resolve(Expression.Constant(new List<ChildA>()), AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*captured or constant navigation root*");
    }

    [Fact]
    public void Resolve_StaticPropertyRoot_Throws()
    {
        var staticRoot = typeof(NavigationPathResolverTests).GetProperty(nameof(StaticRoot))!;
        var expression = Expression.Property(null, staticRoot);

        Action act = () => NavigationPathResolver.Resolve(expression, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*static property*");
    }

    [Fact]
    public void Resolve_DefaultExpressionRoot_Throws()
    {
        Action act = () => NavigationPathResolver.Resolve(Expression.Default(typeof(ParentA)), AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*not supported*");
    }

    [Fact]
    public void Resolve_NullArgument_ThrowsArgumentNull()
    {
        var p = Expression.Parameter(typeof(ParentA), "p");
        var scope = ScopeWith(new object(), p, typeof(ParentA));

        Action nullPath = () => NavigationPathResolver.Resolve(null!, scope);
        Action nullScope = () => NavigationPathResolver.Resolve(Member(p, nameof(ParentA.Children)), null!);

        nullPath.Should().Throw<ArgumentNullException>();
        nullScope.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_WhereCallInOperand_Throws()
    {
        Expression<Func<ParentA, IEnumerable<ChildA>>> expression = p => p.Children.Where(c => c.Id > 0);

        Action act = () => NavigationPathResolver.Resolve(expression.Body, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*method call 'Where'*");
    }

    [Fact]
    public void Resolve_SelectCallInOperand_Throws()
    {
        Expression<Func<ParentA, IEnumerable<int>>> expression = p => p.Children.Select(c => c.Id);

        Action act = () => NavigationPathResolver.Resolve(expression.Body, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*method call 'Select'*");
    }

    [Fact]
    public void Resolve_ToListCallInOperand_Throws()
    {
        Expression<Func<ParentA, List<ChildA>>> expression = p => p.Children.ToList();

        Action act = () => NavigationPathResolver.Resolve(expression.Body, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*method call 'ToList'*");
    }

    [Fact]
    public void Resolve_ArbitraryMethodCallInOperand_Throws()
    {
        Expression<Func<ParentA, IEnumerable<ChildA>>> expression = p => ArbitraryCall(p.Children);

        Action act = () => NavigationPathResolver.Resolve(expression.Body, AnyScope());

        act.Should().Throw<NotSupportedException>().WithMessage("*method call 'ArbitraryCall'*");
    }

    // ---------------------------------------------------------------------------------------------
    // Metadata-only resolution (no runtime value reads)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_ThrowingGetters_AreNeverRead()
    {
        GetterProbe.Reset();
        Register(new EntityMetadataBuilder<ThrowingParent>().HasMany(p => p.Children, c => c.ParentId));
        Register(new EntityMetadataBuilder<ThrowingChild>());

        var p = Expression.Parameter(typeof(ThrowingParent), "p");
        var path = Resolve(Member(p, nameof(ThrowingParent.Children)), new object(), p, typeof(ThrowingParent));

        Assert.Equal(typeof(ThrowingChild).GetProperty(nameof(ThrowingChild.ParentId)),
            path.Hops[0].PrimaryLeg.ForeignKey.PropertyInfo);
        GetterProbe.Count.Should().Be(0, "resolution is metadata-only and never reads a runtime getter");
    }

    // ---------------------------------------------------------------------------------------------
    // Immutability and concurrency
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_MutatingInputCollectionAfterResolution_DoesNotChangeTheSnapshot()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var binding = new NavigationSourceBinding(scopeId, p, p, typeof(ParentA));
        var roots = new List<NavigationSourceBinding> { binding };
        var scope = new NavigationResolutionScope(scopeId, roots);
        var expression = Member(p, nameof(ParentA.Children));

        var path = NavigationPathResolver.Resolve(expression, scope);
        roots.Clear();

        scope.VisibleRoots.Should().ContainSingle();
        path.Hops.Should().ContainSingle();
        path.RootBinding.Should().BeSameAs(binding);

        var again = NavigationPathResolver.Resolve(expression, scope);
        again.Equals(path).Should().BeTrue();
    }

    [Fact]
    public void Resolve_PublishedCollections_AreTrulyImmutableSnapshots()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var binding = new NavigationSourceBinding(scopeId, p, p, typeof(ParentA));
        var scope = new NavigationResolutionScope(scopeId, new List<NavigationSourceBinding> { binding });
        var path = NavigationPathResolver.Resolve(Member(p, nameof(ParentA.Children)), scope);

        // The published collections are not a mutable List<T> and cannot be downcast to one.
        (path.Hops as List<ResolvedNavigationHop>).Should().BeNull();
        (scope.VisibleRoots as List<NavigationSourceBinding>).Should().BeNull();

        var hops = (IList<ResolvedNavigationHop>)path.Hops;
        var roots = (IList<NavigationSourceBinding>)scope.VisibleRoots;
        hops.IsReadOnly.Should().BeTrue();
        roots.IsReadOnly.Should().BeTrue();

        var firstHop = path.Hops[0];
        var firstRoot = scope.VisibleRoots[0];

        Action addHop = () => hops.Add(firstHop);
        Action setHop = () => hops[0] = firstHop;
        Action addRoot = () => roots.Add(binding);
        Action setRoot = () => roots[0] = binding;

        addHop.Should().Throw<NotSupportedException>();
        setHop.Should().Throw<NotSupportedException>();
        addRoot.Should().Throw<NotSupportedException>();
        setRoot.Should().Throw<NotSupportedException>();

        // The rejected mutations left the snapshots unchanged.
        path.Hops.Should().HaveCount(1);
        path.Hops[0].Should().BeSameAs(firstHop);
        scope.VisibleRoots.Should().HaveCount(1);
        scope.VisibleRoots[0].Should().BeSameAs(firstRoot);
    }

    [Fact]
    public void Resolve_PathEquality_CoversNullReferenceAndCountBranches()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var childrenPath = Member(p, nameof(ParentA.Children));
        var self = Resolve(childrenPath, scopeId, p, typeof(ParentA));
        var nullProbe = Resolve(childrenPath, scopeId, p, typeof(ParentA));
        var foreignProbe = Resolve(childrenPath, scopeId, p, typeof(ParentA));
        var withHops = Resolve(childrenPath, scopeId, p, typeof(ParentA));
        var withoutHops = Resolve(p, scopeId, p, typeof(ParentA));

        // Reference-equality branch.
        self.Equals(self).Should().BeTrue();

        // A null argument is not equal and must not throw (the null branch of Equals).
        nullProbe.Equals((ResolvedNavigationPath?)null).Should().BeFalse();

        // A foreign type routes through `as` and is not equal.
        foreignProbe.Equals(new object()).Should().BeFalse();

        // Same scope identity and root binding, different hop count.
        withHops.Equals(withoutHops).Should().BeFalse();
        withoutHops.Equals(withHops).Should().BeFalse();
    }

    [Fact]
    public void Resolve_RepeatedAndConcurrent_NoSharedMutableState()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var scopeId = new object();
        var p = Expression.Parameter(typeof(ParentA), "p");
        var scope = ScopeWith(scopeId, p, typeof(ParentA));
        var expression = Member(p, nameof(ParentA.Children));
        var expected = NavigationPathResolver.Resolve(expression, scope);

        var observed = new ConcurrentBag<ResolvedNavigationPath>();
        var failures = new ConcurrentQueue<string>();

        Parallel.For(0, 2_000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            var path = NavigationPathResolver.Resolve(expression, scope);
            observed.Add(path);
            if (!expected.Equals(path))
                failures.Enqueue("resolved path identity diverged under concurrency");
        });

        failures.Should().BeEmpty();
        observed.Should().OnlyContain(path => path.Equals(expected));
    }

    // ---------------------------------------------------------------------------------------------
    // Class root resolution
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_ClassRoot_Resolves()
    {
        Register(new EntityMetadataBuilder<ParentA>().HasMany(p => p.Children, c => c.ParentId));

        var p = Expression.Parameter(typeof(ParentA), "p");
        var path = Resolve(Member(p, nameof(ParentA.Children)), new object(), p, typeof(ParentA));

        path.RootEntityType.Should().Be(typeof(ParentA));
        path.FinalHop.Should().NotBeNull();
        path.FinalHop!.RelatedType.Should().Be(typeof(ChildA));
    }
}
