using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_sqlserver_tests;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T316 (#316): the plan cache must key an APPLY join by its source structure. The #316 pair is an
/// uncorrelated generated alias APPLY (cross product) and a correlated alias APPLY over the same
/// projection type; preparing them on one context must never let the second adopt the first SQL.
/// These tests use the placeholder connection string and open no connection.
/// </summary>
public class T316ApplyPlanCacheTests
{
    [SqlTable("t316_marker")]
    public sealed class T316Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }

    [SqlTable("t316_line")]
    public sealed class T316Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, true, CancellationToken.None))
            .DbCommand.CommandText
            .Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    /// <summary>The uncorrelated generated alias APPLY arm: a plain right-hand table.</summary>
    private static QueryCommand<int> Uncorrelated(IDataContext ctx)
        => ctx.From<T316Marker>()
            .CrossApply(ctx.From<T316Line>(), Alias.Buyer)
            .Select(p => p.Buyer.Id);

    /// <summary>The correlated alias APPLY arm from #316: the applied query filters on the left row.</summary>
    private static QueryCommand<int> Correlated(IDataContext ctx)
        => ctx.From<T316Marker>()
            .JoinAlias<AliasJoin_P1_A2_Buyer<T316Marker, T316Line>, AliasProjection_P1_A2_Buyer<T316Marker, T316Line>, T316Line>(
                static dc => new AliasJoin_P1_A2_Buyer<T316Marker, T316Line>(dc),
                m => ctx.From<T316Line>().Where(l => l.MarkerId == m.Id),
                JoinType.CrossApply)
            .Select(p => p.Buyer.Id);

    [Fact]
    public void T316_SqlText_UncorrelatedThenCorrelated()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        var uncorrelatedSql = SqlOf(ctx, Uncorrelated(ctx));
        var correlatedSql = SqlOf(ctx, Correlated(ctx));

        correlatedSql.Should().NotBe(uncorrelatedSql,
            "the correlated APPLY source is a different join shape and must not reuse the uncorrelated command's SQL");
    }

    [Fact]
    public void T316_SqlText_CorrelatedThenUncorrelated()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        var correlatedSql = SqlOf(ctx, Correlated(ctx));
        var uncorrelatedSql = SqlOf(ctx, Uncorrelated(ctx));

        uncorrelatedSql.Should().NotBe(correlatedSql,
            "the uncorrelated APPLY over a plain table must not inherit the correlated command's SQL");
    }

    [Fact]
    public void T316_EquivalentShapeReusesHolder()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        // Both lambdas are built in this one method scope so they share the same closure type for the
        // captured context; only the lambda parameter name differs.
        var firstCommand = ctx.From<T316Marker>()
            .JoinAlias<AliasJoin_P1_A2_Buyer<T316Marker, T316Line>, AliasProjection_P1_A2_Buyer<T316Marker, T316Line>, T316Line>(
                static dc => new AliasJoin_P1_A2_Buyer<T316Marker, T316Line>(dc),
                m => ctx.From<T316Line>().Where(l => l.MarkerId == m.Id),
                JoinType.CrossApply)
            .Select(p => p.Buyer.Id);
        var secondCommand = ctx.From<T316Marker>()
            .JoinAlias<AliasJoin_P1_A2_Buyer<T316Marker, T316Line>, AliasProjection_P1_A2_Buyer<T316Marker, T316Line>, T316Line>(
                static dc => new AliasJoin_P1_A2_Buyer<T316Marker, T316Line>(dc),
                marker => ctx.From<T316Line>().Where(l => l.MarkerId == marker.Id),
                JoinType.CrossApply)
            .Select(p => p.Buyer.Id);

        var first = Prepare(ctx, firstCommand, storeInCache: true);
        var second = Prepare(ctx, secondCommand, storeInCache: true);

        second.Should().BeSameAs(first,
            "a structurally equivalent APPLY shape, including a different parameter name, must reuse the cached holder");
    }

    [Fact]
    public void T316_UncachedPreparationNeitherReadsNorWrites()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        var coldCommand = Correlated(ctx);
        var cold = Prepare(ctx, coldCommand, storeInCache: false);
        coldCommand.Cache.Should().BeTrue("storeInCache:false is call-local and never mutates the sticky command flag");

        var cachedCommand = Correlated(ctx);
        var cached = Prepare(ctx, cachedCommand, storeInCache: true);
        cached.Should().NotBeSameAs(cold, "a cold uncached preparation must not seed a later cache hit");
        cachedCommand.Cache.Should().BeTrue();

        var warmUncachedCommand = Correlated(ctx);
        var warmUncached = Prepare(ctx, warmUncachedCommand, storeInCache: false);
        warmUncached.Should().NotBeSameAs(cached, "a warm uncached preparation must not adopt the cached holder");
        warmUncachedCommand.Cache.Should().BeTrue();

        var cachedAgainCommand = Correlated(ctx);
        var cachedAgain = Prepare(ctx, cachedAgainCommand, storeInCache: true);
        cachedAgain.Should().BeSameAs(cached, "the cached holder is reused for an equivalent shape");
        cachedAgainCommand.Cache.Should().BeTrue();
    }

    [Fact]
    public void T316_ContextIsolation()
    {
        using var firstContext = SqlServerTestContext.Create();
        using var secondContext = SqlServerTestContext.Create();
        firstContext.PurgeQueryCache();

        var firstSql = SqlOf(firstContext, Uncorrelated(firstContext));
        var secondSql = SqlOf(secondContext, Correlated(secondContext));

        // The plan store is scoped by context type and thread, so two contexts of one provider can
        // share a holder for the SAME shape; what must never transfer is the first query's SQL.
        secondSql.Should().NotBe(firstSql,
            "the second context must not inherit the first context's uncorrelated APPLY command");
    }

    [Fact]
    public void T316_CachedCommandPatchPreservesIdentity()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        var min = 0;
        var firstCommand = ctx.From<T316Marker>().Where(m => m.Grp > min).Select(m => m.Id);
        var first = Prepare(ctx, firstCommand, storeInCache: true);
        firstCommand.Cache.Should().BeTrue();
        first.DbCommand.Parameters.Count.Should().Be(1);
        first.DbCommand.Parameters[0].Value.Should().Be(0);

        min = 2;
        var secondCommand = ctx.From<T316Marker>().Where(m => m.Grp > min).Select(m => m.Id);
        var second = Prepare(ctx, secondCommand, storeInCache: true);

        second.Should().BeSameAs(first, "the captured value is not part of the plan key");
        second.DbCommand.Parameters.Count.Should().Be(1);
        second.DbCommand.Parameters[0].Value.Should().Be(2, "the cached holder re-reads the current closure value");
        secondCommand.Cache.Should().BeTrue("patch/reuse never mutates the sticky command cache flag");
    }

    [Fact]
    public void T316_ApplyFormVariants()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.PurgeQueryCache();

        // Alias A -> B.
        var aliasUncorrelated = SqlOf(ctx, Uncorrelated(ctx));
        var aliasCorrelated = SqlOf(ctx, Correlated(ctx));
        aliasCorrelated.Should().NotBe(aliasUncorrelated);

        // Alias B -> A on a cold cache.
        ctx.PurgeQueryCache();
        var reversedCorrelated = SqlOf(ctx, Correlated(ctx));
        var reversedUncorrelated = SqlOf(ctx, Uncorrelated(ctx));
        reversedUncorrelated.Should().NotBe(reversedCorrelated);

        // Positional APPLY: distinct sources must render distinct text under the same JoinType.
        var crossById = SqlOf(ctx, ctx.From<T316Marker>()
            .CrossApply(m => ctx.From<T316Line>().Where(l => l.MarkerId == m.Id))
            .Select(p => p.Item2.Id));
        var crossByAmount = SqlOf(ctx, ctx.From<T316Marker>()
            .CrossApply(m => ctx.From<T316Line>().Where(l => l.Amount > m.Grp))
            .Select(p => p.Item2.Id));
        crossByAmount.Should().NotBe(crossById,
            "two structurally distinct APPLY sources cannot share a prepared command");

        // Cross/outer are distinct join kinds even with the same source.
        var outerById = SqlOf(ctx, ctx.From<T316Marker>()
            .OuterApply(m => ctx.From<T316Line>().Where(l => l.MarkerId == m.Id))
            .Select(p => p.Item2.Id));
        outerById.Should().NotBe(crossById);
    }
}
