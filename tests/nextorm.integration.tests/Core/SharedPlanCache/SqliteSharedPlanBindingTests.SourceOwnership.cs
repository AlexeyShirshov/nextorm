using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedCtePlan_RebindsWithoutKeepingTheDefiningCommands(bool recursive)
    {
        var (holder, key, context, shape, anchor) = Execute(recursive);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var assertions = new AssertionScope();
        shape.IsAlive.Should().BeFalse("the named source's column shape belongs to the live query");
        if (anchor is not null)
        {
            anchor.IsAlive.Should().BeFalse("the recursive reference's anchor belongs to the live declaration");
            key.QueryCommand.Ctes![0].RecursiveReference.Should().BeNull("the comparison key does not render or validate the recursive step");
        }
        context.IsAlive.Should().BeFalse("published plans must not own the context that built the CTE");
        QueryPlanStore.Values.Should().Contain(holder);
        GC.KeepAlive(key);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (DbPreparedQueryCommand<int> Holder, QueryPlan Key, WeakReference Context, WeakReference Shape, WeakReference? Anchor)
            Execute(bool recursive)
        {
            using var firstConnection = OpenSeededConnection();
            using var secondConnection = OpenSeededConnection();
            var firstTap = new BindingTap();
            var secondTap = new BindingTap();
            using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
            using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
            var token = TestContext.Current.CancellationToken;

            var firstQuery = Build(first, 1, recursive);
            var secondQuery = Build(second, 2, recursive);
            QueryCommand.CaptureRawShape(firstQuery).Should().BeNull("typed sources use the normalized plan route");
            var columnShape = firstQuery.From!.ColumnShape;
            columnShape.Should().NotBeNull();
            var anchorShape = firstQuery.Ctes![0].RecursiveReference?.AnchorShape;
            (anchorShape is not null).Should().Be(recursive);
            var cold = first.GetPreparedQueryCommand(firstQuery, false, true, token);
            var warm = second.GetPreparedQueryCommand(secondQuery, false, true, token);
            var holder = BoundQueryInvocation<int>.GetPlan(cold)!;
            BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(holder);

            int[] expectedCold = recursive ? [1, 2, 3] : [1];
            int[] expectedWarm = recursive ? [2, 3] : [2];
            cold.ToList(first).Should().Equal(expectedCold);
            warm.ToList(second).Should().Equal(expectedWarm);
            firstTap.LastParameters.Should().Equal((0, "target", 1));
            secondTap.LastParameters.Should().Equal((0, "target", 2));

            return (holder, firstQuery.GetOrCreatePlanKey(null), new WeakReference(first),
                new WeakReference(columnShape), anchorShape is null ? null : new WeakReference(anchorShape));
        }

        static QueryCommand<int> Build(SqliteDataContext context, int target, bool recursive)
        {
            var anchor = context.From<BindingRow>().Where(row => row.Id == target)
                .Select(row => new CteBindingRow { Number = row.Id });
            var cte = recursive
                ? anchor.AsRecursiveCte("issue16_typed_cte", self => context.From(self)
                    .Where(row => row.Number < 3).Select(row => new CteBindingRow { Number = row.Number + 1 }))
                : anchor.AsCte("issue16_typed_cte");
            return context.From(cte).OrderBy(row => row.Number).Select(row => row.Number);
        }
    }

    [Fact]
    public void RawSourcePlan_RebindsTheSameSourceWithoutKeepingItsParameterBag()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (owner, holder) = Execute(first, firstTap, 1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        owner.IsAlive.Should().BeFalse("the normalized source key must not keep its parameter bag");
        QueryPlanStore.Values.Should().Contain(holder);

        var (_, differentSource) = Execute(second, secondTap, 3);
        differentSource.Should().NotBeSameAs(holder, "independent FromSql source instances keep their existing identity boundary");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Owner, DbPreparedQueryCommand<int> Holder) Execute(
            SqliteDataContext context, BindingTap tap, int value)
        {
            var bag = new RawSourceBag { Value = value };
            var source = context.FromSql("select @Value as Id", bag);
            var command = source.Select(row => SqlFunctions.Column<int>(row, "Id"));
            QueryCommand.CaptureRawShape(command).Should().BeNull("a caller-defined SQL source bypasses raw staging");
            var token = TestContext.Current.CancellationToken;
            var first = context.GetPreparedQueryCommand(command, false, true, token);
            first.ToList(context).Should().Equal(value);
            tap.LastParameters.Should().Equal((0, "Value", value));

            bag.Value = value + 1;
            var repeated = context.GetPreparedQueryCommand(source.Select(row => SqlFunctions.Column<int>(row, "Id")), false, true, token);
            repeated.ToList(context).Should().Equal(value + 1);
            tap.LastParameters.Should().Equal((0, "Value", value + 1));
            var holder = BoundQueryInvocation<int>.GetPlan(first)!;
            holder.Should().NotBeNull();
            BoundQueryInvocation<int>.GetPlan(repeated).Should().BeSameAs(holder);
            return (new WeakReference(bag), holder);
        }
    }

    [Fact]
    public void TvfPlan_RebindsTheCurrentArgumentWithoutKeepingTheFactoryOrArgumentOwner()
    {
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        var firstTap = new JsonSourceTap();
        var secondTap = new JsonSourceTap();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        var (factory, argument, holder) = Execute(first, "[11]", 11);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        factory.IsAlive.Should().BeFalse("factory metadata is snapshotted at lowering and its owner is not part of SQL identity");
        argument.IsAlive.Should().BeFalse("the TVF argument is bound per invocation, not owned by the plan key");

        var (_, _, repeated) = Execute(second, "[22]", 22);
        holder.Should().NotBeNull();
        repeated.Should().BeSameAs(holder);
        firstTap.LastParameter.Should().Be((0, "Document", "[11]"));
        secondTap.LastParameter.Should().Be((0, "Document", "[22]"));

        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference Factory, WeakReference Argument, DbPreparedQueryCommand<long>? Holder) Execute(
            SqliteDataContext context, string document, long expected)
        {
            var factory = new JsonEachFactory("json_each");
            var argument = new JsonSourceArgument(document);
            var command = context.From(() => factory.Create(argument.Document)).Select(row => row.Value);
            QueryCommand.CaptureRawShape(command).Should().BeNull("table functions use the normalized plan route");
            var invocation = context.GetPreparedQueryCommand(command, false, true, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(expected);
            return (new WeakReference(factory), new WeakReference(argument), BoundQueryInvocation<long>.GetPlan(invocation));
        }
    }

    private sealed class RawSourceBag
    {
        public int Value { get; set; }
    }

    private sealed class CteBindingRow
    {
        public int Number { get; init; }
    }

    private sealed record JsonSourceArgument(string Document);

    private sealed class JsonEachFactory(string name)
    {
        public TvfEntityBuilder<IT320SqJsonEachRow> Create(string document) => new(name);
    }

    private sealed class JsonSourceTap : IQueryInterceptor
    {
        internal (int Ordinal, string Name, string? Value)? LastParameter { get; private set; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            command.Parameters.Count.Should().Be(1);
            LastParameter = (0, command.Parameters[0].ParameterName, (string?)command.Parameters[0].Value);
        }
    }
}
