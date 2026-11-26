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
}
