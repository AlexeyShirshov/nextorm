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

        var command = ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.K1).ToCommand();
        command.Cache.Should().BeTrue("the command starts cacheable");

        var first = (DbPreparedQueryCommand<ExtremeNativeEntity>)Prepare(ctx, command);
        var second = (DbPreparedQueryCommand<ExtremeNativeEntity>)Prepare(ctx, command);

        first.DbCommand.CommandText.Should().Contain("argMax(tuple(");
        first.DbCommand.CommandText.Should().Contain("having count() > 0");
        ReferenceEquals(first, second).Should().BeTrue("the second preparation must reuse the cached plan");
        second.DbCommand.CommandText.Should().Be(first.DbCommand.CommandText);
        command.Cache.Should().BeTrue("the native extrema path must never mutate the sticky Cache flag");
    }

    [Fact]
    public void NativeExtrema_FreshEquivalentCommand_ShouldReuseTheCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        QueryCommand<ExtremeNativeEntity> Build()
            => ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.K1).ToCommand();

        var first = Prepare(ctx, Build());
        var second = Prepare(ctx, Build());

        ReferenceEquals(first, second).Should()
            .BeTrue("two equal native extrema shapes on the same context must share one cached plan");
    }

    [Fact]
    public void OrdinaryQuery_AfterNativeExtrema_ShouldStillHitThePlanCache()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        var extrema = ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.K1).ToCommand();
        Prepare(ctx, extrema);
        extrema.Cache.Should().BeTrue();

        var ordinary = ctx.From<ISimpleEntity>().Select(x => x.Id);
        ordinary.Cache.Should().BeTrue();

        var first = Prepare(ctx, ordinary);
        var second = Prepare(ctx, ordinary);

        ReferenceEquals(first, second).Should()
            .BeTrue("an ordinary query after a native extrema query must still hit the plan cache");
        ordinary.Cache.Should().BeTrue();
    }
}
