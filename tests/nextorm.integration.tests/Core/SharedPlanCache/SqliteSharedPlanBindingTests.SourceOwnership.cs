using System.Data.Common;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
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
