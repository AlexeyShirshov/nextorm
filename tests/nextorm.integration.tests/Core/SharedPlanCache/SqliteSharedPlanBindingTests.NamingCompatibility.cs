using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void RawRecipe_RejectsEqualCustomConventionsThatChooseDifferentColumns()
    {
        using var firstConnection = OpenNamingConnection();
        using var secondConnection = OpenNamingConnection();
        using var compatibleConnection = OpenNamingConnection();
        var left = new EqualNamingConvention("left_value");
        var right = new EqualNamingConvention("right_value");
        using var first = CreateNamingContext(firstConnection, left);
        using var second = CreateNamingContext(secondConnection, right);
        using var compatible = CreateNamingContext(compatibleConnection, left);
        var token = TestContext.Current.CancellationToken;

        left.Equals(right).Should().BeTrue("the user equality callback does not establish SQL compatibility");
        var owner = NamingQuery(first);
        var ownerShape = QueryCommand.CaptureRawShape(owner)!;
        var firstInvocation = first.GetPreparedQueryCommand(owner, false, true, token);
        firstInvocation.ToList(first).Should().Equal(41);
        second.GetPreparedQueryCommand(NamingQuery(second), false, false, token).ToList(second).Should().Equal(99);

        var fresh = NamingQuery(second);
        var freshShape = QueryCommand.CaptureRawShape(fresh)!;
        ownerShape.Fingerprint = freshShape.Fingerprint;
        QueryPlanStore.SetRaw(first.GetType(), owner.GetOrCreatePlanKey(null),
            BoundQueryInvocation<int>.GetPlan(firstInvocation)!, ownerShape, owner);
        var rawMatch = QueryPlanStore.TryGetRaw(second.GetType(), freshShape, fresh, out _);

        var secondInvocation = second.GetPreparedQueryCommand(fresh, false, true, token);
        secondInvocation.ToList(second).Should().Equal(99);
        rawMatch.Should().BeFalse();
        BoundQueryInvocation<int>.GetPlan(secondInvocation).Should().NotBeSameAs(BoundQueryInvocation<int>.GetPlan(firstInvocation));

        var reuses = QueryPlanner.RecipeReuses;
        var repeated = compatible.GetPreparedQueryCommand(NamingQuery(compatible), false, true, token);
        repeated.ToList(compatible).Should().Equal(41);
        QueryPlanner.RecipeReuses.Should().BeGreaterThan(reuses);
        BoundQueryInvocation<int>.GetPlan(repeated).Should().BeSameAs(BoundQueryInvocation<int>.GetPlan(firstInvocation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_DoesNotRetainTheCustomNamingConvention(bool rawRecipe)
    {
        var (convention, holder) = Execute(rawRecipe);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        convention.IsAlive.Should().BeFalse("neither the normalized key nor the raw recipe may retain a caller's naming convention");
        QueryPlanStore.Values.Should().Contain(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Convention, DbPreparedQueryCommand<int> Holder) Execute(bool rawRecipe)
        {
            using var connection = OpenNamingConnection();
            var convention = new EqualNamingConvention("right_value");
            using var context = CreateNamingContext(connection, convention);
            var token = TestContext.Current.CancellationToken;
            var invocation = rawRecipe
                ? context.GetPreparedQueryCommand(NamingQuery(context), false, true, token)
                : context.GetStreamingRowsQueryCommand(NamingQuery(context), token);
            invocation.ToList(context).Should().Equal(99);
            var holder = BoundQueryInvocation<int>.GetPlan(invocation)!;
            if (rawRecipe)
            {
                using var otherConnection = OpenNamingConnection();
                using var other = CreateNamingContext(otherConnection, convention);
                var reuses = QueryPlanner.RecipeReuses;
                var warm = other.GetPreparedQueryCommand(NamingQuery(other), false, true, token);
                warm.ToList(other).Should().Equal(99);
                QueryPlanner.RecipeReuses.Should().Be(reuses + 1);
                BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(holder);
            }
            return (new WeakReference(convention), holder);
        }
    }

    private static SqliteConnection OpenNamingConnection()
    {
        var connection = OpenSeededConnection();
        try
        {
            using var seed = connection.CreateCommand();
            seed.CommandText = $"alter table {TableName} add column left_value integer not null default 41; "
                + $"alter table {TableName} add column right_value integer not null default 99;";
            seed.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static SqliteDataContext CreateNamingContext(SqliteConnection connection, INamingConvention naming)
        => new(connection, new DataContextBuilder().UseNamingConvention(naming));

    private static QueryCommand<int> NamingQuery(SqliteDataContext context)
        => context.From<NamingRow>().Where(row => row.Id == 1).OrderBy(row => row.Id).Select(row => row.ProbeValue);

    [SqlTable(TableName)]
    public sealed class NamingRow
    {
        public int Id { get; set; }
        public int ProbeValue { get; set; }
    }

    private sealed class EqualNamingConvention(string column) : INamingConvention
    {
        public string TableName(string clrName, bool isInterface) => clrName;
        public string ColumnName(string propertyName) => propertyName == nameof(NamingRow.ProbeValue) ? column : propertyName;
        public override bool Equals(object? obj) => obj is EqualNamingConvention;
        public override int GetHashCode() => 0;
    }
}
