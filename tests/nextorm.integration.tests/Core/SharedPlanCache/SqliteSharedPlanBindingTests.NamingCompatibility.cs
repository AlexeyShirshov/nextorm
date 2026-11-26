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

    [Fact]
    public void NormalizedPlan_DoesNotRetainTheCustomNamingConvention()
    {
        var (convention, holder) = Execute();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        convention.IsAlive.Should().BeFalse("the normalized cache key must not retain a caller's naming convention");
        QueryPlanStore.Values.Should().Contain(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Convention, DbPreparedQueryCommand<int> Holder) Execute()
        {
            using var connection = OpenNamingConnection();
            var convention = new EqualNamingConvention("right_value");
            using var context = CreateNamingContext(connection, convention);
            var invocation = context.GetStreamingRowsQueryCommand(NamingQuery(context), TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(99);
            return (new WeakReference(convention), BoundQueryInvocation<int>.GetPlan(invocation)!);
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
