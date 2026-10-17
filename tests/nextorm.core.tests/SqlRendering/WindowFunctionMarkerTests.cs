using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Postgres;

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
    public void Over_ScalarWindowOrderOverload_ShouldReturnDefaultValue()
    {
        // The scalar (partition, WindowOrder) overload is inert like the other Over overloads: it only
        // records the call in an expression tree, so calling it directly yields default(T).
        var order = CreateWindowOrder(() => 1, OrderDirection.Asc);

        new WindowFunction<int>().Over(partitionBy: () => 1, orderBy: order).Should().Be(0);
        new WindowFunction<int?>().Over(partitionBy: () => 1, orderBy: order).Should().BeNull();
        new WindowFunction<string>().Over(partitionBy: () => 1, orderBy: order).Should().BeNull();
    }

    [Fact]
    public void Over_ScalarWindowOrderOverload_ShouldRenderTheSameSqlAsTheArrayOverload()
    {
        // The new overload is a call-shape alias for the single-element array form: the rendered SQL has
        // to be byte-identical both without a frame and with one. A single dialect is enough here; the
        // per-dialect SqlGenerationTests suites pin the remaining five.
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var e = ctx.From<SimpleEntity>();

        var scalar = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(
                partitionBy: () => x.Id,
                orderBy: SqlFunctions.Sql.asc(() => x.Id))
        }));
        var array = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(
                new Expression<Func<object?>>[] { () => x.Id },
                new WindowOrder[] { SqlFunctions.Sql.asc(() => x.Id) })
        }));
        scalar.Should().Be(array);
        scalar.Should().ContainEquivalentOf("row_number() over (partition by Id order by Id)");

        var scalarFrame = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                partitionBy: () => x.Id,
                orderBy: SqlFunctions.Sql.asc(() => x.Id),
                frame: WindowFrame.Rows(1, 1))
        }));
        var arrayFrame = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                new Expression<Func<object?>>[] { () => x.Id },
                new WindowOrder[] { SqlFunctions.Sql.asc(() => x.Id) },
                WindowFrame.Rows(1, 1))
        }));
        scalarFrame.Should().Be(arrayFrame);
        scalarFrame.Should().ContainEquivalentOf("over (partition by Id order by Id rows between 1 preceding and 1 following)");
    }

    [Fact]
    public void Over_ScalarWindowOrderOverload_WithNullPartition_ShouldEmitNoPartitionClause()
    {
        // `partitionBy: null` on the scalar overload is the same no-partition call shape as a null
        // partition array, an empty partition array and the order-only overload: SplitWindowArguments
        // forwards the null argument untouched (Visitors/WindowSql.cs:163-164) and ParseWindowPartitions
        // yields zero partitions for a null/constant-null expression and for an empty array
        // (Visitors/WindowSql.cs:65-72), so the translator emits no `partition by`
        // (Visitors/WindowFunctionTranslator.cs:152-154). All four shapes must render identical SQL.
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var e = ctx.From<SimpleEntity>();

        var scalar = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(
                partitionBy: (Expression<Func<object?>>?)null,
                orderBy: SqlFunctions.Sql.asc(() => x.Id))
        }));
        var arrayNull = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(
                partitionBy: (Expression<Func<object?>>[]?)null,
                orderBy: new WindowOrder[] { SqlFunctions.Sql.asc(() => x.Id) })
        }));
        var arrayEmpty = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(
                partitionBy: new Expression<Func<object?>>[0],
                orderBy: new WindowOrder[] { SqlFunctions.Sql.asc(() => x.Id) })
        }));
        var orderOnly = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        scalar.Should().Be(arrayNull);
        scalar.Should().Be(arrayEmpty);
        scalar.Should().Be(orderOnly);
        scalar.Should().NotContainEquivalentOf("partition by");
        scalar.Should().ContainEquivalentOf("row_number() over (order by Id)");
    }

    [Fact]
    public void Over_DefaultOrNullOrderArgument_ShouldStillBindToTheExpressionOverload()
    {
        var function = new WindowFunction<int>();
        Expression<Func<object?>> partition = () => 1;

        // A method-group conversion to a delegate whose second parameter is an expression only compiles
        // while the original (Expression, Expression, WindowFrame) overload is the one selected: the new
        // overload's second parameter is a WindowOrder and cannot satisfy this delegate signature.
        Func<Expression<Func<object?>>?, Expression<Func<object?>>?, WindowFrame?, int> expressionOverload = function.Over;
        expressionOverload(partition, null, null).Should().Be(0);

        // The target-typed default / null argument is resolved at compile time, so read back the method
        // the compiler actually bound from an expression tree and assert its parameter types.
        Expression<Func<int>> withDefault = () => function.Over(partitionBy: partition, orderBy: default);
        Expression<Func<int>> withNull = () => function.Over(partitionBy: () => 1, orderBy: null);

        AssertOverload(withDefault, typeof(Expression<Func<object?>>), typeof(Expression<Func<object?>>), typeof(WindowFrame));
        AssertOverload(withNull, typeof(Expression<Func<object?>>), typeof(Expression<Func<object?>>), typeof(WindowFrame));
    }

    [Fact]
    public void Over_ScalarOrderArgument_ShouldBindToTheNewOverload()
    {
        var function = new WindowFunction<int>();
        Expression<Func<object?>> partition = () => 1;
        var order = CreateWindowOrder(() => 2, OrderDirection.Asc);

        // Positive method-group conversion to the new (Expression, WindowOrder, WindowFrame) signature.
        Func<Expression<Func<object?>>?, WindowOrder, WindowFrame?, int> scalarOverload = function.Over;
        scalarOverload(partition, order, null).Should().Be(0);

        Expression<Func<int>> twoArguments = () => function.Over(partitionBy: partition, orderBy: order);
        Expression<Func<int>> framed = () => function.Over(partitionBy: partition, orderBy: order, frame: WindowFrame.Rows(1, 1));

        AssertOverload(twoArguments, typeof(Expression<Func<object?>>), typeof(WindowOrder), typeof(WindowFrame));
        AssertOverload(framed, typeof(Expression<Func<object?>>), typeof(WindowOrder), typeof(WindowFrame));
    }

    private static void AssertOverload(Expression<Func<int>> call, params Type[] expectedParameterTypes)
    {
        var method = ((MethodCallExpression)call.Body).Method;

        method.Name.Should().Be(nameof(WindowFunction<int>.Over));
        method.GetParameters().Select(p => p.ParameterType).Should().Equal(expectedParameterTypes);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) =>
        ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText
            .Replace("\r\n", "\n");

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
