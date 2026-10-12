using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// T213 D03 (r=2): admission of a mixed <c>Projection&lt;mappedEntity, scalar&gt;</c> projection.
/// Recreates the archived bounded carrier spike as a real test: <c>TryBuildProjectionSelectList</c>
/// expands the mapped junction item into its tagged scalar columns and keeps the supported second item
/// as an ordinary column (the two recorded spike failures <c>RowMaterializerBuilder.cs:465</c>/<c>:468</c>
/// must be gone for this shape). The admitted select list is then materialized through the shared
/// <see cref="RowMaterializerBuilder"/> seam (no database), covering a value, reference and nullable
/// scalar, an all-columns-NULL entity item and the fail-closed negative for a non-mapped complex item.
/// Runs against the provider-free <see cref="RootProjectionTests.TestContext"/> (fake dialect) so the
/// planner and the shared materializer are exercised without a database.
/// </summary>
public class JoinIntoCompositeCarrierSpikeTests
{
    private static IDataContext CreateContext() => new RootProjectionTests.TestContext();

    private static QueryCommand<TResult> Prepare<TResult>(IDataContext ctx, EntityBuilder<TResult> builder)
    {
        var cmd = builder.ToCommand();
        _ = ctx.GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);
        return cmd;
    }

    [Fact]
    public void MixedProjection_EntityPlusValueScalar_ShouldAdmitScalarColumnAndMaterialize()
    {
        using var ctx = CreateContext();
        var scalarIds = ctx.From<CarrierScalar>().Select(s => s.Id);

        var cmd = Prepare(ctx, ctx.From<CarrierJunction>().Join(ctx.From(scalarIds), (j, s) => j.Id == s));

        cmd.SelectList.Should().NotBeNull();
        var selectList = cmd.SelectList!;
        // The junction is expanded into its five mapped columns, all tagged to slot 1; the scalar item is
        // an ordinary sixth column bound to the projection's Item2 (untagged, so the materializer reads it
        // directly instead of trying to rebuild an entity from it).
        selectList.Should().HaveCount(6);
        selectList.Take(5).Should().OnlyContain(c => c.ProjectionItem != null && c.ProjectionItem!.Slot == 0 && c.ProjectionItem.EntityType == typeof(CarrierJunction));
        selectList[5].ProjectionItem.Should().BeNull();
        selectList[5].PropertyName.Should().Be("Item2");
        selectList[5].PropertyType.Should().Be(typeof(int));

        var projection = MaterializeValue(selectList, scalar: 42, nullEntity: false);
        projection.Item1.Should().NotBeNull("the mapped entity group materializes beside the scalar");
        projection.Item2.Should().Be(42);
    }

    [Fact]
    public void MixedProjection_EntityPlusReferenceScalar_ShouldAdmitAndMaterialize()
    {
        using var ctx = CreateContext();
        var scalarNames = ctx.From<CarrierScalar>().Select(s => s.Name);

        var cmd = Prepare(ctx, ctx.From<CarrierJunction>().Join(ctx.From(scalarNames), (j, s) => j.Id == s!.Length));

        cmd.SelectList.Should().NotBeNull();
        var selectList = cmd.SelectList!;
        selectList[5].ProjectionItem.Should().BeNull();
        selectList[5].PropertyType.Should().Be(typeof(string));

        var projection = Materialize(selectList, scalar: (string?)"carrier", nullEntity: false);
        projection.Item1.Should().NotBeNull();
        projection.Item2.Should().Be("carrier");
    }

    [Fact]
    public void MixedProjection_EntityPlusNullableScalar_ShouldAdmitAndMaterialize()
    {
        using var ctx = CreateContext();
        var scalarValues = ctx.From<CarrierScalar>().Select(s => (int?)s.Value);

        var cmd = Prepare(ctx, ctx.From<CarrierJunction>().Join(ctx.From(scalarValues), (j, s) => j.Id == s));

        cmd.SelectList.Should().NotBeNull();
        var selectList = cmd.SelectList!;
        selectList[5].ProjectionItem.Should().BeNull();
        selectList[5].PropertyType.Should().Be(typeof(int?));

        var projection = Materialize(selectList, scalar: (int?)null, nullEntity: false);
        projection.Item1.Should().NotBeNull();
        projection.Item2.Should().BeNull("a nullable scalar item round-trips its null");
    }

    [Fact]
    public void MixedProjection_EntityAllColumnsNull_ShouldMaterializeNullEntity()
    {
        using var ctx = CreateContext();
        var scalarIds = ctx.From<CarrierScalar>().Select(s => s.Id);

        var cmd = Prepare(ctx, ctx.From<CarrierJunction>().Join(ctx.From(scalarIds), (j, s) => j.Id == s));

        cmd.SelectList.Should().NotBeNull();
        var selectList = cmd.SelectList!;
        var projection = MaterializeValue(selectList, scalar: 7, nullEntity: true);

        projection.Item1.Should().BeNull("an all-columns-NULL entity item materializes as null, not a default instance");
        projection.Item2.Should().Be(7);
    }

    [Fact]
    public void MixedProjection_UnsupportedComplexItem_ShouldStillFailClosed()
    {
        using var ctx = CreateContext();
        var unmapped = ctx.From<CarrierScalar>().Select(s => new CarrierUnmappedItem { X = s.Id });

        var joined = ctx.From<CarrierJunction>().Join(ctx.From(unmapped), (j, u) => j.Id == u.X);

        Action act = () => Prepare(ctx, joined);

        act.Should().Throw<QueryPreparationException>(
            "a non-mapped complex item must not be admitted as an ordinary scalar column");
    }

    // ---------------------------------------------------------------------------------------------
    // Materialization through the shared builder, using the admitted select list. The provider read is
    // replaced by constants (mapColumn) and the all-NULL test by a constant (isNull); the entity group is
    // still rebuilt by the real BuildEntityItem path, which is the seam the carrier spike exercised.
    // ---------------------------------------------------------------------------------------------

    private static Projection<CarrierJunction, int> MaterializeValue(SelectExpression[] selectList, int scalar, bool nullEntity)
        => Materialize(selectList, scalar, nullEntity);

    private static Projection<CarrierJunction, T> Materialize<T>(SelectExpression[] selectList, T scalar, bool nullEntity)
    {
        var param = Expression.Parameter(typeof(object), "row");
        Func<SelectExpression, Expression> map = column => column.ProjectionItem is not null
            ? Expression.Constant(DefaultFor(column.PropertyType), column.PropertyType)
            : Expression.Constant(scalar, column.PropertyType);
        Func<SelectExpression, Expression> isNull = column => column.ProjectionItem is not null && nullEntity
            ? Expression.Constant(true)
            : Expression.Constant(false);

        var body = RowMaterializerBuilder.Build(
            typeof(Projection<CarrierJunction, T>), param, selectList, ignoreColumns: false, map, mapDynamicColumns: null, isNull);
        var materialize = Expression.Lambda<Func<object, Projection<CarrierJunction, T>>>(body, param).Compile();
        return materialize(new object());
    }

    private static object? DefaultFor(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
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
