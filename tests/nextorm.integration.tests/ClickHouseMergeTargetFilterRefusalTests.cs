using System.Data.Common;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #123 live ClickHouse evidence: ClickHouse has no <c>MERGE</c>/native upsert that can carry the
/// target predicate, so a filtered MERGE/UPSERT must refuse with the typed
/// <see cref="NotSupportedException"/> before touching the database. ClickHouse does not derive the
/// shared <see cref="CommonTestSuite"/>, so this case is pinned here against the container-backed
/// provider. A <see cref="IQueryInterceptor"/> counts every command lifecycle event, which proves the
/// refusal is a metadata decision with zero command invocation reads/writes.
/// </summary>
public sealed class ClickHouseMergeTargetFilterRefusalTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private sealed class CommandCountingInterceptor : IQueryInterceptor
    {
        private int _initialized;
        private int _executing;
        private int _executed;

        public void CommandInitialized(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _initialized);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
            => Interlocked.Increment(ref _executed);

        public int Total => Volatile.Read(ref _initialized) + Volatile.Read(ref _executing) + Volatile.Read(ref _executed);
    }

    private static int NextId() => Random.Shared.Next(1_000_000, int.MaxValue);

    private static (ClickHouseDataContext Context, CommandCountingInterceptor Counter) CreateCountingContext()
    {
        var counter = new CommandCountingInterceptor();
        var ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
        ctx.AddInterceptor(counter);
        ctx.Properties[MergeTargetFilterFixtures.MinAgeKey] = 100;
        return (ctx, counter);
    }

    [Fact]
    public void FilteredKeyUpsert_QuerySource_ShouldRefuseWithZeroCommandInvocation()
    {
        var (ctx, counter) = CreateCountingContext();
        using var _ = ctx;
        var id = NextId();

        // The query source would require a real read; the capability refusal must happen before it.
        var act = () => ctx.CreateMergeBuilder<MergeTargetFilterEntity>()
            .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>(
            "ClickHouse's key upsert cannot isolate the write target under an active filter");
        counter.Total.Should().Be(0, "the refusal is a metadata decision before any connection, read or write");
    }

    [Fact]
    public void FilteredFullMergeForm_QuerySource_ShouldRefuseWithZeroCommandInvocation()
    {
        var (ctx, counter) = CreateCountingContext();
        using var _ = ctx;
        var id = NextId();

        var act = () => ctx.CreateMergeBuilder<MergeTargetFilterEntity>()
            .Using(ctx.From<MergeTargetFilterEntity>().IgnoreFilters().Where(x => x.Id == id))
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>(
            "ClickHouse advertises no general MERGE, so the filtered full-MERGE form refuses too");
        counter.Total.Should().Be(0, "the refusal precedes any source read, command creation or mutation");
    }
}
