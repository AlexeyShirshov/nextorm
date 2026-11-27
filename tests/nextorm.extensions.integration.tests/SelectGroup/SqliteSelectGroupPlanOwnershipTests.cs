using System.Data.Common;
using System.Reflection;
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

    [Fact]
    public void RawRecipe_ReusesGroupingWithoutRetainingTheFirstComponent()
    {
        using var firstConnection = new SqliteConnection("Data Source=:memory:");
        using var secondConnection = new SqliteConnection("Data Source=:memory:");
        Seed(firstConnection);
        Seed(secondConnection);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder());
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder());

        var (firstPlan, firstComponent, _) = Execute(first);
        var before = RecipeReuses();
        var (secondPlan, _, keptFreshComponent) = Execute(second);
        RecipeReuses().Should().Be(before + 1, "the second command must stage the raw recipe");
        secondPlan.Should().BeSameAs(firstPlan);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var assertions = new AssertionScope();
        firstComponent.IsAlive.Should().BeFalse("neither the raw shape nor the recipe should own the first component");
        keptFreshComponent.Should().BeTrue("staging must preserve the current command's component");
        GC.KeepAlive(firstPlan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Plan, WeakReference Component, bool KeptOwnComponent) Execute(SqliteDataContext context)
        {
            var command = context.From<GroupRow>()
                .WithTag("raw select group ownership")
                .SelectGroupHaving(row => row.Id, id => id > 1);
            var component = command.Components!.Single();
            var invocation = context.GetPreparedQueryCommand(command, false, true, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(2);
            return (PreparedPlanObservation.GetPlan(invocation), new WeakReference(component),
                ReferenceEquals(command.Components!.Single(), component));
        }
    }

    [Fact]
    public void RawRecipe_RebindsHavingWithoutRetainingItsCapture()
    {
        using var firstConnection = new SqliteConnection("Data Source=:memory:");
        using var secondConnection = new SqliteConnection("Data Source=:memory:");
        Seed(firstConnection);
        Seed(secondConnection);
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (plan, capture) = Warm(first, second);
        firstTap.Parameters.Should().Equal((0, "Target", 1));
        secondTap.Parameters.Should().Equal((0, "Target", 2));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        capture.IsAlive.Should().BeFalse("neither the raw selector nor the executable recipe should own the HAVING capture");
        GC.KeepAlive(plan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Plan, WeakReference Capture) Warm(SqliteDataContext first, SqliteDataContext second)
        {
            var owner = new HavingOwner(1);
            var token = TestContext.Current.CancellationToken;
            QueryCommand<int> Build(SqliteDataContext context) => context.From<GroupRow>()
                .WithTag("captured raw select group ownership")
                .SelectGroupHaving(row => row.Id, id => id == owner.Target);

            var cold = first.GetPreparedQueryCommand(Build(first), false, true, token);
            cold.ToList(first).Should().Equal(1);
            owner.Target = 2;
            var before = RecipeReuses();
            var warm = second.GetPreparedQueryCommand(Build(second), false, true, token);
            warm.ToList(second).Should().Equal(2);
            RecipeReuses().Should().Be(before + 1, "the second command must stage the raw recipe");
            var plan = PreparedPlanObservation.GetPlan(cold);
            PreparedPlanObservation.GetPlan(warm).Should().BeSameAs(plan);
            return (plan, new WeakReference(owner));
        }
    }

    private static int RecipeReuses() => (int)typeof(DataContext).Assembly.GetType("NextORM.Core.QueryPlanner", throwOnError: true)!
        .GetProperty("RecipeReuses", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    [Fact]
    public void RawRecipe_ReusesUnionWithoutRetainingTheFirstOperandsComponent()
    {
        using var firstConnection = new SqliteConnection("Data Source=:memory:");
        using var secondConnection = new SqliteConnection("Data Source=:memory:");
        Seed(firstConnection);
        Seed(secondConnection);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder());
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder());

        var (firstPlan, firstComponent, _) = Execute(first);
        var before = RecipeReuses();
        var (secondPlan, _, keptFreshComponent) = Execute(second);
        RecipeReuses().Should().Be(before + 1, "the second command must stage the raw set-operation recipe");
        secondPlan.Should().BeSameAs(firstPlan);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var assertions = new AssertionScope();
        firstComponent.IsAlive.Should().BeFalse("a nested raw recipe must not own the first operand's component");
        keptFreshComponent.Should().BeTrue("staging must preserve the component of the current operand");
        GC.KeepAlive(firstPlan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Plan, WeakReference Component, bool KeptOwnComponent) Execute(SqliteDataContext context)
        {
            var right = context.From<GroupRow>().SelectGroupHaving(row => row.Id, id => id > 1);
            var component = right.Components!.Single();
            var command = context.From<GroupRow>()
                .WithTag("raw union grouping ownership")
                .Where(row => row.Id == 1)
                .Select(row => row.Id)
                .UnionAll(right);
            var invocation = context.GetPreparedQueryCommand(command, false, true, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().BeEquivalentTo(new[] { 1, 2 });
            return (PreparedPlanObservation.GetPlan(invocation), new WeakReference(component),
                ReferenceEquals(command.UnionQuery!.Components!.Single(), component));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRecipe_ReusesCteWithoutRetainingTheFirstBodyComponent(bool withSibling)
    {
        using var firstConnection = new SqliteConnection("Data Source=:memory:");
        using var secondConnection = new SqliteConnection("Data Source=:memory:");
        Seed(firstConnection);
        Seed(secondConnection);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder());
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder());

        var (firstPlan, firstComponent, _) = Execute(first, withSibling);
        var before = RecipeReuses();
        var (secondPlan, _, keptFreshComponent) = Execute(second, withSibling);
        RecipeReuses().Should().Be(before + 1, "the second command must stage the raw CTE recipe");
        secondPlan.Should().BeSameAs(firstPlan);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var assertions = new AssertionScope();
        firstComponent.IsAlive.Should().BeFalse("the raw CTE recipe must not own the first definition's component");
        keptFreshComponent.Should().BeTrue("staging must preserve the component of the current CTE body");
        GC.KeepAlive(firstPlan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Plan, WeakReference Component, bool KeptOwnComponent) Execute(
            SqliteDataContext context, bool withSibling)
        {
            const string name = "issue16_owned_group";
            var body = context.From<GroupRow>().SelectGroupHaving(row => row.Id, id => id > 1);
            var component = body.Components!.Single();
            var ctes = withSibling
                ? context.With("issue16_other_group", context.From<GroupRow>().Select(row => row.Id)).With(name, body)
                : context.With(name, body);
            var command = ctes.From(name).Select(row => row["Id"].AsInt);
            var invocation = context.GetPreparedQueryCommand(command, false, true, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(2);
            var preparedBody = command.Ctes!.Single(definition => definition.Name == name).Query;
            return (PreparedPlanObservation.GetPlan(invocation), new WeakReference(component),
                ReferenceEquals(preparedBody.Components!.Single(), component));
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

    private sealed class HavingOwner(int target)
    {
        internal int Target { get; set; } = target;
    }

    [SqlTable("issue16_group_rows")]
    public sealed class GroupRow
    {
        public int Id { get; set; }
    }
}
