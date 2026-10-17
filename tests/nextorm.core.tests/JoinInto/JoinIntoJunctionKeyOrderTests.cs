using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// D07 (r=5) metadata-order/reachability proof for <c>EntityBuilder.BuildPairedKeyEquality</c> (#213): the
/// junction foreign-key list is paired with the relationship key list <b>by index</b>, so a composite
/// many-to-many link only joins when the two lists are in the same component order. The positive case
/// (aligned order) stitches the child; the negative control (a deliberately mis-ordered junction foreign
/// key) does not — proving the pairing is order-sensitive, not an artifact of a single matching column.
/// </summary>
public class JoinIntoJunctionKeyOrderTests
{
    public sealed class OrderedParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<OrderedChild> Children { get; set; } = new List<OrderedChild>();
    }

    public sealed class MisorderedParent
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public ICollection<OrderedChild> Children { get; set; } = new List<OrderedChild>();
    }

    public sealed class OrderedChild
    {
        public int Id { get; set; }
    }

    public sealed class OrderedLink
    {
        public int JunctionTenant { get; set; }
        public int JunctionParentId { get; set; }
        public int ChildId { get; set; }
    }

    private static void RegisterManyToMany<T>(
        System.Reflection.PropertyInfo navigation,
        System.Reflection.PropertyInfo tenantKey,
        System.Reflection.PropertyInfo idKey,
        System.Reflection.PropertyInfo junctionFirst,
        System.Reflection.PropertyInfo junctionSecond)
    {
        var parentProps = new EntityMetadataBuilder<T>().Build().Properties
            .Where(p => p.PropertyInfo != navigation)
            .ToList();
        var childKey = typeof(OrderedChild).GetProperty(nameof(OrderedChild.Id))!;
        var linkChildKey = typeof(OrderedLink).GetProperty(nameof(OrderedLink.ChildId))!;

        DataContextCache.Metadata[typeof(T)] = new EntityMetadata(
            typeof(T).Name,
            parentProps,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(T),
                    typeof(OrderedChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    idKey,
                    typeof(OrderedChild),
                    typeof(T),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(OrderedLink),
                        ParentKey = [tenantKey, idKey],
                        ChildKey = [childKey],
                        // Paired by index with ParentKey: [junctionFirst, junctionSecond].
                        JunctionParentForeignKey = [junctionFirst, junctionSecond],
                        JunctionChildForeignKey = [linkChildKey],
                    }),
            ]);
    }

    private static IReadOnlyList<int> StitchOrdered()
    {
        var navigation = typeof(OrderedParent).GetProperty(nameof(OrderedParent.Children))!;
        var tenantKey = typeof(OrderedParent).GetProperty(nameof(OrderedParent.TenantId))!;
        var idKey = typeof(OrderedParent).GetProperty(nameof(OrderedParent.Id))!;
        var junctionTenant = typeof(OrderedLink).GetProperty(nameof(OrderedLink.JunctionTenant))!;
        var junctionParentId = typeof(OrderedLink).GetProperty(nameof(OrderedLink.JunctionParentId))!;
        RegisterManyToMany<OrderedParent>(navigation, tenantKey, idKey, junctionTenant, junctionParentId);

        using var ctx = new InMemoryDataContext();
        ctx.From<OrderedParent>().WithData([new OrderedParent { TenantId = 1, Id = 5 }]);
        ctx.From<OrderedChild>().WithData([new OrderedChild { Id = 10 }]);
        ctx.From<OrderedLink>().WithData([new OrderedLink { JunctionTenant = 1, JunctionParentId = 5, ChildId = 10 }]);

        return ctx.From<OrderedParent>()
            .JoinInto(ctx.From<OrderedChild>(), (p, c) => true, p => p.Children)
            .ToList()[0].Children.Select(c => c.Id).ToList();
    }

    private static IReadOnlyList<int> StitchMisordered()
    {
        var navigation = typeof(MisorderedParent).GetProperty(nameof(MisorderedParent.Children))!;
        var tenantKey = typeof(MisorderedParent).GetProperty(nameof(MisorderedParent.TenantId))!;
        var idKey = typeof(MisorderedParent).GetProperty(nameof(MisorderedParent.Id))!;
        var junctionTenant = typeof(OrderedLink).GetProperty(nameof(OrderedLink.JunctionTenant))!;
        var junctionParentId = typeof(OrderedLink).GetProperty(nameof(OrderedLink.JunctionParentId))!;
        // Deliberately reversed: [JunctionParentId, JunctionTenant] vs principal [TenantId, Id].
        RegisterManyToMany<MisorderedParent>(navigation, tenantKey, idKey, junctionParentId, junctionTenant);

        using var ctx = new InMemoryDataContext();
        ctx.From<MisorderedParent>().WithData([new MisorderedParent { TenantId = 1, Id = 5 }]);
        ctx.From<OrderedChild>().WithData([new OrderedChild { Id = 10 }]);
        ctx.From<OrderedLink>().WithData([new OrderedLink { JunctionTenant = 1, JunctionParentId = 5, ChildId = 10 }]);

        return ctx.From<MisorderedParent>()
            .JoinInto(ctx.From<OrderedChild>(), (p, c) => true, p => p.Children)
            .ToList()[0].Children.Select(c => c.Id).ToList();
    }

    [Fact]
    public void CompositeJunctionFk_AlignedWithRelationshipKeyOrder_ShouldStitch()
    {
        StitchOrdered().Should().Equal(new[] { 10 },
            "the junction foreign keys are paired with the relationship key components by the same index");
    }

    [Fact]
    public void CompositeJunctionFk_Misordered_ShouldNotStitch_ProvingIndexPairing()
    {
        // The link row is identical; only the declared junction foreign-key order differs. If the pairing
        // were order-insensitive (for example value-based), the child would still stitch. It does not, so
        // BuildPairedKeyEquality's index pairing is proven reachable and order-sensitive.
        StitchMisordered().Should().BeEmpty(
            "reversing the junction foreign-key order pairs the wrong key components and matches nothing");
    }
}
