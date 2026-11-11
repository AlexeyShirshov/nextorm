using System;
using System.Threading;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D384 U6: plan-cache equivalence for source-less join overloads. The cache key of a query built
/// through a source-less form must equal the key of the explicit <c>ctx.From(...)</c> form; preparing
/// both on one context must reuse a single plan entry (no duplicate, no re-plan) and must never
/// disable caching or mutate a shared command.
/// </summary>
[Collection("Query plan cache")]
public class SourceLessJoinPlanCacheTests
{
    [SqlTable("slpc_parent")]
    public sealed class SlpcParent
    {
        public int Id { get; set; }
    }

    [SqlTable("slpc_child")]
    public sealed class SlpcChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    private static IDataContext Create() => SqliteTestContext.Create();

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    // ---------- type-only CrossJoin ----------

    [Fact]
    public void CrossJoin_TypeOnly_SourceLess_CacheKeyEqualsExplicit()
    {
        using var ctx = Create();
        ctx.PurgeQueryCache();
        ctx.From<SlpcParent>();
        ctx.From<SlpcChild>();

        var explicitCmd = ctx.From<SlpcParent>().CrossJoin(ctx.From<SlpcChild>()).Select(p => p.Item1.Id);
        var sourcelessCmd = ctx.From<SlpcParent>().CrossJoin<SlpcChild>().Select(p => p.Item1.Id);

        explicitCmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        sourcelessCmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        explicitCmd.GetOrCreatePlanKey(null).Equals(sourcelessCmd.GetOrCreatePlanKey(null))
            .Should().BeTrue("source-less CrossJoin<T> must share the explicit ctx.From<T>() plan key");

        ctx.PurgeQueryCache();
        var first = Prepare(ctx, explicitCmd, true);
        var countAfterExplicit = QueryPlanStore.Values.Count();
        countAfterExplicit.Should().Be(1, "the explicit form stores exactly one plan entry");

        var second = Prepare(ctx, sourcelessCmd, true);
        QueryPlanStore.Values.Count().Should().Be(countAfterExplicit,
            "source-less CrossJoin<T> must not add a plan entry");
        ReferenceEquals(first, second).Should().BeTrue(
            "source-less CrossJoin<T> must hit the explicit plan instance");

        explicitCmd.Cache.Should().BeTrue();
        sourcelessCmd.Cache.Should().BeTrue();
    }

    // ---------- CrossApply type-only (key equality only: SQLite cannot render APPLY) ----------

    [Fact]
    public void CrossApply_TypeOnly_SourceLess_CacheKeyEqualsExplicit()
    {
        using var ctx = Create();
        ctx.PurgeQueryCache();
        ctx.From<SlpcParent>();
        ctx.From<SlpcChild>();

        var explicitCmd = ctx.From<SlpcParent>().CrossApply(ctx.From<SlpcChild>()).Select(p => p.Item1.Id);
        var sourcelessCmd = ctx.From<SlpcParent>().CrossApply<SlpcChild>().Select(p => p.Item1.Id);

        explicitCmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        sourcelessCmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        explicitCmd.GetOrCreatePlanKey(null).Equals(sourcelessCmd.GetOrCreatePlanKey(null))
            .Should().BeTrue("source-less CrossApply<T> must share the explicit ctx.From<T>() plan key");
    }

    // ---------- OuterApply type-only (key equality only: SQLite cannot render APPLY) ----------

    [Fact]
    public void OuterApply_TypeOnly_SourceLess_CacheKeyEqualsExplicit()
    {
        using var ctx = Create();
        ctx.PurgeQueryCache();
        ctx.From<SlpcParent>();
        ctx.From<SlpcChild>();

        var explicitCmd = ctx.From<SlpcParent>().OuterApply(ctx.From<SlpcChild>()).Select(p => p.Item1.Id);
        var sourcelessCmd = ctx.From<SlpcParent>().OuterApply<SlpcChild>().Select(p => p.Item1.Id);

        explicitCmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        sourcelessCmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        explicitCmd.GetOrCreatePlanKey(null).Equals(sourcelessCmd.GetOrCreatePlanKey(null))
            .Should().BeTrue("source-less OuterApply<T> must share the explicit ctx.From<T>() plan key");
    }

    // ---------- string-table Join ----------

    [Fact]
    public void Join_StringTable_SourceLess_CacheKeyEqualsExplicit()
    {
        using var ctx = Create();
        ctx.PurgeQueryCache();

        var explicitCmd = ctx.From<SlpcParent>()
            .Join(ctx.From("slpc_child"), (p, c) => p.Id == c["parent_id"].AsInt)
            .Select(p => p.Item1.Id);
        var sourcelessCmd = ctx.From<SlpcParent>()
            .Join("slpc_child", (p, c) => p.Id == c["parent_id"].AsInt)
            .Select(p => p.Item1.Id);

        explicitCmd.PrepareCommand(false, TestContext.Current.CancellationToken);
        sourcelessCmd.PrepareCommand(false, TestContext.Current.CancellationToken);

        explicitCmd.GetOrCreatePlanKey(null).Equals(sourcelessCmd.GetOrCreatePlanKey(null))
            .Should().BeTrue("source-less Join(\"table\") must share the explicit ctx.From(\"table\") plan key");

        ctx.PurgeQueryCache();
        var first = Prepare(ctx, explicitCmd, true);
        var countAfterExplicit = QueryPlanStore.Values.Count();
        countAfterExplicit.Should().Be(1, "the explicit form stores exactly one plan entry");

        var second = Prepare(ctx, sourcelessCmd, true);
        QueryPlanStore.Values.Count().Should().Be(countAfterExplicit,
            "source-less Join(\"table\") must not add a plan entry");
        ReferenceEquals(first, second).Should().BeTrue(
            "source-less Join(\"table\") must hit the explicit plan instance");

        explicitCmd.Cache.Should().BeTrue();
        sourcelessCmd.Cache.Should().BeTrue();
    }

    // ---------- negative: no sticky disable ----------

    [Fact]
    public void SourceLess_PrepareWithStoreInCacheFalse_DoesNotDisableCaching()
    {
        using var ctx = Create();
        ctx.PurgeQueryCache();
        ctx.From<SlpcParent>();
        ctx.From<SlpcChild>();

        var cmd = ctx.From<SlpcParent>().CrossJoin<SlpcChild>().Select(p => p.Item1.Id);
        cmd.Cache.Should().BeTrue();

        _ = Prepare(ctx, cmd, false);
        cmd.Cache.Should().BeTrue("storeInCache:false must not set the sticky _dontCache flag");

        // caching still works afterwards
        ctx.PurgeQueryCache();
        var first = Prepare(ctx, cmd, true);
        var second = Prepare(ctx, cmd, true);
        ReferenceEquals(first, second).Should().BeTrue(
            "caching stays enabled for later source-less queries on the same context");
    }
}
