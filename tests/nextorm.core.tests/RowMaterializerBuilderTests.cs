using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Direct tests of the projection-item materialization path of <see cref="RowMaterializerBuilder"/>.
/// The builder is the shared result-shape -&gt; expression-tree seam, so its constructor-selection
/// contract is pinned here without a database: a projection whose constructor arity does not match the
/// number of projected items must assemble member by member through a parameterless constructor (or be
/// rejected with a preparation error), never invoke a parameterful constructor with no arguments.
/// </summary>
public class RowMaterializerBuilderTests
{
    public sealed class Part
    {
        public int Id { get; set; }
    }

    public sealed class Shape
    {
        public Part? First { get; set; }
        public int Second { get; set; }
        public int Id { get; set; }
        public object? Store { get; set; }

        public Shape()
        {
        }

        public Shape(Part first) => First = first;
    }

    public sealed class CtorOnlyShape
    {
        public Part? First { get; set; }
        public int Second { get; set; }

        public CtorOnlyShape(Part first) => First = first;
    }

    // The entity item's own columns are mapped by this delegate through BuildCore; every column it is
    // asked for is a scalar of the entity (int here), so no column is ever of the entity type itself.
    private static Func<SelectExpression, Expression> ConstantMap()
        => column => Expression.Constant(0, column.PropertyType);

    private static SelectExpression[] SelectListFor(Type resultType)
    {
        var item = new ProjectionEntityItem(0, typeof(Part), resultType.GetProperty(nameof(Shape.First)));
        return
        [
            new SelectExpression(typeof(int))
            {
                Index = 0,
                PropertyName = nameof(Part.Id),
                ProjectionItem = item,
            },
            new SelectExpression(typeof(int))
            {
                Index = 1,
                PropertyName = nameof(Shape.Second),
            },
        ];
    }

    [Fact]
    public void ConstructorArityMismatch_ShouldAssembleByMemberThroughTheParameterlessConstructor()
    {
        var param = Expression.Parameter(typeof(object), "row");
        var body = RowMaterializerBuilder.Build(
            typeof(Shape), param, SelectListFor(typeof(Shape)), ignoreColumns: false, ConstantMap());

        var materialize = Expression.Lambda<Func<object, Shape>>(body, param).Compile();
        var shape = materialize(new object());

        shape.First.Should().NotBeNull();
        shape.First!.Id.Should().Be(0);
        shape.Second.Should().Be(0);
    }

    [Fact]
    public void ConstructorArityMismatch_WithoutParameterlessConstructor_ShouldThrowClearPreparationError()
    {
        var param = Expression.Parameter(typeof(object), "row");

        Action act = () => RowMaterializerBuilder.Build(
            typeof(CtorOnlyShape), param, SelectListFor(typeof(CtorOnlyShape)), ignoreColumns: false, ConstantMap());

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*CtorOnlyShape*1 constructor parameter*2 item*");
    }

    [Fact]
    public void MixedProjection_WithDynamicColumnsStore_ShouldRouteTheStoreThroughTheProviderAccessor()
    {
        var param = Expression.Parameter(typeof(object), "row");
        var item = new ProjectionEntityItem(0, typeof(Part), typeof(Shape).GetProperty(nameof(Shape.First)));
        var store = new SelectExpression(typeof(object))
        {
            Index = 1,
            PropertyName = nameof(Shape.Store),
            PropertyInfo = typeof(Shape).GetProperty(nameof(Shape.Store)),
            IsDynamicColumnsStore = true,
        };
        var selectList = new SelectExpression[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = nameof(Part.Id), ProjectionItem = item },
            store,
            new SelectExpression(typeof(int)) { Index = 2, PropertyName = nameof(Shape.Second) },
        };

        var storeCalls = 0;
        Expression MapDynamic(SelectExpression _, int starStart, DynamicColumns __)
        {
            storeCalls++;
            starStart.Should().Be(2);
            return Expression.Constant("store", typeof(object));
        }

        var body = RowMaterializerBuilder.Build(typeof(Shape), param, selectList, ignoreColumns: false, ConstantMap(), MapDynamic);

        var materialize = Expression.Lambda<Func<object, Shape>>(body, param).Compile();
        var shape = materialize(new object());

        storeCalls.Should().Be(1, "a mixed projection must still materialize the dynamic-columns store through the provider accessor");
        shape.Store.Should().Be("store");
        shape.First.Should().NotBeNull();
        shape.Second.Should().Be(0);
    }

    [Fact]
    public void ConstructorPositionItem_WithoutMatchingCtor_ShouldThrowClearPreparationError()
    {
        // The item addresses a constructor position (Member is null) but the longest constructor takes
        // one argument while the projection materializes two items, so member-init has to bind it. A
        // wrong implementation would fall back to the entity's own property name ("Id", which Shape
        // happens to declare) and fail building the tree with a coercion error instead.
        var param = Expression.Parameter(typeof(object), "row");
        var item = new ProjectionEntityItem(0, typeof(Part), member: null);
        var selectList = new SelectExpression[]
        {
            new SelectExpression(typeof(int)) { Index = 0, PropertyName = nameof(Part.Id), ProjectionItem = item },
            new SelectExpression(typeof(int)) { Index = 1, PropertyName = nameof(Shape.Second) },
        };

        Action act = () => RowMaterializerBuilder.Build(typeof(Shape), param, selectList, ignoreColumns: false, ConstantMap());

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*constructor position*");
    }
}
