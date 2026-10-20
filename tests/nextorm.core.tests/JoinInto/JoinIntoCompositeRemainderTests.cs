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
    // D223 TASK 1: plan §4 execution-path variant matrix closure. Every plan-mandated arity/key
    // representation row is asserted here (in-memory) or in the SQLite carrier probe (SQL execution):
    // junction FK arity 1/2, 2/2, 1/3 and 3/3 with last-component collision data (no silent two-column
    // limit) and a mapped reference-type (string) junction key. Nullable junction components are closed
    // in <see cref="JoinIntoCompositeNullSemanticsTests"/>.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Declares a many-to-many relationship with an arbitrary parent/child key arity and junction
    /// foreign-key arity, so the composite-junction carrier can be exercised at shapes the (single-column
    /// only) fluent surface cannot express. Mirrors the SQLite probe registration.
    /// </summary>
    private static void RegisterManyToManyJunction<TEntity, TChild, TJunction>(
        string navigationName,
        IReadOnlyList<string> parentKeyNames,
        IReadOnlyList<string> childKeyNames,
        IReadOnlyList<string> junctionParentForeignKeyNames,
        IReadOnlyList<string> junctionChildForeignKeyNames)
    {
        var parentProps = new EntityMetadataBuilder<TEntity>().Build().Properties;
        var childProps = new EntityMetadataBuilder<TChild>().Build().Properties;
        var linkProps = new EntityMetadataBuilder<TJunction>().Build().Properties;
        var navigation = typeof(TEntity).GetProperty(navigationName);
        var parentKeys = parentKeyNames.Select(name => Prop(parentProps, name).PropertyInfo).ToArray();
        var childKeys = childKeyNames.Select(name => Prop(childProps, name).PropertyInfo).ToArray();
        var junctionParentForeignKeys = junctionParentForeignKeyNames.Select(name => Prop(linkProps, name).PropertyInfo).ToArray();
        var junctionChildForeignKeys = junctionChildForeignKeyNames.Select(name => Prop(linkProps, name).PropertyInfo).ToArray();

        DataContextCache.Metadata[typeof(TEntity)] = new EntityMetadata(
            typeof(TEntity).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(TEntity),
                    typeof(TChild),
                    navigation,
                    isCollection: true,
                    childKeys[0],
                    parentKeys[0],
                    typeof(TChild),
                    typeof(TEntity),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(TJunction),
                        ParentKey = parentKeys,
                        ChildKey = childKeys,
                        JunctionParentForeignKey = junctionParentForeignKeys,
                        JunctionChildForeignKey = junctionChildForeignKeys,
                    }),
            ]);
    }

    // --- arity 1/2: one parent component, two child components --------------------------------

    public sealed class Arity12Parent
    {
        public int Id { get; set; }
        public ICollection<Arity12Child> Children { get; set; } = new List<Arity12Child>();
    }

    public sealed class Arity12Child
    {
        public int Pod { get; set; }
        public int Id { get; set; }
    }

    public sealed class Arity12Link
    {
        public int ParentId { get; set; }
        public int ChildPod { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void CompositeJunction_ArityOneTwo_ShouldMatchEveryChildComponent()
    {
        DataContextCache.Clear();
        RegisterManyToManyJunction<Arity12Parent, Arity12Child, Arity12Link>(
            nameof(Arity12Parent.Children),
            [nameof(Arity12Parent.Id)],
            [nameof(Arity12Child.Pod), nameof(Arity12Child.Id)],
            [nameof(Arity12Link.ParentId)],
            [nameof(Arity12Link.ChildPod), nameof(Arity12Link.ChildId)]);

        using var ctx = new InMemoryDataContext();
        ctx.From<Arity12Parent>().WithData([new Arity12Parent { Id = 1 }]);
        ctx.From<Arity12Child>().WithData([
            new Arity12Child { Pod = 1, Id = 10 },
            // Shares only the first child component: dropping the last component would attach this too.
            new Arity12Child { Pod = 1, Id = 20 },
        ]);
        ctx.From<Arity12Link>().WithData([new Arity12Link { ParentId = 1, ChildPod = 1, ChildId = 10 }]);

        var parents = ctx.From<Arity12Parent>()
            .JoinInto(ctx.From<Arity12Child>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 },
            "the junction's last child component is part of the match; child 20 shares only the first");
    }

    // --- arity 2/2: two parent components, two child components --------------------------------

    public sealed class Arity22Parent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<Arity22Child> Children { get; set; } = new List<Arity22Child>();
    }

    public sealed class Arity22Child
    {
        public int Pod { get; set; }
        public int Id { get; set; }
    }

    public sealed class Arity22Link
    {
        public int ParentTenant { get; set; }
        public int ParentId { get; set; }
        public int ChildPod { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void CompositeJunction_ArityTwoTwo_ShouldMatchEveryParentAndChildComponent()
    {
        DataContextCache.Clear();
        RegisterManyToManyJunction<Arity22Parent, Arity22Child, Arity22Link>(
            nameof(Arity22Parent.Children),
            [nameof(Arity22Parent.TenantId), nameof(Arity22Parent.Id)],
            [nameof(Arity22Child.Pod), nameof(Arity22Child.Id)],
            [nameof(Arity22Link.ParentTenant), nameof(Arity22Link.ParentId)],
            [nameof(Arity22Link.ChildPod), nameof(Arity22Link.ChildId)]);

        using var ctx = new InMemoryDataContext();
        ctx.From<Arity22Parent>().WithData([
            new Arity22Parent { TenantId = 1, Id = 1 },
            new Arity22Parent { TenantId = 1, Id = 2 },
        ]);
        ctx.From<Arity22Child>().WithData([
            new Arity22Child { Pod = 1, Id = 10 },
            new Arity22Child { Pod = 1, Id = 20 },
        ]);
        // Both links share the parent's first component (1); only the last parent component routes them.
        ctx.From<Arity22Link>().WithData([
            new Arity22Link { ParentTenant = 1, ParentId = 1, ChildPod = 1, ChildId = 10 },
            new Arity22Link { ParentTenant = 1, ParentId = 2, ChildPod = 1, ChildId = 20 },
        ]);

        var parents = ctx.From<Arity22Parent>()
            .JoinInto(ctx.From<Arity22Child>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2, "the two composite-key parents are distinct");
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10 },
            "the junction's last parent component routes child 10 to parent (1,1) only");
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(new[] { 20 },
            "the junction's last parent component routes child 20 to parent (1,2) only");
    }

    // --- arity 1/3: one parent component, three child components --------------------------------

    public sealed class Arity13Parent
    {
        public int Id { get; set; }
        public ICollection<Arity13Child> Children { get; set; } = new List<Arity13Child>();
    }

    public sealed class Arity13Child
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    public sealed class Arity13Link
    {
        public int ParentId { get; set; }
        public int ChildA { get; set; }
        public int ChildB { get; set; }
        public int ChildC { get; set; }
    }

    [Fact]
    public void CompositeJunction_ArityOneThree_ShouldMatchEveryChildComponent()
    {
        DataContextCache.Clear();
        RegisterManyToManyJunction<Arity13Parent, Arity13Child, Arity13Link>(
            nameof(Arity13Parent.Children),
            [nameof(Arity13Parent.Id)],
            [nameof(Arity13Child.A), nameof(Arity13Child.B), nameof(Arity13Child.C)],
            [nameof(Arity13Link.ParentId)],
            [nameof(Arity13Link.ChildA), nameof(Arity13Link.ChildB), nameof(Arity13Link.ChildC)]);

        using var ctx = new InMemoryDataContext();
        ctx.From<Arity13Parent>().WithData([new Arity13Parent { Id = 1 }]);
        ctx.From<Arity13Child>().WithData([
            new Arity13Child { A = 1, B = 1, C = 10 },
            // Same first two child components; only the third tells them apart (no two-column limit).
            new Arity13Child { A = 1, B = 1, C = 20 },
        ]);
        ctx.From<Arity13Link>().WithData([new Arity13Link { ParentId = 1, ChildA = 1, ChildB = 1, ChildC = 10 }]);

        var parents = ctx.From<Arity13Parent>()
            .JoinInto(ctx.From<Arity13Child>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.C).Should().Equal(new[] { 10 },
            "the third child component is part of the match; child 20 shares only the first two");
    }

    // --- arity 3/3: three parent and three child components -------------------------------------

    public sealed class Arity33Parent
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public ICollection<Arity33Child> Children { get; set; } = new List<Arity33Child>();
    }

    public sealed class Arity33Child
    {
        public int D { get; set; }
        public int E { get; set; }
        public int F { get; set; }
    }

    public sealed class Arity33Link
    {
        public int ParentA { get; set; }
        public int ParentB { get; set; }
        public int ParentC { get; set; }
        public int ChildD { get; set; }
        public int ChildE { get; set; }
        public int ChildF { get; set; }
    }

    [Fact]
    public void CompositeJunction_ArityThreeThree_ShouldMatchEveryParentAndChildComponent()
    {
        DataContextCache.Clear();
        RegisterManyToManyJunction<Arity33Parent, Arity33Child, Arity33Link>(
            nameof(Arity33Parent.Children),
            [nameof(Arity33Parent.A), nameof(Arity33Parent.B), nameof(Arity33Parent.C)],
            [nameof(Arity33Child.D), nameof(Arity33Child.E), nameof(Arity33Child.F)],
            [nameof(Arity33Link.ParentA), nameof(Arity33Link.ParentB), nameof(Arity33Link.ParentC)],
            [nameof(Arity33Link.ChildD), nameof(Arity33Link.ChildE), nameof(Arity33Link.ChildF)]);

        using var ctx = new InMemoryDataContext();
        ctx.From<Arity33Parent>().WithData([
            new Arity33Parent { A = 1, B = 1, C = 1 },
            new Arity33Parent { A = 1, B = 1, C = 2 },
        ]);
        ctx.From<Arity33Child>().WithData([
            new Arity33Child { D = 1, E = 1, F = 10 },
            new Arity33Child { D = 1, E = 1, F = 20 },
        ]);
        // The parents share their first two components; the third (and the child's third) disambiguates.
        ctx.From<Arity33Link>().WithData([
            new Arity33Link { ParentA = 1, ParentB = 1, ParentC = 1, ChildD = 1, ChildE = 1, ChildF = 10 },
            new Arity33Link { ParentA = 1, ParentB = 1, ParentC = 2, ChildD = 1, ChildE = 1, ChildF = 20 },
        ]);

        var parents = ctx.From<Arity33Parent>()
            .JoinInto(ctx.From<Arity33Child>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2, "the arity-general carrier must not impose a two-component maximum");
        parents.Single(p => p.C == 1).Children.Select(c => c.F).Should().Equal(new[] { 10 },
            "the third parent and child components route child 10 to parent (1,1,1) only");
        parents.Single(p => p.C == 2).Children.Select(c => c.F).Should().Equal(new[] { 20 },
            "the third parent and child components route child 20 to parent (1,1,2) only");
    }

    // --- key representation: mapped reference type (string) -------------------------------------

    public sealed class StringKeyParent
    {
        public string Code { get; set; } = string.Empty;
        public ICollection<StringKeyChild> Children { get; set; } = new List<StringKeyChild>();
    }

    public sealed class StringKeyChild
    {
        public int Id { get; set; }
    }

    public sealed class StringKeyLink
    {
        public string ParentCode { get; set; } = string.Empty;
        public int ChildId { get; set; }
    }

    [Fact]
    public void CompositeJunction_StringParentKey_ShouldStitchByReferenceKeyValue()
    {
        DataContextCache.Clear();
        RegisterManyToManyJunction<StringKeyParent, StringKeyChild, StringKeyLink>(
            nameof(StringKeyParent.Children),
            [nameof(StringKeyParent.Code)],
            [nameof(StringKeyChild.Id)],
            [nameof(StringKeyLink.ParentCode)],
            [nameof(StringKeyLink.ChildId)]);

        using var ctx = new InMemoryDataContext();
        ctx.From<StringKeyParent>().WithData([
            new StringKeyParent { Code = "P1" },
            new StringKeyParent { Code = "P2" },
        ]);
        ctx.From<StringKeyChild>().WithData([new StringKeyChild { Id = 10 }]);
        ctx.From<StringKeyLink>().WithData([new StringKeyLink { ParentCode = "P1", ChildId = 10 }]);

        var parents = ctx.From<StringKeyParent>()
            .JoinInto(ctx.From<StringKeyChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Code == "P1").Children.Select(c => c.Id).Should().Equal(new[] { 10 },
            "the string junction key matches by value, not by reference");
        parents.Single(p => p.Code == "P2").Children.Should().BeEmpty("only the declared string key matches");
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

    public sealed class KeylessParentRemainder
    {
        // Deliberately no mapped key: the identity selector returns null and the stitcher must fall back
        // to reference identity (#105), never to a full-key match.
        public string Name { get; set; } = "";
        public ICollection<KeylessParentRemainderChild> Children { get; set; } = new List<KeylessParentRemainderChild>();
    }

    public sealed class KeylessParentRemainderChild
    {
        public int Id { get; set; }
        public string ParentName { get; set; } = "";
    }

    [Fact]
    public void KeylessParent_JoinInto_ShouldKeepReferenceIdentity_NotFullKeyMatching()
    {
        // A keyless PARENT must behave as before: the stitcher keeps the #105 reference-identity fallback,
        // so two distinct parent instances carrying equal payload stay two parents. Applying the composite
        // full-key path to a keyless parent would merge them (or throw), which is the regression this
        // guards. This test reddens if the full-key path is routed through a keyless parent.
        using var ctx = new InMemoryDataContext();
        ctx.From<KeylessParentRemainder>().WithData([
            new KeylessParentRemainder { Name = "dup" },
            new KeylessParentRemainder { Name = "dup" },
        ]);
        ctx.From<KeylessParentRemainderChild>().WithData([
            new KeylessParentRemainderChild { Id = 10, ParentName = "dup" },
            new KeylessParentRemainderChild { Id = 11, ParentName = "dup" },
        ]);

        // The explicit principal/foreign-key selectors supply the join predicate; the parent itself stays
        // keyless, so the stitcher's identity selector is null and only reference identity remains.
        var parents = ctx.From<KeylessParentRemainder>()
            .JoinInto(
                ctx.From<KeylessParentRemainderChild>(),
                (p, c) => p.Name == c.ParentName,
                p => p.Children,
                p => p.Name,
                c => c.ParentName)
            .ToList();

        parents.Should().HaveCount(2,
            "distinct keyless parent instances keep reference identity; a full-key match would merge them");
        parents.Should().OnlyContain(p => p.Children.Select(c => c.Id).OrderBy(id => id).SequenceEqual(new[] { 10, 11 }),
            "each keyless parent instance still receives both matching children");
    }
}
