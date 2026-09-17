using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;

namespace nextorm.core.tests;

/// <summary>
/// The <see cref="NORM.WindowFunction{T}"/> and <see cref="NORM.WindowOrder"/> types are expression-tree
/// markers: the compiler records the <c>Over</c>/<c>asc</c>/<c>desc</c> calls in the tree and the SQL
/// visitor reads the method metadata, so their bodies are not on a normal query path. These tests call
/// them directly to pin their inert, value-returning contract.
/// </summary>
public class WindowFunctionMarkerTests
{
    private static NORM.WindowOrder CreateWindowOrder(Expression<Func<object?>> expression, OrderDirection direction)
    {
        // WindowOrder's constructor is internal (an instance is normally produced by asc/desc inside an
        // expression tree), so reflection is required to exercise it from the test assembly.
        return (NORM.WindowOrder)Activator.CreateInstance(
            typeof(NORM.WindowOrder),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [expression, direction],
            culture: null)!;
    }

    [Fact]
    public void Over_ExpressionOverload_ShouldReturnDefaultValue()
    {
        var function = new NORM.WindowFunction<int>();
        Expression<Func<object?>> partition = () => 1;
        Expression<Func<object?>> order = () => 2;

        function.Over(partition, order).Should().Be(0);
        function.Over(partition).Should().Be(0);
        function.Over().Should().Be(0);
    }

    [Fact]
    public void Over_WindowOrderOverloads_ShouldReturnDefaultValue()
    {
        var function = new NORM.WindowFunction<int>();
        var order = CreateWindowOrder(() => 1, OrderDirection.Desc);

        function.Over(order).Should().Be(0);
        function.Over([order]).Should().Be(0);
        function.Over(new Expression<Func<object?>>[] { () => 1 }, [order]).Should().Be(0);
    }

    [Fact]
    public void WindowOrderConstructor_ShouldExposeExpressionAndDirection()
    {
        Expression<Func<object?>> expression = () => 1;

        var asc = CreateWindowOrder(expression, OrderDirection.Asc);
        var desc = CreateWindowOrder(expression, OrderDirection.Desc);

        asc.Expression.Should().BeSameAs(expression);
        asc.Direction.Should().Be(OrderDirection.Asc);
        desc.Direction.Should().Be(OrderDirection.Desc);
    }

    [Fact]
    public void WindowFrameFactories_ShouldExposeTypeAndBounds()
    {
        var rows = NORM.WindowFrame.Rows(NORM.WindowFrameBound.UnboundedPreceding, NORM.WindowFrameBound.UnboundedFollowing);

        rows.Type.Should().Be(NORM.WindowFrameType.Rows);
        rows.Start.Kind.Should().Be(NORM.WindowFrameBoundKind.UnboundedPreceding);
        rows.End.Kind.Should().Be(NORM.WindowFrameBoundKind.UnboundedFollowing);

        var range = NORM.WindowFrame.RangeUnboundedPrecedingToCurrentRow;
        range.Type.Should().Be(NORM.WindowFrameType.Range);
        range.Start.Kind.Should().Be(NORM.WindowFrameBoundKind.UnboundedPreceding);
        range.End.Kind.Should().Be(NORM.WindowFrameBoundKind.CurrentRow);

        NORM.WindowFrameBound.Preceding(3).Kind.Should().Be(NORM.WindowFrameBoundKind.Preceding);
        NORM.WindowFrameBound.Preceding(3).Offset.Should().Be(3);
        NORM.WindowFrameBound.Following(4).Kind.Should().Be(NORM.WindowFrameBoundKind.Following);
        NORM.WindowFrameBound.Following(4).Offset.Should().Be(4);
        NORM.WindowFrameBound.CurrentRow.Kind.Should().Be(NORM.WindowFrameBoundKind.CurrentRow);
    }
}
