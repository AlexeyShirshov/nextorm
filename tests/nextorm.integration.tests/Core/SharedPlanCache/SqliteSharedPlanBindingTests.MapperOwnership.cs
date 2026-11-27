using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderMapper_BindsTheExecutingContextWithoutRetainingTheFirst(bool cachePlan)
    {
        var token = TestContext.Current.CancellationToken;
        var (firstRows, firstContext, firstPlan) = ReadRows(40, cachePlan, token);
        var (secondRows, _, secondPlan) = ReadRows(98, cachePlan, token);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var assertions = new AssertionScope();
        firstRows.Should().Equal(41, 42);
        secondRows.Should().Equal([99, 100], "the mapper must use the executing provider's own accessor");
        firstContext.IsAlive.Should().BeFalse("the compiled mapper must not own a disposed provider context");
        if (cachePlan)
        {
            firstPlan.Should().NotBeNull();
            secondPlan.Should().BeSameAs(firstPlan, "the SQL plan is compatible even though its row mapper is context-bound");
            QueryPlanStore.Values.Should().Contain(firstPlan!);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (List<int> Rows, WeakReference Context, DbPreparedQueryCommand<int>? Plan) ReadRows(
            int offset, bool cachePlan, CancellationToken token)
        {
            using var connection = OpenSeededConnection();
            using var context = new InstanceMapperContext(connection, offset);
            var query = context.From<BindingRow>().WithTag(cachePlan ? "implicit provider mapper" : "uncached provider mapper")
                .OrderBy(row => row.Id).Select(row => row.Id);
            var invocation = context.GetPreparedQueryCommand(query, false, cachePlan, token);
            var rows = invocation.ToList(context);
            var mapperBuilds = context.MapperBuilds;
            var repeated = context.GetPreparedQueryCommand(query, false, cachePlan, token);
            repeated.ToList(context).Should().Equal(rows);
            context.MapperBuilds.Should().Be(mapperBuilds, "one live provider should reuse its compiled mapper");
            return (rows, new WeakReference(context), cachePlan ? BoundQueryInvocation<int>.GetPlan(invocation) : null);
        }
    }

    [Fact]
    public void ProviderMapper_NativeListDoesNotReuseAnEarlierContextsAccessor()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new InstanceMapperContext(firstConnection, 40);
        using var second = new InstanceMapperContext(secondConnection, 98);
        var publishes = QueryPlanStore.NativePublishes;

        Query(first, 1).ToList().Should().Equal(41);
        Query(second, 2).ToList().Should().Equal(100);
        QueryPlanStore.NativePublishes.Should().Be(publishes, "a native hit cannot supply the executing context's mapper");
    }

    [Fact]
    public void RawProviderMapper_BindsTheExecutingContextWithoutRetainingTheFirst()
    {
        var (firstIds, firstContext) = ReadRows(40);
        var (secondIds, _) = ReadRows(98);
        firstIds.Should().Equal(41, 42);
        secondIds.Should().Equal(99, 100);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        firstContext.IsAlive.Should().BeFalse("the raw mapper must not keep its context alive");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (int[] Ids, WeakReference Context) ReadRows(int offset)
        {
            using var connection = OpenSeededConnection();
            using var context = new InstanceMapperContext(connection, offset);
            using var result = context.ExecuteRaw("select Id from issue16_binding_rows order by Id");
            return (result.Read<BindingRow>().Select(row => row.Id).ToArray(), new WeakReference(context));
        }
    }

    private sealed class InstanceMapperContext(SqliteConnection connection, int offset)
        : SqliteDataContext(connection, new DataContextBuilder())
    {
        public int Offset { get; } = offset;
        internal int MapperBuilds { get; private set; }

        public override Expression MapColumnExpression(SelectExpression column, Expression record)
        {
            MapperBuilds++;
            var read = base.MapColumnExpression(column, record);
            return read.Type == typeof(int)
                ? Expression.Add(read, Expression.Property(Expression.Constant(this), nameof(Offset)))
                : read;
        }
    }

    [Fact]
    public void SqlPlanKey_DoesNotOwnTheColumnConverter()
    {
        var (key, converter) = BuildKey();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        converter.IsAlive.Should().BeFalse("the comparison key only needs the converter's identity");
        GC.KeepAlive(key);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (QueryPlan Key, WeakReference Converter) BuildKey()
        {
            using var connection = OpenSeededConnection();
            using var context = new SqliteDataContext(connection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var converter = new ProjectionConverter(40);
            var command = BuildConvertedQuery(context, converter);
            var equivalent = BuildConvertedQuery(context, converter);
            var different = BuildConvertedQuery(context, new ProjectionConverter(98));
            var key = command.GetOrCreatePlanKey(null).GetSqlCacheVersion();
            var same = equivalent.GetOrCreatePlanKey(null);
            var other = different.GetOrCreatePlanKey(null);
            key.GetHashCode().Should().Be(other.GetHashCode(), "the converter checks must work inside one hash bucket");
            key.Equals(same).Should().BeTrue();
            same.Equals(key).Should().BeTrue();
            key.Equals(other).Should().BeFalse();
            other.Equals(key).Should().BeFalse();
            context.GetPreparedQueryCommand(command, false, false, token).ToList(context).Should().Equal(41);
            return (key, new WeakReference(converter));
        }
    }

    [Fact]
    public void CachedProjection_DoesNotKeepItsConverterOnThePlanHolder()
    {
        var (holder, converter) = Execute();
        QueryPlanStore.Clear();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        converter.IsAlive.Should().BeFalse("only the live invocation needs its converted row mapper");
        GC.KeepAlive(holder);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Holder, WeakReference Converter) Execute()
        {
            using var firstConnection = OpenSeededConnection();
            using var secondConnection = OpenSeededConnection();
            using var first = new SqliteDataContext(firstConnection, new DataContextBuilder());
            using var second = new SqliteDataContext(secondConnection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var converter = new ProjectionConverter(40);
            var cold = first.GetPreparedQueryCommand(BuildConvertedQuery(first, converter), false, true, token);
            var warm = second.GetPreparedQueryCommand(BuildConvertedQuery(second, converter), false, true, token);
            var plan = BoundQueryInvocation<int>.GetPlan(cold)!;
            BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(plan);
            cold.ToList(first).Should().Equal(41);
            warm.ToList(second).Should().Equal(41);
            QueryPlanStore.Values.Should().Contain(plan);
            return (plan, new WeakReference(converter));
        }
    }

    [Fact]
    public async Task CachedProjection_StreamsConvertedRowsAndBindsFreshValues()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var token = TestContext.Current.CancellationToken;
        var converter = new ProjectionConverter(40);
        var cold = first.GetPreparedQueryCommand(Build(first, 1), false, true, token);
        var warm = second.GetStreamingRowsQueryCommand(Build(second, 2), token);

        BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(BoundQueryInvocation<int>.GetPlan(cold));
        cold.First(first).Should().Be(41);
        (await warm.SingleAsync(second, token)).Should().Be(42);
        await using var stream = warm.CreateAsyncEnumerator(second, token);
        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Should().Be(42);
        (await stream.MoveNextAsync()).Should().BeFalse();
        firstTap.LastParameters.Should().Equal((0, "target", 1));
        secondTap.LastParameters.Should().Equal((0, "target", 2));

        QueryCommand<int> Build(SqliteDataContext context, int target)
        {
            var query = context.From<BindingRow>().Where(row => row.Id == target).Select(row => row.Id);
            SetProjectionConverter(query, converter);
            return query;
        }
    }

    private static QueryCommand<int> BuildConvertedQuery(SqliteDataContext context, ProjectionConverter converter)
    {
        var query = context.From<BindingRow>().Where(row => row.Id == 1).Select(row => row.Id);
        SetProjectionConverter(query, converter);
        return query;
    }

    private static void SetProjectionConverter(QueryCommand<int> query, ProjectionConverter converter)
    {
        query.PrepareCommand(TestContext.Current.CancellationToken);
        // Supply a command-local converter without registering it in global entity metadata.
        query.SelectList![0].ProviderType = typeof(int);
        query.SelectList[0].Converter = converter;
    }

    [Fact]
    public void UncachedProjection_DoesNotKeepItsConverterInTheMapperCache()
    {
        var converter = Execute(40);
        Execute(98);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        converter.IsAlive.Should().BeFalse("the shared mapper cache must not own the projection's converter");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(int offset)
        {
            using var connection = OpenSeededConnection();
            using var context = new SqliteDataContext(connection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var converter = new ProjectionConverter(offset);
            var query = BuildConvertedQuery(context, converter);
            var first = context.GetPreparedQueryCommand(query, false, false, token);
            var second = context.GetPreparedQueryCommand(query, false, false, token);
            first.ToList(context).Should().Equal(offset + 1);
            second.ToList(context).Should().Equal(offset + 1);
            BoundQueryInvocation<int>.GetPlan(second)!.MapDelegate.Should()
                .BeSameAs(BoundQueryInvocation<int>.GetPlan(first)!.MapDelegate);
            return new WeakReference(converter);
        }
    }

    [Fact]
    public void UncachedProjection_KeepingOneConverterAliveDoesNotKeepAnotherAlive()
    {
        var liveConverter = new ProjectionConverter(40);
        var releasedConverter = Execute(liveConverter);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        releasedConverter.IsAlive.Should().BeFalse("a mapper depends on the lifetime of both of its converters");
        GC.KeepAlive(liveConverter);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(ProjectionConverter first)
        {
            using var connection = OpenSeededConnection();
            using var context = new SqliteDataContext(connection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var query = context.From<BindingRow>().Where(row => row.Id == 1)
                .Select(row => new { First = row.Id, Second = row.Id });
            query.PrepareCommand(token);
            var second = new ProjectionConverter(98);
            var columns = query.SelectList!;
            columns[0].ProviderType = typeof(int);
            columns[0].Converter = first;
            columns[1].ProviderType = typeof(int);
            columns[1].Converter = second;

            var row = context.GetPreparedQueryCommand(query, false, false, token).ToList(context).Should().ContainSingle().Which;
            (row.First, row.Second).Should().Be((41, 99));
            return new WeakReference(second);
        }
    }

    private sealed class ProjectionConverter(int offset) : ValueConverter<int, int>
    {
        public override int ConvertToProvider(int model) => model - offset;

        public override int ConvertFromProvider(int provider) => provider + offset;
    }
}
