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
