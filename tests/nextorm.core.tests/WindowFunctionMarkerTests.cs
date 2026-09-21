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
    public void Over_NamedWindowOverload_ShouldReturnDefaultValue()
    {
        var function = new WindowFunction<int>();

        function.Over("w").Should().Be(0);
    }

    [Fact]
    public void WindowFrame_GroupsFactory_ShouldExposeTypeAndBounds()
    {
        var groups = WindowFrame.Groups(WindowFrameBound.Preceding(2), WindowFrameBound.Following(3));

        groups.Type.Should().Be(WindowFrameType.Groups);
        groups.Start.Kind.Should().Be(WindowFrameBoundKind.Preceding);
        groups.Start.Offset.Should().Be(2);
        groups.End.Kind.Should().Be(WindowFrameBoundKind.Following);
        groups.End.Offset.Should().Be(3);
        groups.Exclusion.Should().BeNull();

        WindowFrame.Groups(1, 1).Type.Should().Be(WindowFrameType.Groups);
    }

    [Fact]
    public void WindowFrame_WithExclusion_ShouldSetExclusion()
    {
        var frame = WindowFrame.RowsUnboundedPrecedingToCurrentRow;
        frame.Exclusion.Should().BeNull();

        var currentRow = frame.WithExclusion(WindowFrameExclusion.CurrentRow);
        currentRow.Exclusion.Should().Be(WindowFrameExclusion.CurrentRow);
        currentRow.Type.Should().Be(WindowFrameType.Rows);
        currentRow.Start.Kind.Should().Be(WindowFrameBoundKind.UnboundedPreceding);

        frame.WithExclusion(WindowFrameExclusion.Group).Exclusion.Should().Be(WindowFrameExclusion.Group);
        frame.WithExclusion(WindowFrameExclusion.Ties).Exclusion.Should().Be(WindowFrameExclusion.Ties);
        frame.WithExclusion(WindowFrameExclusion.NoOthers).Exclusion.Should().Be(WindowFrameExclusion.NoOthers);
    }

    [Fact]
    public void Window_ShouldExposeDefinitionOnCommand()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var cmd = e.Window("w", partitionBy: [x => x.Id], orderBy: [e.Desc(x => x.Id)])
            .Select(x => new { x.Id });

        var window = cmd.Windows.Should().ContainSingle().Subject;
        window.Name.Should().Be("w");
        window.PartitionBy.Should().ContainSingle();
        window.OrderBy.Should().ContainSingle().Which.Direction.Should().Be(OrderDirection.Desc);
        window.Frame.Should().BeNull();
    }

    [Fact]
    public void Window_DuplicateName_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.Window("w").Window("w");

        act.Should().Throw<InvalidOperationException>().WithMessage("*already declared*");
    }

    [Fact]
    public void Window_InvalidName_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.Window("1 bad");

        act.Should().Throw<ArgumentException>().WithMessage("*identifier*");
    }

    [Fact]
    public void Window_BeforeJoin_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var act = () => e.Window("w").Join(ctx.From<SimpleEntity>(), (a, b) => a.Id == b.Id);

        act.Should().Throw<InvalidOperationException>().WithMessage("*after joins*");
    }

    [Fact]
    public void Window_PlanEquality_ShouldCompareWindows()
    {
        using var ctx = new InMemoryDataContext();
        var e = ctx.From<SimpleEntity>();

        var a = e.Window("w", partitionBy: [x => x.Id]).Select(x => new { x.Id });
        var same = e.Window("w", partitionBy: [x => x.Id]).Select(x => new { x.Id });
        var otherName = e.Window("v", partitionBy: [x => x.Id]).Select(x => new { x.Id });
        var otherFrame = e.Window("w", partitionBy: [x => x.Id], frame: WindowFrame.Groups(1, 1)).Select(x => new { x.Id });

        a.PrepareCommand(CancellationToken.None);
        same.PrepareCommand(CancellationToken.None);
        otherName.PrepareCommand(CancellationToken.None);
        otherFrame.PrepareCommand(CancellationToken.None);

        var comparer = a.GetQueryPlanEqualityComparer();

        comparer.Equals(a, same).Should().BeTrue();
        comparer.Equals(a, otherName).Should().BeFalse();
        comparer.Equals(a, otherFrame).Should().BeFalse();
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
