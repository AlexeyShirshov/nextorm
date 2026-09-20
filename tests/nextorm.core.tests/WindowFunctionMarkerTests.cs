using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The <see cref="WindowFunction{T}"/> and <see cref="WindowOrder"/> types are expression-tree
/// markers: the compiler records the <c>Over</c>/<c>asc</c>/<c>desc</c> calls in the tree and the SQL
/// visitor reads the method metadata, so their bodies are not on a normal query path. These tests call
/// them directly to pin their inert, value-returning contract.
/// </summary>
public class WindowFunctionMarkerTests
{
    private static WindowOrder CreateWindowOrder(Expression<Func<object?>> expression, OrderDirection direction)
    {
        // WindowOrder's constructor is internal (an instance is normally produced by asc/desc inside an
        // expression tree), so reflection is required to exercise it from the test assembly.
        return (WindowOrder)Activator.CreateInstance(
            typeof(WindowOrder),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [expression, direction],
            culture: null)!;
    }

    [Fact]
    public void Over_ExpressionOverload_ShouldReturnDefaultValue()
    {
        var function = new WindowFunction<int>();
        Expression<Func<object?>> partition = () => 1;
        Expression<Func<object?>> order = () => 2;

        function.Over(partition, order).Should().Be(0);
        function.Over(partition).Should().Be(0);
        function.Over().Should().Be(0);
    }

    [Fact]
    public void Over_WindowOrderOverloads_ShouldReturnDefaultValue()
    {
        var function = new WindowFunction<int>();
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
        var rows = WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.UnboundedFollowing);

        rows.Type.Should().Be(WindowFrameType.Rows);
        rows.Start.Kind.Should().Be(WindowFrameBoundKind.UnboundedPreceding);
        rows.End.Kind.Should().Be(WindowFrameBoundKind.UnboundedFollowing);

        var range = WindowFrame.RangeUnboundedPrecedingToCurrentRow;
        range.Type.Should().Be(WindowFrameType.Range);
        range.Start.Kind.Should().Be(WindowFrameBoundKind.UnboundedPreceding);
        range.End.Kind.Should().Be(WindowFrameBoundKind.CurrentRow);

        WindowFrameBound.Preceding(3).Kind.Should().Be(WindowFrameBoundKind.Preceding);
        WindowFrameBound.Preceding(3).Offset.Should().Be(3);
        WindowFrameBound.Following(4).Kind.Should().Be(WindowFrameBoundKind.Following);
        WindowFrameBound.Following(4).Offset.Should().Be(4);
        WindowFrameBound.CurrentRow.Kind.Should().Be(WindowFrameBoundKind.CurrentRow);
    }
}
