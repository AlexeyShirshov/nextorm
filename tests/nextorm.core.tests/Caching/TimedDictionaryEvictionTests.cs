using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Deterministic tests for the test-only sliding-expiration hook. They mutate the process-wide
/// <see cref="DataContextCache.CacheSlidingExpiration"/>, so they join the "Query cache controls"
/// collection that serializes every test touching the cache controls.
/// </summary>
[Collection("Query cache controls")]
public class TimedDictionaryEvictionTests
{
    private sealed class EvictionMarker;

    private static TimeSpan WithWindow()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(1000);
        return previous;
    }

    [Fact]
    public void ExpireEntriesForTesting_Should_Age_Timed_Entry_For_Next_Read()
    {
        var previous = WithWindow();
        try
        {
            var dict = new TimedDictionary<int, string>();
            dict[1] = "one";
            dict.ContainsKey(1).Should().BeTrue();

            dict.ExpireEntriesForTesting();

            dict.ContainsKey(1).Should().BeFalse();
            dict.TryGetValue(1, out _).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireEntriesForTesting_Zero_Ttl_Should_Not_Age()
    {
        var previous = WithWindow();
        try
        {
            var dict = new TimedDictionary<int, string>();
            dict[1] = "one";

            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            dict.ExpireEntriesForTesting();
            DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(1000);

            // If the hook aged the entry anyway, the next read would now cross the re-armed window.
            dict.ContainsKey(1).Should().BeTrue();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireEntriesForTesting_Empty_Dictionary_Is_NoOp()
    {
        var previous = WithWindow();
        try
        {
            var dict = new TimedDictionary<int, string>();

            dict.ExpireEntriesForTesting();

            dict.Count.Should().Be(0);
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireEntriesForTesting_Ages_All_Timed_Entries()
    {
        var previous = WithWindow();
        try
        {
            var dict = new TimedDictionary<int, string>();
            dict[1] = "a";
            dict[2] = "b";
            dict[3] = "c";

            dict.ExpireEntriesForTesting();

            dict.ContainsKey(1).Should().BeFalse();
            dict.ContainsKey(2).Should().BeFalse();
            dict.ContainsKey(3).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireEntriesForTesting_Should_Age_Without_Removing()
    {
        var previous = WithWindow();
        try
        {
            var dict = new TimedDictionary<int, string>();
            dict[1] = "one";

            dict.ExpireEntriesForTesting();

            // Age-only: the raw entry is still present; the next read performs the removal.
            dict.Remove(1).Should().BeTrue();
            dict.ContainsKey(1).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireEntriesForTesting_Should_Expire_Value_And_Nullable_Payloads()
    {
        var previous = WithWindow();
        try
        {
            var values = new TimedDictionary<int, int>();
            values[7] = 42;
            var nullable = new TimedDictionary<int, int?>();
            nullable[7] = null;

            values.ExpireEntriesForTesting();
            nullable.ExpireEntriesForTesting();

            values.TryGetValue(7, out _).Should().BeFalse();
            nullable.TryGetValue(7, out _).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
        }
    }

    [Fact]
    public void ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches()
    {
        var previous = WithWindow();
        try
        {
            using var ctx = new InMemoryDataContext();
            ctx.Data[typeof(SimpleEntity)] = new SimpleEntity[] { new() { Id = 1 } };
            ctx.From<SimpleEntity>();

            var metadataKey = typeof(SimpleEntity);
            var tvpKey = typeof(EvictionMarker);
            var selectKey = typeof(EvictionMarker);
            var expKey = new ExpressionKey((Expression<Func<int, int>>)(x => x + 1), new QueryProvider());
            var inKey = new ExpressionKey((Expression<Func<int, int>>)(x => x * 2), new QueryProvider());

            DataContextCache.Metadata.ContainsKey(metadataKey).Should().BeTrue();
            DataContextCache.TvpMetadata[tvpKey] = DataContextCache.Metadata[metadataKey];
            DataContextCache.SelectListCache[selectKey] = [];
            DataContextCache.ExpressionsCache[expKey] = (Func<int, int>)(x => x + 1);
            DataContextCache.InValuesCache[inKey] = _ => null;

            DataContextCache.ExpireTimedEntriesForTesting();

            DataContextCache.Metadata.ContainsKey(metadataKey).Should().BeFalse();
            DataContextCache.TvpMetadata.ContainsKey(tvpKey).Should().BeFalse();
            DataContextCache.SelectListCache.ContainsKey(selectKey).Should().BeFalse();
            DataContextCache.ExpressionsCache.ContainsKey(expKey).Should().BeFalse();
            DataContextCache.InValuesCache.ContainsKey(inKey).Should().BeFalse();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
    }

    [Fact]
    public void ExpireTimedEntriesForTesting_Should_Not_Touch_Plain_Stores_Or_PlanStore()
    {
        const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        var plainMetadata = (IDictionary<Type, byte>)typeof(DataContextCache)
            .GetField("_autoPublishedJunctionMetadata", PrivateStatic)!
            .GetValue(null)!;
        var filterAccessors = (IDictionary<string, Func<IDataContext, object>>)typeof(DataContextCache)
            .GetField("_queryFilterContextAccessors", PrivateStatic)!
            .GetValue(null)!;
        var generationField = typeof(QueryPlanStore).GetField("_generation", PrivateStatic)!;

        var accessor = (Func<IDataContext, object>)(_ => new object());
        plainMetadata.Clear();
        filterAccessors.Clear();
        plainMetadata[typeof(TimedDictionaryEvictionTests)] = 0;
        filterAccessors["sentinel"] = accessor;

        var plainMetadataCount = plainMetadata.Count;
        var filterAccessorsCount = filterAccessors.Count;
        var generationBefore = (long)generationField.GetValue(null)!;

        var previous = WithWindow();
        try
        {
            DataContextCache.SelectListCache[typeof(EvictionMarker)] = [];
            DataContextCache.SelectListCache.ContainsKey(typeof(EvictionMarker)).Should().BeTrue();

            DataContextCache.ExpireTimedEntriesForTesting();

            // Timed store IS aged/evicted by the hook (contrast row).
            DataContextCache.SelectListCache.ContainsKey(typeof(EvictionMarker)).Should().BeFalse();

            // The two plain (non-timed) ConcurrentDictionary stores are not cleared nor aged.
            plainMetadata.Count.Should().Be(plainMetadataCount);
            plainMetadata[typeof(TimedDictionaryEvictionTests)].Should().Be(0);
            filterAccessors.Count.Should().Be(filterAccessorsCount);
            filterAccessors["sentinel"].Should().BeSameAs(accessor);

            // QueryPlanStore is not invalidated by the hook.
            ((long)generationField.GetValue(null)!).Should().Be(generationBefore);

            // The hook delegates only to the five timed stores (DataContextCache.cs:151-155).
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            plainMetadata.Clear();
            filterAccessors.Clear();
            DataContextCache.Clear();
        }
    }
}
