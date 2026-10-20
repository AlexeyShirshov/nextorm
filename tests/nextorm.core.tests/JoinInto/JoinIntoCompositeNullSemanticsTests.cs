using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D07 (r=5) composite-key NULL / absent-child semantics (#213, variant-closure row
/// "default/nullable values"). A composite relationship key whose component is <c>null</c> is matched
/// in memory (<see cref="JoinIntoKey"/> compares the boxed components with <c>null == null</c>), and a
/// LEFT join with no matching child leaves the one-to-one reference <c>null</c> and the collection
/// empty — never a default-substituted child.
/// <para>
/// Known limitation (documented deliberately, not silently left uncovered): SQL evaluates
/// <c>NULL = NULL</c> as <c>false</c>, so the same composite key with a NULL component would <b>not</b>
/// match a SQL provider. In-memory therefore matches where the SQL path does not; the intent is pinned
/// here and in the provider SQL-generation tests so the divergence is a recorded limitation rather than
/// an unnoticed defect.
/// </para>
/// </summary>
public class JoinIntoCompositeNullSemanticsTests
{
    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    private static void RegisterParent<T>(PropertyInfo? navigation, IRelationshipMetadata relationship)
    {
        var properties = new EntityMetadataBuilder<T>().Build().Properties
            .Where(p => navigation is null || p.PropertyInfo != navigation)
            .ToList();

        DataContextCache.Metadata[typeof(T)] =
            new EntityMetadata(typeof(T).Name, properties, relationships: [relationship]);
    }

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
    // A composite key with a NULL component.
    // ---------------------------------------------------------------------------------------------

    public sealed class NullKeyParent
    {
        public int? TenantId { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<NullKeyChild> Children { get; set; } = new List<NullKeyChild>();
    }

    public sealed class NullKeyChild
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int ParentId { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void CompositeKey_NullComponent_InMemoryMatchesNullEqualsNull_DivergesFromSql()
    {
        var parentProps = Props<NullKeyParent>();
        var childProps = Props<NullKeyChild>();
        var navigation = typeof(NullKeyParent).GetProperty(nameof(NullKeyParent.Children));
        RegisterParent<NullKeyParent>(navigation, new InjectedRelationship(
            RelationshipKind.OneToMany,
            typeof(NullKeyParent),
            typeof(NullKeyChild),
            navigation,
            isCollection: true,
            foreignKey: [Prop(childProps, nameof(NullKeyChild.TenantId)), Prop(childProps, nameof(NullKeyChild.ParentId))],
            principalKey: [Prop(parentProps, nameof(NullKeyParent.TenantId)), Prop(parentProps, nameof(NullKeyParent.Id))]));

        using var ctx = new InMemoryDataContext();
        ctx.From<NullKeyParent>().WithData([
            new NullKeyParent { TenantId = null, Id = 1, Name = "p" },
        ]);
        ctx.From<NullKeyChild>().WithData([
            // Both components null / equal: in memory null == null is a match.
            new NullKeyChild { TenantId = null, ParentId = 1, Id = 10, Name = "null-match" },
            // A non-null tenant must still be rejected: only the complete composite key matches.
            new NullKeyChild { TenantId = 7, ParentId = 1, Id = 20, Name = "non-null" },
        ]);

        var parents = ctx.From<NullKeyParent>()
            .JoinInto(
                ctx.From<NullKeyChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        // KNOWN LIMITATION: this match is in-memory-only. A SQL provider evaluates NULL = NULL as false,
        // so this row would not be stitched there; the SQL-generation tests document the same divergence.
        parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 },
            "in memory a NULL composite component matches NULL (null == null); the SQL path does not");
    }

    // ---------------------------------------------------------------------------------------------
    // An absent LEFT child (all child columns NULL) must be null, never a default-substituted child.
    // ---------------------------------------------------------------------------------------------

    public sealed class AbsentParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        public AbsentChild? Profile { get; set; }
        public ICollection<AbsentChild> Children { get; set; } = new List<AbsentChild>();
    }

    public sealed class AbsentChild
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int ParentId { get; set; }
        public string? Name { get; set; }
    }

    private static void RegisterAbsentParent(PropertyInfo? navigation, RelationshipKind kind, bool isCollection)
    {
        var parentProps = Props<AbsentParent>();
        var childProps = Props<AbsentChild>();
        RegisterParent<AbsentParent>(navigation, new InjectedRelationship(
            kind,
            typeof(AbsentParent),
            typeof(AbsentChild),
            navigation,
            isCollection,
            foreignKey: [Prop(childProps, nameof(AbsentChild.TenantId)), Prop(childProps, nameof(AbsentChild.ParentId))],
            principalKey: [Prop(parentProps, nameof(AbsentParent.TenantId)), Prop(parentProps, nameof(AbsentParent.Id))]));
    }

    [Fact]
    public void CompositeOneToOne_AbsentLeftChild_ShouldBeNullNotDefault()
    {
        var navigation = typeof(AbsentParent).GetProperty(nameof(AbsentParent.Profile));
        RegisterAbsentParent(navigation, RelationshipKind.OneToOne, isCollection: false);

        using var ctx = new InMemoryDataContext();
        ctx.From<AbsentParent>().WithData([
            new AbsentParent { TenantId = 1, Id = 1, Name = "no-profile" },
        ]);
        // No child rows at all: the LEFT join has nothing to attach.

        var parents = ctx.From<AbsentParent>()
            .JoinInto(
                ctx.From<AbsentChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Profile)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Profile.Should().BeNull(
            "an absent LEFT child (all columns NULL) maps to null, not a default child instance");
    }

    [Fact]
    public void CompositeOneToMany_AbsentLeftChild_ShouldBeEmpty()
    {
        var navigation = typeof(AbsentParent).GetProperty(nameof(AbsentParent.Children));
        RegisterAbsentParent(navigation, RelationshipKind.OneToMany, isCollection: true);

        using var ctx = new InMemoryDataContext();
        ctx.From<AbsentParent>().WithData([
            new AbsentParent { TenantId = 1, Id = 1, Name = "no-children" },
        ]);

        var parents = ctx.From<AbsentParent>()
            .JoinInto(
                ctx.From<AbsentChild>(),
                (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId,
                p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().BeEmpty("an absent LEFT child contributes no element");
    }

    // ---------------------------------------------------------------------------------------------
    // D223 TASK 1: a dedicated many-to-many junction nullable component. AC05 requires a null
    // component never to match a valid (non-null) component; the junction's complete composite key is
    // what scopes the association, so a non-null junction component must not attach to a parent whose
    // corresponding component is null (and vice versa). The SQL half lives in
    // tests/nextorm.sqlite.tests/Joins/JoinIntoCompositeCarrierSpikeProbeTests.cs.
    // ---------------------------------------------------------------------------------------------

    public sealed class NullableJunctionParent
    {
        public int? TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<NullableJunctionChild> Children { get; set; } = new List<NullableJunctionChild>();
    }

    public sealed class NullableJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class NullableJunctionLink
    {
        public int? ParentTenant { get; set; }
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    private static void RegisterNullableJunction()
    {
        var parentProps = Props<NullableJunctionParent>();
        var childProps = Props<NullableJunctionChild>();
        var linkProps = Props<NullableJunctionLink>();
        var navigation = typeof(NullableJunctionParent).GetProperty(nameof(NullableJunctionParent.Children));

        DataContextCache.Metadata[typeof(NullableJunctionParent)] = new EntityMetadata(
            typeof(NullableJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(NullableJunctionParent),
                    typeof(NullableJunctionChild),
                    navigation,
                    isCollection: true,
                    Prop(childProps, nameof(NullableJunctionChild.Id)).PropertyInfo,
                    Prop(parentProps, nameof(NullableJunctionParent.Id)).PropertyInfo,
                    typeof(NullableJunctionChild),
                    typeof(NullableJunctionParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(NullableJunctionLink),
                        ParentKey = [Prop(parentProps, nameof(NullableJunctionParent.TenantId)).PropertyInfo, Prop(parentProps, nameof(NullableJunctionParent.Id)).PropertyInfo],
                        ChildKey = [Prop(childProps, nameof(NullableJunctionChild.Id)).PropertyInfo],
                        JunctionParentForeignKey = [Prop(linkProps, nameof(NullableJunctionLink.ParentTenant)).PropertyInfo, Prop(linkProps, nameof(NullableJunctionLink.ParentId)).PropertyInfo],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(NullableJunctionLink.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    [Fact]
    public void CompositeJunction_NullParentComponent_ShouldNotMatchAValidComponent()
    {
        DataContextCache.Clear();
        RegisterNullableJunction();

        using var ctx = new InMemoryDataContext();
        ctx.From<NullableJunctionParent>().WithData([
            new NullableJunctionParent { TenantId = null, Id = 1 },
            new NullableJunctionParent { TenantId = 1, Id = 2 },
        ]);
        ctx.From<NullableJunctionChild>().WithData([
            new NullableJunctionChild { Id = 10 },
            new NullableJunctionChild { Id = 20 },
            new NullableJunctionChild { Id = 30 },
            new NullableJunctionChild { Id = 40 },
        ]);
        ctx.From<NullableJunctionLink>().WithData([
            // Non-null tenant must not attach to the null-tenant parent (only the tenant component differs).
            new NullableJunctionLink { ParentTenant = 1, ParentId = 1, ChildId = 10 },
            // Null junction component must not attach to the valid non-null parent either.
            new NullableJunctionLink { ParentTenant = null, ParentId = 2, ChildId = 20 },
            // A matching link, including a matching null component, still attaches.
            new NullableJunctionLink { ParentTenant = null, ParentId = 1, ChildId = 30 },
            new NullableJunctionLink { ParentTenant = 1, ParentId = 2, ChildId = 40 },
        ]);

        var parents = ctx.From<NullableJunctionParent>()
            .JoinInto(ctx.From<NullableJunctionChild>(), (p, c) => true, p => p.Children)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.TenantId is null).Children.Select(c => c.Id).Should().Equal(new[] { 30 },
            "in memory a null junction component matches the null parent component (null == null), not the valid one");
        parents.Single(p => p.TenantId == 1).Children.Select(c => c.Id).Should().Equal(new[] { 40 },
            "a null junction component must not match a valid non-null parent component");
    }
}
