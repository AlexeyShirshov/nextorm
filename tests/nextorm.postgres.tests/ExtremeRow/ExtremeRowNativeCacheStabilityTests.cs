using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Cache-stability contract of PostgreSQL's native <c>SelectWhereMax</c>/<c>SelectWhereMin</c> strategy:
/// a repeated native extrema command must produce a stable SQL text and hit the plan cache, a later
/// ordinary query on the same context must stay cacheable, and neither path may ever set the sticky
/// <see cref="QueryCommand.Cache"/> flag (a sticky disable would silently drop the plan cache for the
/// whole context). Runs in the serialized "Query plan cache" collection so a concurrent purge cannot
/// evict the plan between the two preparations.
/// </summary>
[Collection("Query plan cache")]
public class ExtremeRowNativeCacheStabilityTests
{
    private static IPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ctx.GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: true, CancellationToken.None);

    [Fact]
    public void NativeExtrema_RepeatedSameCommand_ShouldHitThePlanCacheWithStableSql()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        var command = ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.Int).ToCommand();
        command.Cache.Should().BeTrue("the command starts cacheable");

        var first = (DbPreparedQueryCommand<ExtremeNativeEntity>)Prepare(ctx, command);
        var second = (DbPreparedQueryCommand<ExtremeNativeEntity>)Prepare(ctx, command);

        first.DbCommand.CommandText.Should().Contain("limit 1");
        first.DbCommand.CommandText.Should().NotContain("row_number()");
        ReferenceEquals(first, second).Should().BeTrue("the second preparation must reuse the cached plan");
        second.DbCommand.CommandText.Should().Be(first.DbCommand.CommandText);
        command.Cache.Should().BeTrue("the native extrema path must never mutate the sticky Cache flag");
    }

    [Fact]
    public void NativeExtrema_FreshEquivalentCommand_ShouldReuseTheCachedPlan()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        QueryCommand<ExtremeNativeEntity> Build()
            => ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.Int).ToCommand();

        var first = Prepare(ctx, Build());
        var second = Prepare(ctx, Build());

        ReferenceEquals(first, second).Should()
            .BeTrue("two equal native extrema shapes on the same context must share one cached plan");
    }

    [Fact]
    public void OrdinaryQuery_AfterNativeExtrema_ShouldStillHitThePlanCache()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        // Warm the native extrema path first: the risk is that it leaks a sticky cache disable onto the
        // shared command or the context, which would then force every later query to rebuild.
        var extrema = ctx.From<ExtremeNativeEntity>().SelectWhereMax(x => x.Int).ToCommand();
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
