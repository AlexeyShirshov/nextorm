using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void CapturedExistsCommand_DoesNotKeepTheFirstOperandInTheExpressionCache()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstOperand = Execute(first, 1, [1, 2]);
        Execute(second, 3, []);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 3));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOperand.IsAlive.Should().BeFalse("the shared expression accessor must not retain the first EXISTS operand");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, int target, int[] expected)
        {
            var operand = Query(context, target);
            var command = context.From<BindingRow>().Where(row => SqlFunctions.Sql.exists(operand))
                .OrderBy(row => row.Id).Select(row => row.Id);
            // Isolate the shared expression cache from the L1 plan and its aliases.
            command.Cache = false;
            command.ToList().Should().Equal(expected);
            return new WeakReference(operand);
        }
    }

    [Fact]
    public void ExistsFactory_DoesNotKeepTheFirstFactoryInTheExpressionCache()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstFactory = Execute(first, 1, [1, 2]);
        Execute(second, 3, []);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 3));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstFactory.IsAlive.Should().BeFalse("the shared accessor must not own the EXISTS factory");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, int target, int[] expected)
        {
            var factory = new ExistsFactory(context, target);
            var command = context.From<BindingRow>().Where(row => SqlFunctions.Sql.exists(factory.Create()))
                .OrderBy(row => row.Id).Select(row => row.Id);
            command.Cache = false;
            command.ToList().Should().Equal(expected);
            return new WeakReference(factory);
        }
    }

    private sealed class ExistsFactory(SqliteDataContext context, int target)
    {
        public QueryCommand<int> Create() => Query(context, target);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SqlRenderer_DoesNotKeepAComponentSuppliedSubqueryOperand(bool useIn)
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstOperand = Execute(first, useIn, 1);
        Execute(second, useIn, 2);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 2));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOperand.IsAlive.Should().BeFalse("the SQL renderer must not own a component-supplied operand");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, bool useIn, int target)
        {
            var token = TestContext.Current.CancellationToken;
            var operand = Query(context, target);
            operand.PrepareCommand(token);
            var command = context.From<BindingRow>().OrderBy(row => row.Id).Select(row => row.Id);
            command.PrepareCommand(token);
            // Exercise the late SQL visitor, as when a component supplies a predicate at render time.
            Expression<Func<BindingRow, bool>> condition = useIn
                ? row => SqlFunctions.Sql.@in(row.Id, operand)
                : row => SqlFunctions.Sql.exists(operand);
            command.SetComponentPreparedCondition(condition);
            var invocation = context.GetPreparedQueryCommand(command, false, false, token);
            int[] expected = useIn ? [target] : [1, 2];
            invocation.ToList(context).Should().Equal(expected);
            return new WeakReference(operand);
        }
    }
}
