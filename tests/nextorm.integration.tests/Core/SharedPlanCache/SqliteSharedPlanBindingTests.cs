using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Data.Sqlite;
using NextORM.QueryComponents;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Query plan cache")]
public sealed partial class SqliteSharedPlanBindingTests
{
    private const string TableName = "issue16_binding_rows";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeAny_DoesNotUseThePlainAliasForAComponent(bool asynchronous)
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var plain = Filter(context, 1);
        plain.Any().Should().BeTrue();
        var hits = QueryPlanStore.NativeHits;
        plain.Any().Should().BeTrue();
        QueryPlanStore.NativeHits.Should().Be(hits + 1);

        var filtered = plain.WithComponent(new AdditivePredicateComponent<BindingRow>(BeyondSeededRows));
        var found = asynchronous ? await filtered.AnyAsync(TestContext.Current.CancellationToken) : filtered.Any();
        found.Should().BeFalse("the component excludes both rows from the EXISTS subquery");
        QueryPlanStore.NativeHits.Should().Be(hits + 1);
    }

    [Fact]
    public void ScalarCount_DoesNotUseThePlainArtifactForAComponent()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var plain = context.From<BindingRow>();
        plain.Count().Should().Be(2);
        plain.WithComponent(new AdditivePredicateComponent<BindingRow>(BeyondSeededRows)).Count().Should().Be(0);
        plain.Count().Should().Be(2);
    }

    private static readonly Expression<Func<BindingRow, bool>> BeyondSeededRows = row => row.Id > 2;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeList_BindsEachInvocationWhenCheckoutReenters(bool asynchronous)
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        var token = TestContext.Current.CancellationToken;
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var warmConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        using var warm = new SqliteDataContext(warmConnection, new DataContextBuilder());

        List(warm, 1).Should().Equal(1);
        List(warm, 1).Should().Equal(1);
        var previousHits = QueryPlanStore.NativeHits;
        firstTap.OnInitialized = () => List(second, 2).Should().Equal(2);

        var rows = asynchronous ? await ListAsync(first, 1, token) : List(first, 1);

        using var assertions = new AssertionScope();
        rows.Should().Equal(1);
        QueryPlanStore.NativeHits.Should().BeGreaterThanOrEqualTo(previousHits + 2,
            "both executions must select the warm native plan");
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeAny_BindsEachInvocationWhenCheckoutReenters(bool asynchronous)
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        var token = TestContext.Current.CancellationToken;
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var warmConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        using var warm = new SqliteDataContext(warmConnection, new DataContextBuilder());

        // Build each context's Any wrapper without minting a command for the native statement.
        Filter(first, 1).OrderBy(row => row.Id).Any().Should().BeTrue();
        Filter(second, 1).OrderBy(row => row.Id).Any().Should().BeTrue();
        Filter(warm, 1).Any().Should().BeTrue();
        var previousHits = QueryPlanStore.NativeHits;
        firstTap.OnInitialized = () => Filter(second, 3).Any().Should().BeFalse();

        var builder = Filter(first, 1);
        var found = asynchronous ? await builder.AnyAsync(token) : builder.Any();

        using var assertions = new AssertionScope();
        found.Should().BeTrue();
        QueryPlanStore.NativeHits.Should().BeGreaterThanOrEqualTo(previousHits + 2,
            "both executions must select the warm native plan");
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryStreams_BindTheirOwnValuesAfterBothArePrepared(bool forceFallback)
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        var token = TestContext.Current.CancellationToken;
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var beforeRecipe = ParamRefreshRecipe.FastBindHits;
        var beforeFallback = QueryPlanner.FallbackRefreshes;
        await using var stream1 = OrdinaryStream(first, 1, forceFallback, token);
        await using var stream2 = OrdinaryStream(second, 2, forceFallback, token);

        (await stream1.MoveNextAsync()).Should().BeTrue();
        (await stream2.MoveNextAsync()).Should().BeTrue();

        using var assertions = new AssertionScope();
        stream1.Current.Should().Be(1);
        stream2.Current.Should().Be(2);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 2));
        if (forceFallback)
            QueryPlanner.FallbackRefreshes.Should().BeGreaterThan(beforeFallback);
        else
            ParamRefreshRecipe.FastBindHits.Should().BeGreaterThan(beforeRecipe);
        (await stream1.MoveNextAsync()).Should().BeFalse();
        (await stream2.MoveNextAsync()).Should().BeFalse();
    }

    [Fact]
    public void FirstOrdinaryInvocation_BindsItsValueWithoutKeepingItInThePlan()
    {
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));

        // Change the shape, so prior binding tests can't have populated this entry.
        var query = Filter(context, 2).OrderByDescending(row => row.Id).Select(row => row.Id);
        var invocation = context.GetPreparedQueryCommand(query, false, true, TestContext.Current.CancellationToken);
        var plan = BoundQueryInvocation<int>.GetPlan(invocation)!;

        invocation.ToList(context).Should().Equal(2);
        tap.LastParameters.Should().Equal((0, "target", 2));
        using var assertions = new AssertionScope();
        plan.ParamSeeds.Single().Value.Should().BeNull("a cached factory must not retain the first capture");
        plan.DbCommandParams[0].Value.Should().Be(DBNull.Value, "the diagnostic descriptor has no invocation value");
        QueryPlanStore.Values.Should().Contain(plan, "the assertion concerns a published plan");
    }

    [Fact]
    public void DerivedProjection_DoesNotPublishItsColumnsAsEntityMetadata()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var inner = context.From(TableName).Select(row => new
        {
            Id = SqlFunctions.Column<int>(row, "Id"),
            DerivedValue = SqlFunctions.Column<int>(row, "Id") + 10
        });
        var source = context.From(inner);

        var values = context.From(source).OrderBy(row => row.Id).Select(row => row.DerivedValue).ToList();

        values.Should().Equal(11, 12);
        DataContextCache.SelectListCache.ContainsKey(inner.ResultType!).Should().BeFalse(
            "a derived column shape belongs to its defining query, not to the shared CLR type");
    }

    [Fact]
    public void DictionaryLookup_RebindsWithoutKeepingTheEvaluatedEntriesOnTheKey()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstQuery = Build(first, new Dictionary<int, int> { [1] = 10, [2] = 20 });
        var secondQuery = Build(second, new Dictionary<int, int> { [1] = 20, [2] = 10 });
        QueryCommand.CaptureRawShape(firstQuery).Should().BeNull("lookup entries must be evaluated from the current command");
        var token = TestContext.Current.CancellationToken;
        var firstInvocation = first.GetPreparedQueryCommand(firstQuery, false, true, token);
        var secondInvocation = second.GetPreparedQueryCommand(secondQuery, false, true, token);

        firstInvocation.ToList(first).Should().Equal(2);
        secondInvocation.ToList(second).Should().Equal(1);
        firstTap.LastParameters.Should().Equal((0, "p0", 1), (1, "p1", 10), (2, "p2", 2), (3, "p3", 20));
        secondTap.LastParameters.Should().Equal((0, "p0", 1), (1, "p1", 20), (2, "p2", 2), (3, "p3", 10));
        BoundQueryInvocation<int>.GetPlan(secondInvocation).Should().BeSameAs(BoundQueryInvocation<int>.GetPlan(firstInvocation));
        firstQuery.LookupPartitions.Should().NotBeNull();
        var key = firstQuery.GetOrCreatePlanKey(null);
        key.QueryCommand.Should().NotBeSameAs(firstQuery);
        key.QueryCommand.LookupPartitions.Should().BeNull("the published key only needs the lookup's entry count");

        static QueryCommand<int> Build(SqliteDataContext context, Dictionary<int, int> lookup)
            => context.From<BindingRow>().Where(row => lookup[row.Id] == 20).Select(row => row.Id);
    }

    [Fact]
    public void CapturedInList_ReusesThePlanAndBindsEachCallersCollection()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var token = TestContext.Current.CancellationToken;
        var firstInvocation = first.GetPreparedQueryCommand(Build(first, [1]), false, true, token);
        var secondInvocation = second.GetPreparedQueryCommand(Build(second, [2]), false, true, token);

        firstInvocation.ToList(first).Should().Equal(1);
        secondInvocation.ToList(second).Should().Equal(2);
        firstTap.LastParameters.Should().Equal((0, "p0", 1));
        secondTap.LastParameters.Should().Equal((0, "p0", 2));
        BoundQueryInvocation<int>.GetPlan(secondInvocation).Should().BeSameAs(BoundQueryInvocation<int>.GetPlan(firstInvocation));

        static QueryCommand<int> Build(SqliteDataContext context, int[] values)
            => context.From<BindingRow>().Where(row => values.Contains(row.Id)).Select(row => row.Id);
    }

    [Fact]
    public void ScalarCapture_WithHiddenProperty_BindsTheExactMember()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var owner = Expression.Constant(new DerivedTarget());
        var token = TestContext.Current.CancellationToken;

        var firstInvocation = first.GetPreparedQueryCommand(Build(first, typeof(BaseTarget)), false, true, token);
        firstInvocation.ToList(first).Should().Equal(1);
        var secondInvocation = second.GetPreparedQueryCommand(Build(second, typeof(DerivedTarget)), false, true, token);
        secondInvocation.ToList(second).Should().Equal(2);

        firstTap.LastParameters.Should().Equal((0, "Target", 1));
        secondTap.LastParameters.Should().Equal((0, "Target", 2));

        QueryCommand<int> Build(SqliteDataContext context, Type declaringType)
        {
            var row = Expression.Parameter(typeof(BindingRow), "row");
            var id = Expression.Property(row, nameof(BindingRow.Id));
            var target = Expression.Property(owner, declaringType.GetProperty(nameof(BaseTarget.Target))!);
            var predicate = Expression.Lambda<Func<BindingRow, bool>>(Expression.Equal(id, target), row);
            return context.From<BindingRow>().Where(predicate).OrderBy(item => item.Id).Select(item => item.Id);
        }
    }

    [Fact]
    public void CapturedScalarAccessor_ReusesItsShapeWithoutRetainingTheFirstOwner()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var previousAccessors = DataContextCache.ExpressionsCache.Values.ToArray();

        var firstOwner = Execute(first, 1);
        var accessor = DataContextCache.ExpressionsCache.Values.Except(previousAccessors).Should().ContainSingle().Which;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOwner.IsAlive.Should().BeFalse("the compiled accessor cache must not own the first request");

        Execute(second, 2);
        DataContextCache.ExpressionsCache.Values.Except(previousAccessors).Should().ContainSingle().Which.Should().BeSameAs(accessor);
        firstTap.LastParameters.Should().Equal((0, "Value", 1));
        secondTap.LastParameters.Should().Equal((0, "Value", 2));

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, int value)
        {
            var owner = new ScalarAccessorOwner(value);
            var row = Expression.Parameter(typeof(BindingRow), "row");
            var id = Expression.Property(row, nameof(BindingRow.Id));
            var target = Expression.Property(Expression.Constant(owner), nameof(ScalarAccessorOwner.Value));
            var predicate = Expression.Lambda<Func<BindingRow, bool>>(Expression.Equal(id, target), row);
            var query = context.From<BindingRow>().Where(predicate).OrderBy(item => item.Id).Select(item => item.Id);
            // Isolate the process-wide accessor cache from both plan indices.
            context.GetPreparedQueryCommand(query, false, false, TestContext.Current.CancellationToken)
                .ToList(context).Should().Equal(value);
            return new WeakReference(owner);
        }
    }

    private sealed record ScalarAccessorOwner(int Value);

    [Fact]
    public void RawRecipe_RejectsADifferentReceiverAtALaterOccurrence()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var a = new RawRecipeOwner(0, 3, 1);
        var b = new RawRecipeOwner(0, 3, 2);
        var token = TestContext.Current.CancellationToken;
        var firstInvocation = first.GetPreparedQueryCommand(Build(first, a), false, true, token);
        firstInvocation.ToList(first).Should().Equal(1);

        var equivalent = Build(second, a);
        var changed = Build(second, b);
        var equivalentShape = QueryCommand.CaptureRawShape(equivalent)!;
        var changedShape = QueryCommand.CaptureRawShape(changed)!;
        QueryPlanStore.TryGetRaw(second.GetType(), equivalentShape, equivalent, out var candidate).Should().BeTrue();
        candidate!.Recipe.Should().NotBeSameAs(candidate.Plan.QueryCommand,
            "the comparison-only key must not be used as a prepared recipe");
        var previousReuses = QueryPlanner.RecipeReuses;
        second.GetPreparedQueryCommand(equivalent, false, true, token).ToList(second).Should().Equal(1);
        QueryPlanner.RecipeReuses.Should().BeGreaterThan(previousReuses);

        // A repeated receiver at a different occurrence must not match even inside the same bucket.
        changedShape.Fingerprint = equivalentShape.Fingerprint;
        QueryPlanStore.TryGetRaw(second.GetType(), changedShape, changed, out _).Should().BeFalse();

        var secondInvocation = second.GetPreparedQueryCommand(changed, false, true, token);
        secondInvocation.ToList(second).Should().Equal(2);
        firstTap.LastParameters.Should().Equal((0, "Low", 0), (1, "High", 3), (2, "Target", 1));
        secondTap.LastParameters.Should().Equal((0, "Low", 0), (1, "High", 3), (2, "Target", 2));
        BoundQueryInvocation<int>.GetPlan(secondInvocation).Should().BeSameAs(BoundQueryInvocation<int>.GetPlan(firstInvocation));

        QueryCommand<int> Build(SqliteDataContext context, RawRecipeOwner target)
        {
            var row = Expression.Parameter(typeof(BindingRow), "row");
            var id = Expression.Property(row, nameof(BindingRow.Id));
            var low = Expression.Property(Expression.Constant(a), nameof(RawRecipeOwner.Low));
            var high = Expression.Property(Expression.Constant(b), nameof(RawRecipeOwner.High));
            var match = Expression.Property(Expression.Constant(target), nameof(RawRecipeOwner.Target));
            var range = Expression.AndAlso(Expression.GreaterThan(id, low), Expression.LessThan(id, high));
            var predicate = Expression.Lambda<Func<BindingRow, bool>>(
                Expression.AndAlso(range, Expression.Equal(id, match)), row);
            return context.From<BindingRow>().Where(predicate).OrderBy(item => item.Id).Select(item => item.Id);
        }
    }

    private sealed record RawRecipeOwner(int Low, int High, int Target);

    [Fact]
    public void DerivedSqlPlan_RebindsWithoutRetainingTheFirstPredicateOwner()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (firstOwner, plan) = Execute(first, 1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstOwner.IsAlive.Should().BeFalse("the normalized key must not keep the first derived predicate owner");

        var (_, secondPlan) = Execute(second, 2);
        secondPlan.Should().BeSameAs(plan);
        firstTap.LastParameters.Should().Equal((0, "NestedTarget", 1));
        secondTap.LastParameters.Should().Equal((0, "NestedTarget", 2));

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int>? Plan) Execute(SqliteDataContext context, int target)
        {
            var owner = new NestedPredicateOwner(target);
            var inner = context.From<BindingRow>().Where(row => row.Id == owner.NestedTarget)
                .Select(row => new { row.Id });
            var outer = context.From(inner).OrderBy(row => row.Id).Select(row => row.Id);
            QueryCommand.CaptureRawShape(outer).Should().BeNull("a derived source bypasses raw staging");
            var invocation = context.GetPreparedQueryCommand(outer, false, true, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(target);
            return (new WeakReference(owner), BoundQueryInvocation<int>.GetPlan(invocation));
        }
    }

    private sealed record NestedPredicateOwner(int NestedTarget);

    [Fact]
    public void CorrelatedSqlPlan_DoesNotKeepTheLiveOuterRegistry()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        var (registry, storedPlan) = Execute(context);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        registry.IsAlive.Should().BeFalse("the normalized plan must not point back to the planning registry");
        storedPlan.QueryCommand.ReferencedQueries.Should().NotBeEmpty();

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Registry, QueryPlan Plan) Execute(SqliteDataContext context)
        {
            var innerSource = context.From<CorrelationRow>();
            var command = context.From<BindingRow>()
                .Where(row => SqlFunctions.Sql.exists(innerSource.Where(inner => inner.Id == row.Id)))
                .OrderBy(row => row.Id).Select(row => row.Id);
            // The streaming-rows entry point skips raw recipes but still caches this scalar plan.
            var invocation = context.GetStreamingRowsQueryCommand(command, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(1, 2);
            var plan = command.GetOrCreatePlanKey(null);
            plan.QueryCommand.Should().NotBeSameAs(command, "the assertion concerns a published key");
            return (new WeakReference(command), plan);
        }
    }

    [SqlTable(TableName)]
    public sealed class CorrelationRow
    {
        public int Id { get; set; }
    }

    [Fact]
    public void CapturedBuilderSubquery_BindsTheCurrentBuilderWithoutRetainingTheFirst()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstBuilder = Execute(first, 1, [1, 2]);
        Execute(second, 3, []);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 3));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstBuilder.IsAlive.Should().BeFalse("the cached getter must not hold the first builder");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, int target, int[] expected)
        {
            var subquery = Filter(context, target);
            var query = context.From<BindingRow>().Where(row => subquery.Any()).OrderBy(row => row.Id).Select(row => row.Id);
            query.Cache = false;
            query.ToList().Should().Equal(expected);
            return new WeakReference(subquery);
        }
    }

    [Fact]
    public void CapturedScalarSubquery_BindsTheCurrentCommandWithoutRetainingTheFirst()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var firstCommand = Execute(first, 1);
        Execute(second, 2);
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 2));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstCommand.IsAlive.Should().BeFalse("the cached accessor must not hold the first scalar subquery command");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(SqliteDataContext context, int target)
        {
            var subquery = Query(context, target);
            var query = context.From<BindingRow>().OrderBy(row => row.Id)
                .Select(row => new { row.Id, Match = subquery.First() });
            query.Cache = false;
            query.ToList().Select(row => (row.Id, row.Match)).Should().Equal([(1, target), (2, target)]);
            return new WeakReference(subquery);
        }
    }

    private class BaseTarget
    {
        public int Target => 1;
    }

    private sealed class DerivedTarget : BaseTarget
    {
        public new int Target => 2;
    }

    [Fact]
    public void NativeList_DifferentContextTimeouts_UseTheirOwnPlan()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().UseCommandTimeout(7).AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().UseCommandTimeout(19).AddInterceptor(secondTap));

        static List<int> ReadRows(SqliteDataContext context) => context.From<BindingRow>().Select(row => row.Id).ToList();

        ReadRows(first).Should().BeEquivalentTo([1, 2]);
        ReadRows(second).Should().BeEquivalentTo([1, 2]);
        firstTap.LastTimeout.Should().Be(7);
        secondTap.LastTimeout.Should().Be(19);

        var previousHits = QueryPlanStore.NativeHits;
        ReadRows(second).Should().BeEquivalentTo([1, 2]);
        secondTap.LastTimeout.Should().Be(19);
        QueryPlanStore.NativeHits.Should().BeGreaterThan(previousHits, "a compatible call still takes the native route");
    }

    [Fact]
    public void NativeList_ClearThenReadPrimaryStore_DoesNotReuseTheOldNativeEntry()
    {
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder());
        List(context, 1).Should().Equal(1);
        var previousHits = QueryPlanStore.NativeHits;
        List(context, 2).Should().Equal(2);
        QueryPlanStore.NativeHits.Should().BeGreaterThan(previousHits);

        context.PurgeQueryCache();
        QueryPlanStore.Values.Should().BeEmpty("reading the primary store advances the thread's generation");
        previousHits = QueryPlanStore.NativeHits;
        var previousPublishes = QueryPlanStore.NativePublishes;

        List(context, 2).Should().Equal(2);
        QueryPlanStore.NativeHits.Should().Be(previousHits, "the previous generation's native entry must not serve the query");
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(previousPublishes, "the first call after clear publishes a replacement");
        List(context, 1).Should().Equal(1);
        QueryPlanStore.NativeHits.Should().BeGreaterThan(previousHits, "the replacement is available to the next call");
    }

    private static IAsyncEnumerator<int> OrdinaryStream(SqliteDataContext context, int target, bool forceFallback, CancellationToken cancellationToken)
    {
        var builder = Filter(context, target);
        // A simple column sort can use the binding recipe; a computed sort still takes the fallback.
        if (forceFallback)
            builder = builder.OrderBy(row => row.Id + row.Id);

        return builder.Select(row => row.Id).CreateAsyncEnumerator(cancellationToken);
    }

    private static EntityBuilder<BindingRow> Filter(SqliteDataContext context, int target)
        => context.From<BindingRow>().Where(row => row.Id == target);

    private static QueryCommand<int> Query(SqliteDataContext context, int target)
        => context.From<BindingRow>().Where(row => row.Id == target).Select(row => row.Id);

    private static List<int> List(SqliteDataContext context, int target) => Query(context, target).ToList();

    private static Task<List<int>> ListAsync(SqliteDataContext context, int target, CancellationToken cancellationToken)
        => Query(context, target).ToListAsync(cancellationToken);

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

    private sealed class BindingTap : IQueryInterceptor
    {
        internal Action? OnInitialized { get; set; }
        internal (int Ordinal, string Name, int Value)[]? LastParameters { get; private set; }
        internal int LastTimeout { get; private set; }

        public void CommandInitialized(CommandEventData eventData, DbCommand command) => OnInitialized?.Invoke();

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            LastTimeout = command.CommandTimeout;
            LastParameters = command.Parameters.Cast<DbParameter>()
                .Select((parameter, ordinal) => (ordinal, parameter.ParameterName, (int)parameter.Value!))
                .ToArray();
        }
    }

    [SqlTable(TableName)]
    public sealed class BindingRow
    {
        public int Id { get; set; }
    }
}
