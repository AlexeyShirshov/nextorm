using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void RawRecipe_RebindsScalarProjectionWithoutRetainingTheFirstOwner()
    {
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));

        var (owner, holder) = Execute(context, tap);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        owner.IsAlive.Should().BeFalse("the raw projection must not keep the first capture alive");
        QueryPlanStore.Values.Should().Contain(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Holder) Execute(SqliteDataContext context, BindingTap tap)
        {
            var owner = new WhereOwner { Target = 10 };
            QueryCommand<int> Build() => context.From<BindingRow>().WithTag("raw scalar projection ownership")
                .Where(row => row.Id == 1).Select(row => row.Id + owner.Target);

            var token = TestContext.Current.CancellationToken;
            var cold = context.GetPreparedQueryCommand(Build(), false, true, token);
            cold.ToList(context).Should().Equal(11);
            tap.LastParameters.Should().Equal((0, "Target", 10));

            owner.Target = 20;
            var fresh = Build();
            var reuses = QueryPlanner.RecipeReuses;
            var warm = context.GetPreparedQueryCommand(fresh, false, true, token);
            warm.ToList(context).Should().Equal(21);
            tap.LastParameters.Should().Equal((0, "Target", 20));
            QueryPlanner.RecipeReuses.Should().BeGreaterThan(reuses, "the raw preparation path must still be used");
            var holder = BoundQueryInvocation<int>.GetPlan(cold)!;
            BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(holder);
            return (new WeakReference(owner), holder);
        }
    }

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

    [Fact]
    public void NamedWindowPlan_RebindsWithoutRetainingItsFirstOrderKeyOwner()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (firstOwner, firstPlan) = Execute(first, 1, [1, 2]);
        var (_, secondPlan) = Execute(second, -1, [2, 1]);
        secondPlan.Should().BeSameAs(firstPlan);
        firstTap.LastParameters.Should().Equal((0, "Direction", 1));
        secondTap.LastParameters.Should().Equal((0, "Direction", -1));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOwner.IsAlive.Should().BeFalse("the normalized window definition must not own the first order key capture");
        QueryPlanStore.Values.Should().Contain(firstPlan);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Plan) Execute(SqliteDataContext context,
            int direction, int[] expected)
        {
            var owner = new SortOwner { Direction = direction };
            var source = context.From<BindingRow>();
            var command = source.Window("binding_order", orderBy: [source.Asc(row => row.Id * owner.Direction)])
                .OrderBy(row => row.Id).Select(row => SqlFunctions.Sql.row_number().Over("binding_order"));
            var invocation = context.GetStreamingRowsQueryCommand(command, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(expected);
            return (new WeakReference(owner), BoundQueryInvocation<int>.GetPlan(invocation)!);
        }
    }

    [Fact]
    public void RawRecipe_UsesTheFreshNamedWindowWithoutRetainingItsOwner()
    {
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));
        var (owner, holder) = Execute(context, tap);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        owner.IsAlive.Should().BeFalse("neither the raw descriptor nor its recipe should retain the named window capture");
        QueryPlanStore.Values.Should().Contain(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Plan) Execute(SqliteDataContext context, BindingTap tap)
        {
            var owner = new SortOwner { Direction = 1 };
            var source = context.From<BindingRow>().WithTag("raw named window ownership");
            QueryCommand<int> Build() => source.Window("binding_order", orderBy: [source.Asc(row => row.Id * owner.Direction)])
                .OrderBy(row => row.Id).Select(row => SqlFunctions.Sql.row_number().Over("binding_order"));
            var token = TestContext.Current.CancellationToken;
            var cold = context.GetPreparedQueryCommand(Build(), false, true, token);
            cold.ToList(context).Should().Equal(1, 2);
            tap.LastParameters.Should().Equal((0, "Direction", 1));

            owner.Direction = -1;
            var fresh = Build();
            var windows = fresh.Windows;
            var reuses = QueryPlanner.RecipeReuses;
            var warm = context.GetPreparedQueryCommand(fresh, false, true, token);
            warm.ToList(context).Should().Equal(2, 1);
            tap.LastParameters.Should().Equal((0, "Direction", -1));
            QueryPlanner.RecipeReuses.Should().BeGreaterThan(reuses);
            fresh.Windows.Should().BeSameAs(windows);
            var holder = BoundQueryInvocation<int>.GetPlan(cold)!;
            BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(holder);
            return (new WeakReference(owner), holder);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRecipe_UsesTheFreshWhereWithoutRetainingItsFirstOwner(bool compound)
    {
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));

        var (owner, holder, usedFreshWhere) = Warm(context, tap, compound);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        owner.IsAlive.Should().BeFalse("the live raw recipe and descriptor must not keep the first WHERE owner");
        usedFreshWhere.Should().BeTrue("staging should install the current command's own predicate");
        QueryPlanStore.Values.Should().Contain(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Holder, bool UsedFreshWhere) Warm(
            SqliteDataContext context, BindingTap tap, bool compound)
        {
            var owner = new WhereOwner { Target = 1 };
            QueryCommand<int> Build()
            {
                var source = context.From<BindingRow>().WithTag("raw where ownership");
                var filtered = compound
                    ? source.Where(row => row.Id > 0 && (row.Id < 0 || row.Id == owner.Target))
                    : source.Where(row => row.Id == owner.Target);
                return filtered.OrderBy(row => row.Id).Select(row => row.Id);
            }

            var token = TestContext.Current.CancellationToken;
            var first = context.GetPreparedQueryCommand(Build(), false, true, token);
            first.ToList(context).Should().Equal(1);
            tap.LastParameters.Should().Equal((0, "Target", 1));

            owner.Target = 2;
            var fresh = Build();
            var shape = QueryCommand.CaptureRawShape(fresh)!;
            QueryPlanStore.TryGetRaw(context.GetType(), shape, fresh, out _).Should().BeTrue();
            var reuses = QueryPlanner.RecipeReuses;
            var repeated = context.GetPreparedQueryCommand(fresh, false, true, token);
            repeated.ToList(context).Should().Equal(2);
            tap.LastParameters.Should().Equal((0, "Target", 2));
            QueryPlanner.RecipeReuses.Should().BeGreaterThan(reuses, "the existing raw fast path must still be used");
            var holder = BoundQueryInvocation<int>.GetPlan(first)!;
            BoundQueryInvocation<int>.GetPlan(repeated).Should().BeSameAs(holder);
            return (new WeakReference(owner), holder, ReferenceEquals(fresh.PreparedCondition, shape.Condition));
        }
    }

    private sealed class WhereOwner
    {
        public int Target { get; set; }
    }

    [Fact]
    public void RawRecipe_RebindsSortWithoutRetainingTheFirstOwner()
    {
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));

        var (firstOwner, holder, usesFreshSort) = Warm(context, tap);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        QueryPlanStore.Values.Should().Contain(holder);
        usesFreshSort.Should().BeTrue("staging should use the fresh sort expression");
        firstOwner.IsAlive.Should().BeFalse("the raw ORDER BY descriptor and recipe must not own the capture");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Holder, bool UsesFreshSort) Warm(
            SqliteDataContext context, BindingTap tap)
        {
            var owner = new SortOwner { Direction = 1 };
            QueryCommand<int> Build() => context.From<BindingRow>().WithTag("raw sort ownership")
                .OrderBy(row => row.Id * owner.Direction).Select(row => row.Id);

            var token = TestContext.Current.CancellationToken;
            var first = context.GetPreparedQueryCommand(Build(), false, true, token);
            first.ToList(context).Should().Equal(1, 2);
            tap.LastParameters.Should().Equal((0, "Direction", 1));

            owner.Direction = -1;
            var reuses = QueryPlanner.RecipeReuses;
            var fresh = Build();
            var sort = fresh.Sorting![0].SortExpression;
            var raw = QueryCommand.CaptureRawShape(fresh)!;
            QueryPlanStore.TryGetRaw(context.GetType(), raw, fresh, out var rawCandidate).Should().BeTrue();
            rawCandidate!.Recipe.Sorting.Should().BeNull("a replayable key must not be stored as executable syntax");
            var second = context.GetPreparedQueryCommand(fresh, false, true, token);
            second.ToList(context).Should().Equal(2, 1);
            tap.LastParameters.Should().Equal((0, "Direction", -1));
            QueryPlanner.RecipeReuses.Should().BeGreaterThan(reuses, "the check must keep the raw fast path active");
            var holder = BoundQueryInvocation<int>.GetPlan(first)!;
            BoundQueryInvocation<int>.GetPlan(second).Should().BeSameAs(holder);
            return (new WeakReference(owner), holder, ReferenceEquals(fresh.Sorting![0].PreparedExpression, sort));
        }
    }

    private sealed class SortOwner
    {
        public int Direction { get; set; }
    }
}
