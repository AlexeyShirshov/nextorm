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
public sealed class SqliteExtremeRowPlanOwnershipTests
{
    [Fact]
    public void NormalizedPlan_RebindsRankingWithoutRetainingTheFirstCommand()
    {
        var (firstPlan, firstCommand, firstComponent, firstOwner) = Execute(1);
        var (secondPlan, _, _, _) = Execute(-1);
        secondPlan.Should().BeSameAs(firstPlan, "different bound sort multipliers must reuse the statement");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var assertions = new AssertionScope();
        firstCommand.IsAlive.Should().BeFalse("the normalized descriptor must not keep the preparation comparer");
        firstComponent.IsAlive.Should().BeFalse("the normalized descriptor must not own the original component");
        firstOwner.IsAlive.Should().BeFalse("the normalized descriptor must not own the selector capture");
        GC.KeepAlive(firstPlan);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DbPreparedQueryCommand<int> Plan, WeakReference Command, WeakReference Component, WeakReference Owner)
        Execute(int direction)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seed = connection.CreateCommand())
        {
            seed.CommandText = "create table issue16_extreme_rows (Id integer not null); insert into issue16_extreme_rows values (1), (2);";
            seed.ExecuteNonQuery();
        }

        var tap = new BindingTap();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));
        var owner = new RankOwner(direction);
        var query = ExtremeRowExtensions.SelectWhereMin(context.From<RankRow>().WithTag("normalized rank ownership"),
            row => row.Id * owner.Direction, row => row.Id);
        var token = TestContext.Current.CancellationToken;
        query.PrepareCommand(token); // Exclude raw staging and publication, not normalized lookup.
        var invocation = context.GetPreparedQueryCommand(query, false, true, token);
        invocation.ToList(context).Should().Equal(direction == 1 ? 1 : 2);
        tap.Parameters.Should().Equal((0, "Direction", direction));
        return (PreparedPlanObservation.GetPlan(invocation), new WeakReference(query),
            new WeakReference(query.Components!.Single()), new WeakReference(owner));
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

    private sealed record RankOwner(int Direction);

    [SqlTable("issue16_extreme_rows")]
    public sealed class RankRow
    {
        public int Id { get; set; }
    }
}
