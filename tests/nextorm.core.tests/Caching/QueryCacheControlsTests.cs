using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Serializes the tests that mutate the process-wide <see cref="DataContextCache"/> state (clearing it
/// or changing its sliding expiration) with the rest of the suite.
/// </summary>
[CollectionDefinition("Query cache controls", DisableParallelization = true)]
public sealed class QueryCacheControlsCollection;

/// <summary>
/// Tests for the first-class cache controls: process-wide <see cref="DataContextCache.Clear"/>,
/// context-level disabling through <see cref="DataContextBuilder.UseQueryCache"/> /
/// <see cref="InMemoryDataContext.QueryCacheEnabled"/>, and sliding expiration. The disabling path must
/// never mutate the sticky <see cref="QueryCommand.Cache"/> flag (see AGENTS.md).
/// </summary>
[Collection("Query cache controls")]
public class QueryCacheControlsTests
{
    private sealed class ClearMarker;
    private sealed class SlidingExpirationMarker;
    private sealed class RefreshMarker;

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.Data[typeof(SimpleEntity)] = new SimpleEntity[] { new() { Id = 1 } };
        return ctx;
    }

    [Fact]
    public void Clear_Should_Empty_ProcessWide_Caches()
    {
        using var ctx = CreateContext();
        ctx.From<SimpleEntity>();

        DataContextCache.SelectListCache[typeof(ClearMarker)] = [];
        DataContextCache.Metadata.ContainsKey(typeof(SimpleEntity)).Should().BeTrue();

        DataContextCache.Clear();

        DataContextCache.SelectListCache.ContainsKey(typeof(ClearMarker)).Should().BeFalse();
        DataContextCache.Metadata.ContainsKey(typeof(SimpleEntity)).Should().BeFalse();
    }

    [Fact]
    public void Enabled_QueryCache_Should_Reuse_Prepared_Command()
    {
        using var ctx = CreateContext();
        var command = ctx.From<SimpleEntity>().Select(x => x.Id);

        var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Disabled_QueryCache_Should_Rebuild_Without_Mutating_Command_Cache_Flag()
    {
        using var ctx = CreateContext();
        ctx.QueryCacheEnabled = false;
        var command = ctx.From<SimpleEntity>().Select(x => x.Id);
        command.Cache.Should().BeTrue();

        var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
        var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

        second.Should().NotBeSameAs(first);
        command.Cache.Should().BeTrue();
    }

    [Fact]
    public void StoreInCache_False_Should_Not_Mutate_Command_Cache_Flag()
    {
        using var ctx = CreateContext();
        var command = ctx.From<SimpleEntity>().Select(x => x.Id);

        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        command.Cache.Should().BeTrue();
    }

    // A captured scalar Contains must not mutate the persistent command policy when the call is prepared
    // without caching (the InMemory fixture cannot establish the SQL regression; the SQLite D199 suite
    // does that).
    [Fact]
    public void CapturedScalarContains_PrepareWithoutCaching_ShouldPreserveCommandPolicy()
    {
        using var ctx = CreateContext();
        var values = new List<int> { 1 };
        var command = ctx.From<SimpleEntity>().Where(x => values.Contains(x.Id)).Select(x => x.Id);

        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

        command.Cache.Should().BeTrue(
            "preparing a captured scalar Contains without caching must not mutate the persistent command policy");
    }

    // An explicitly disabled caller policy is authoritative and must not be re-enabled by preparing a
    // captured scalar Contains.
    [Fact]
    public void CapturedScalarContains_ShouldRespectExplicitDisabledPolicy()
    {
        using var ctx = CreateContext();
        var values = new List<int> { 1 };
        var command = ctx.From<SimpleEntity>().Where(x => values.Contains(x.Id)).Select(x => x.Id);
        command.Cache = false;

        ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

        command.Cache.Should().BeFalse(
            "an explicitly disabled cache policy must not be re-enabled by preparing a captured scalar Contains");
    }

    [Fact]
    public void UseQueryCache_False_Should_Disable_Plan_Cache()
    {
        new DataContextBuilder().UseQueryCache(false).QueryCacheEnabled.Should().BeFalse();
        new DataContextBuilder().UseQueryCache().QueryCacheEnabled.Should().BeTrue();
    }

    [Fact]
    public void UseCacheSlidingExpiration_Should_Set_Global_Window()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        try
        {
            var ttl = TimeSpan.FromSeconds(5);

            new DataContextBuilder().UseCacheSlidingExpiration(ttl);

            DataContextCache.CacheSlidingExpiration.Should().Be(ttl);
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void UseCacheSlidingExpiration_Negative_ShouldThrow()
    {
        var act = () => new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SlidingExpiration_Should_Evict_After_Ttl()
    {
        DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(200);
        try
        {
            var key = typeof(SlidingExpirationMarker);
            DataContextCache.SelectListCache[key] = [];
            DataContextCache.SelectListCache.ContainsKey(key).Should().BeTrue();

            Thread.Sleep(400);

            DataContextCache.SelectListCache.ContainsKey(key).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            DataContextCache.Clear();
        }
    }

    [Fact]
    public void SlidingExpiration_Should_Refresh_On_Read()
    {
        DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(400);
        try
        {
            var key = typeof(RefreshMarker);
            DataContextCache.SelectListCache[key] = [];

            Thread.Sleep(250);
            DataContextCache.SelectListCache.ContainsKey(key).Should().BeTrue();
            Thread.Sleep(250);

            DataContextCache.SelectListCache.ContainsKey(key).Should().BeTrue();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            DataContextCache.Clear();
        }
    }
}
