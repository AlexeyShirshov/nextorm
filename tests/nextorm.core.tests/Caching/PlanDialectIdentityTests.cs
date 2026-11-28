using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Core.Tests;

[Collection("Query cache controls")]
public sealed class PlanDialectIdentityTests
{
    [Fact]
    public void ImplicitPlan_SeparatesDifferentVersionsAndReusesEquivalentVersions()
    {
        const string connectionString = "Server=localhost;Database=placeholder";
        using var first = new MariaDbDataContext(connectionString, new DataContextBuilder(), new Version(10, 11));
        using var different = new MariaDbDataContext(connectionString, new DataContextBuilder(), new Version(11, 4));
        using var equivalent = new MariaDbDataContext(connectionString, new DataContextBuilder(), new Version(10, 11));

        var firstPlan = Plan(first);
        Plan(different).Should().NotBeSameAs(firstPlan);
        Plan(equivalent).Should().BeSameAs(firstPlan);

        static IPreparedQueryCommand<int> Plan(MariaDbDataContext context)
            => context.GetPreparedQueryCommand(context.From<DialectRow>().Select(row => row.Id),
                false, true, CancellationToken.None);
    }

    [SqlTable("dialect_plan_rows")]
    private sealed class DialectRow
    {
        public int Id { get; set; }
    }
}
