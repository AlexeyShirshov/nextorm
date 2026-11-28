using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Query plan cache")]
public sealed class SqliteSharedPlanStreamingTests
{
    private const string TableName = "issue16_streaming_rows";

    [Fact]
    public void CachedStreamingPlan_DoesNotRetainThePlanningContext()
    {
        var (contextReference, cachedPlan) = WarmPlanAndDropContext();

        // Don't inspect the weak reference until after collection: the JIT can keep an
        // observed target live across a collection in the same method.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        contextReference.IsAlive.Should().BeFalse("a warm plan must not root the context that prepared it");
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        Query(context, 1).ToEnumerable().Should().Equal(2);
        FindCachedPlan().Should().BeSameAs(cachedPlan, "the original plan must remain warm throughout the test");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Context, DbPreparedQueryCommand<int> Plan) WarmPlanAndDropContext()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        Query(context, 0).ToList().Should().Equal(1, 2);
        var cachedPlan = FindCachedPlan();

        Query(context, 1).ToEnumerable().Should().Equal(2);
        FindCachedPlan().Should().BeSameAs(cachedPlan, "streaming reuses a plan first prepared for buffered execution");
        cachedPlan.StreamingEnabled.Should().BeTrue();

        return (new WeakReference(context), cachedPlan);
    }

    private static DbPreparedQueryCommand<int> FindCachedPlan() => QueryPlanStore.Values
        .OfType<DbPreparedQueryCommand<int>>()
        .Single(plan => plan.CommandText.Contains(TableName, StringComparison.Ordinal));

    private static QueryCommand<int> Query(SqliteDataContext context, int minimum) =>
        context.From<StreamRow>().Where(row => row.Id > minimum).OrderBy(row => row.Id).Select(row => row.Id);

    private static SqliteConnection OpenSeededConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"create table {TableName} (Id integer not null); insert into {TableName} values (1), (2);";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    [SqlTable(TableName)]
    public sealed class StreamRow
    {
        public int Id { get; set; }
    }
}
