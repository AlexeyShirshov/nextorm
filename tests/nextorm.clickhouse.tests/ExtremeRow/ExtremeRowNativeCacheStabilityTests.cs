using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Serializes the ClickHouse tests that assert plan-cache HITS with any test that purges the
/// process-wide plan cache, so a purge on another xunit thread cannot evict a just-stored plan.
/// </summary>
[CollectionDefinition("Query plan cache", DisableParallelization = true)]
public sealed class QueryPlanCacheCollection;

/// <summary>
/// Cache-stability contract of ClickHouse's native <c>SelectWhereMax</c>/<c>SelectWhereMin</c> strategy:
/// a repeated native extrema command must produce stable SQL and hit the plan cache, a later ordinary
/// query on the same context must stay cacheable, and neither path may set the sticky
/// <see cref="QueryCommand.Cache"/> flag.
/// </summary>
[Collection("Query plan cache")]
public class ExtremeRowNativeCacheStabilityTests
{
    private static IPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ctx.GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: true, CancellationToken.None);

    [Fact]
    public void NativeExtrema_RepeatedSameCommand_ShouldHitThePlanCacheWithStableSql()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        var minimum = 0;
        var command = ctx.From<ExtremeNativeEntity>().Where(row => row.K1 > minimum)
            .SelectWhereMaxOld(x => x.K1).ToCommand();
        command.Cache.Should().BeTrue("the command starts cacheable");

        var first = (BoundQueryInvocation<ExtremeNativeEntity>)Prepare(ctx, command);
        minimum = 5;
        var second = (BoundQueryInvocation<ExtremeNativeEntity>)Prepare(ctx, command);

        first.Plan.DbCommand.CommandText.Should().Contain("argMax(tuple(");
        first.Plan.DbCommand.CommandText.Should().Contain("having count() > 0");
        second.Plan.Should().BeSameAs(first.Plan);
        first.Values.Should().Equal([0]);
        second.Values.Should().Equal([5]);
        command.Cache.Should().BeTrue("the native extrema path must never mutate the sticky Cache flag");
    }

    [Fact]
    public void NativeExtrema_FreshEquivalentCommand_ShouldReuseTheCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        QueryCommand<ExtremeNativeEntity> Build(int minimum)
            => ctx.From<ExtremeNativeEntity>().Where(row => row.K1 > minimum)
                .SelectWhereMaxOld(x => x.K1).ToCommand();

        var first = (BoundQueryInvocation<ExtremeNativeEntity>)Prepare(ctx, Build(1));
        var second = (BoundQueryInvocation<ExtremeNativeEntity>)Prepare(ctx, Build(9));

        second.Plan.Should().BeSameAs(first.Plan, "two equal native extrema shapes must share one cached plan");
        first.Values.Should().Equal([1]);
        second.Values.Should().Equal([9]);
    }

    [Fact]
    public void OrdinaryQuery_AfterNativeExtrema_ShouldStillHitThePlanCache()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        var extrema = ctx.From<ExtremeNativeEntity>().SelectWhereMaxOld(x => x.K1).ToCommand();
        Prepare(ctx, extrema);
        extrema.Cache.Should().BeTrue();

        var minimum = 2;
        var ordinary = ctx.From<ISimpleEntity>().Where(row => row.Id > minimum).Select(x => x.Id);
        ordinary.Cache.Should().BeTrue();

        var first = (BoundQueryInvocation<int>)Prepare(ctx, ordinary);
        minimum = 8;
        var second = (BoundQueryInvocation<int>)Prepare(ctx, ordinary);

        second.Plan.Should().BeSameAs(first.Plan, "an ordinary query after a native extrema must still hit the plan cache");
        first.Values.Should().Equal([2]);
        second.Values.Should().Equal([8]);
        ordinary.Cache.Should().BeTrue();
    }
}
