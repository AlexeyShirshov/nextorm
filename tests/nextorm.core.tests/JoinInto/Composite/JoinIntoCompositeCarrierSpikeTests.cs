using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T213 candidate 3 (r=5): the mixed <c>Projection&lt;mappedEntity, scalar&gt;</c> admission introduced at
/// D03 (r=2) was only ever consumed by the composite-junction SQL many-to-many link source, which now
/// fails closed at preparation (#223). No ordinary public query produces that shape as a *working*
/// result: an end-to-end public <c>Join</c> of a mapped entity plus a scalar subquery renders an
/// incomplete column (<c>Item2.</c>) and an unterminated join predicate. The admission is therefore
/// removed and the shape fails closed deterministically instead of emitting broken SQL. These cases pin
/// that fail-closed behavior for the value, reference and nullable scalar items and for the all-columns-
/// NULL entity item; the non-mapped complex item stays rejected as before.
/// </summary>
public class JoinIntoCompositeCarrierSpikeTests
{
    private static IDataContext CreateContext() => new RootProjectionTests.TestContext();

    private static void Prepare<TResult>(IDataContext ctx, EntityBuilder<TResult> builder)
        => ctx.GetPreparedQueryCommand(builder.ToCommand(), createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

    private static void ShouldFailClosed<T>(IDataContext ctx, EntityBuilder<T> joined, string because)
        => ((Action)(() => Prepare(ctx, joined)))
            .Should().Throw<QueryPreparationException>(because);

    [Fact]
    public void MixedProjection_EntityPlusValueScalar_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var scalarIds = ctx.From<CarrierScalar>().Select(s => s.Id);

        ShouldFailClosed(
            ctx,
            ctx.From<CarrierJunction>().Join(ctx.From(scalarIds), (j, s) => j.Id == s),
            "a scalar item beside a mapped entity is no longer admitted; the shape has no working public terminal");
    }

    [Fact]
    public void MixedProjection_EntityPlusReferenceScalar_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var scalarNames = ctx.From<CarrierScalar>().Select(s => s.Name);

        ShouldFailClosed(
            ctx,
            ctx.From<CarrierJunction>().Join(ctx.From(scalarNames), (j, s) => j.Id == s!.Length),
            "a reference scalar item beside a mapped entity must fail closed");
    }

    [Fact]
    public void MixedProjection_EntityPlusNullableScalar_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var scalarValues = ctx.From<CarrierScalar>().Select(s => (int?)s.Value);

        ShouldFailClosed(
            ctx,
            ctx.From<CarrierJunction>().Join(ctx.From(scalarValues), (j, s) => j.Id == s),
            "a nullable scalar item beside a mapped entity must fail closed");
    }

    [Fact]
    public void MixedProjection_UnsupportedComplexItem_ShouldStillFailClosed()
    {
        using var ctx = CreateContext();
        var unmapped = ctx.From<CarrierScalar>().Select(s => new CarrierUnmappedItem { X = s.Id });

        var joined = ctx.From<CarrierJunction>().Join(ctx.From(unmapped), (j, u) => j.Id == u.X);

        ((Action)(() => Prepare(ctx, joined)))
            .Should().Throw<QueryPreparationException>(
                "a non-mapped complex item must not be admitted as an ordinary scalar column");
    }
}

/// <summary>A reference type that is deliberately neither a mapped entity nor a supported scalar.</summary>
public sealed class CarrierUnmappedItem
{
    public int X { get; set; }
}

[SqlTable("carrier_junction")]
public sealed class CarrierJunction
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_key1")]
    public int ParentKey1 { get; set; }

    [Column("parent_key2")]
    public int ParentKey2 { get; set; }

    [Column("child_key")]
    public int ChildKey { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

[SqlTable("carrier_scalar")]
public sealed class CarrierScalar
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("value")]
    public int Value { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>
/// D01 (#223) decisive carrier probe (core half): the composite-junction many-to-many link source must
/// build on the SQL path instead of failing closed at the preparation guard. This asserts that the flat
/// arity-general carrier survives preparation; the sqlite probe asserts real SQL execution,
/// materialization and stitching. The fixture is the SAME composite junction used by the sqlite probe:
/// a two-component parent key [TenantId, Id] and a junction with two parent foreign keys
/// [ParentId, ParentId2] plus one child foreign key [ChildId].
/// </summary>
[Collection("Query cache controls")]
public class JoinIntoCompositeCarrierSpikeProbeTests
{
    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    internal static void RegisterProbeJunction()
    {
        var parentProps = Props<ProbeParent>();
        var childProps = Props<ProbeChild>();
        var linkProps = Props<ProbeJunction>();
        var tenant = Prop(parentProps, nameof(ProbeParent.TenantId)).PropertyInfo;
        var id = Prop(parentProps, nameof(ProbeParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(ProbeChild.Id)).PropertyInfo;
        var navigation = typeof(ProbeParent).GetProperty(nameof(ProbeParent.Children));

        DataContextCache.Metadata[typeof(ProbeParent)] = new EntityMetadata(
            typeof(ProbeParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(ProbeParent),
                    typeof(ProbeChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    id,
                    typeof(ProbeChild),
                    typeof(ProbeParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(ProbeJunction),
                        ParentKey = [tenant, id],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(ProbeJunction.ParentId)).PropertyInfo,
                            Prop(linkProps, nameof(ProbeJunction.ParentId2)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(ProbeJunction.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    [Fact]
    public void CompositeJunctionManyToMany_SqlPath_ShouldBuildCompositeLinkSource()
    {
        DataContextCache.Clear();
        RegisterProbeJunction();
        using var ctx = new RootProjectionTests.TestContext();

        var builder = ctx.From<ProbeParent>()
            .JoinInto(ctx.From<ProbeChild>(), (p, c) => true, p => p.Children);

        // Before D01 this threw NotSupportedException("composite-junction M:N is not supported; see #223")
        // from JunctionLinkSourceFactory.BuildSource. The composite carrier must build and prepare instead.
        Action act = () => ctx.GetPreparedQueryCommand(builder.ToCommand(), createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        act.Should().NotThrow("the flat composite carrier must survive SQL preparation");
    }
}

[SqlTable("probe_parent")]
public sealed class ProbeParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeChild> Children { get; set; } = new List<ProbeChild>();
}

[SqlTable("probe_child")]
public sealed class ProbeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_link")]
public sealed class ProbeJunction
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("parent_id2")]
    public int ParentId2 { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
