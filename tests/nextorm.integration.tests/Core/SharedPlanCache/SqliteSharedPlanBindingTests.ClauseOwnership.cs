using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClausePlan_ReusesItsHolderWithoutRetainingTheFirstOwner(bool join)
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (firstOwner, firstPlan) = Execute(first, useJoin: join, firstCall: true);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOwner.IsAlive.Should().BeFalse("the normalized key must not retain the authored clause owner");

        var (_, secondPlan) = Execute(second, useJoin: join, firstCall: false);
        secondPlan.Should().BeSameAs(firstPlan);
        if (join)
        {
            firstTap.LastParameters.Should().Equal((0, "JoinTarget", 1));
            secondTap.LastParameters.Should().Equal((0, "JoinTarget", 2));
        }
        else
        {
            firstTap.LastParameters.Should().Equal((0, "SortDirection", 1));
            secondTap.LastParameters.Should().Equal((0, "SortDirection", -1));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int>? Plan) Execute(
            SqliteDataContext context, bool useJoin, bool firstCall)
        {
            var owner = new ClauseOwner(firstCall ? 1 : 2, firstCall ? 1 : -1);
            var command = useJoin
                ? context.From<BindingRow>()
                    .Join(context.From<CorrelationRow>(), (left, right) => left.Id == right.Id && right.Id == owner.JoinTarget)
                    .Select(pair => pair.Item1.Id)
                : context.From<BindingRow>().OrderBy(row => row.Id * owner.SortDirection).Select(row => row.Id);
            // Cache the ordinary plan without publishing an executable raw recipe for this graph.
            var invocation = context.GetStreamingRowsQueryCommand(command, TestContext.Current.CancellationToken);
            int[] expected = useJoin ? [owner.JoinTarget] : firstCall ? [1, 2] : [2, 1];
            invocation.ToList(context).Should().Equal(expected);
            return (new WeakReference(owner), BoundQueryInvocation<int>.GetPlan(invocation));
        }
    }

    [Fact]
    public void RawRecipe_FingerprintCollision_DoesNotAdoptAnotherSortExpression()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var token = TestContext.Current.CancellationToken;
        var source = context.From<BindingRow>().WithTag("raw sort collision");
        var owner = source.OrderBy(row => row.Id).Select(row => row.Id);
        var fresh = source.OrderBy(row => -row.Id).Select(row => row.Id);
        var ownerShape = QueryCommand.CaptureRawShape(owner)!;
        var freshShape = QueryCommand.CaptureRawShape(fresh)!;
        var ownerInvocation = context.GetPreparedQueryCommand(owner, false, true, token);
        ownerInvocation.ToList(context).Should().Equal(1, 2);

        ownerShape.Fingerprint = freshShape.Fingerprint;
        QueryPlanStore.SetRaw(context.GetType(), owner.GetOrCreatePlanKey(null),
            BoundQueryInvocation<int>.GetPlan(ownerInvocation)!, ownerShape, owner);

        var rawMatch = QueryPlanStore.TryGetRaw(context.GetType(), freshShape, fresh, out _);
        context.GetPreparedQueryCommand(fresh, false, true, token).ToList(context).Should().Equal(2, 1);
        rawMatch.Should().BeFalse("raw comparison must check the authored sort expression inside the bucket");
    }

    private sealed record ClauseOwner(int JoinTarget, int SortDirection);
}
