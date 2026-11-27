using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Data.Sqlite;
using NextORM.Extensions;
using NextORM.Sqlite;
using NextORM.Tests;

namespace NextORM.Integration.Tests;

[Collection("Query plan cache")]
public sealed class SqliteSelectGroupPlanOwnershipTests
{
    [Fact]
    public void NormalizedPlan_RebindsHavingWithoutRetainingTheFirstComponent()
    {
        using var firstConnection = new SqliteConnection("Data Source=:memory:");
        using var secondConnection = new SqliteConnection("Data Source=:memory:");
        Seed(firstConnection);
        Seed(secondConnection);
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (firstPlan, firstComponent, firstOwner) = Execute(first, 1);
        var (secondPlan, _, _) = Execute(second, 2);
        secondPlan.Should().BeSameAs(firstPlan, "captured scalar values are invocation inputs, not plan keys");
        firstTap.Parameters.Should().Equal((0, "Target", 1));
        secondTap.Parameters.Should().Equal((0, "Target", 2));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var assertions = new AssertionScope();
        firstComponent.IsAlive.Should().BeFalse("the normalized key must not own the original component");
        firstOwner.IsAlive.Should().BeFalse("the normalized key must not own the HAVING capture");
        GC.KeepAlive(firstPlan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Plan, WeakReference Component, WeakReference Owner) Execute(
            SqliteDataContext context, int target)
        {
            var owner = new HavingOwner(target);
            var command = context.From<GroupRow>()
                .WithTag("normalized select group ownership")
                .SelectGroupHaving(row => row.Id, id => id == owner.Target);
            var token = TestContext.Current.CancellationToken;
            // Preparing the live shape first excludes raw staging without disabling normalized reuse.
            command.PrepareCommand(token);
            var invocation = context.GetPreparedQueryCommand(command, false, true, token);
            invocation.ToList(context).Should().Equal(target);
            return (PreparedPlanObservation.GetPlan(invocation), new WeakReference(command.Components!.Single()),
                new WeakReference(owner));
        }
    }

    private static void Seed(SqliteConnection connection)
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "create table issue16_group_rows (Id integer not null); insert into issue16_group_rows values (1), (2);";
        command.ExecuteNonQuery();
    }

    private sealed class BindingTap : IQueryInterceptor
    {
        internal (int Ordinal, string Name, int Value)[]? Parameters { get; private set; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            Parameters = command.Parameters.Cast<DbParameter>()
                .Select((parameter, ordinal) => (ordinal, parameter.ParameterName, (int)parameter.Value!))
                .ToArray();
        }
    }

    private sealed record HavingOwner(int Target);

    [SqlTable("issue16_group_rows")]
    public sealed class GroupRow
    {
        public int Id { get; set; }
    }
}
