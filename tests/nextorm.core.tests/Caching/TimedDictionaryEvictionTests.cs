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

    /// <summary>
    /// Entity type private to the five-cache scenario. Keying the scenario on a type no other test
    /// mentions is what makes the fact order-independent: a seeder that populates a shared entity while
    /// the sliding window is disabled can no longer hand this scenario an untimed entry it reuses.
    /// </summary>
    private sealed class EvictionWindowEntity
    {
        public int Id { get; set; }
    }

    /// <summary>Key marker for the untimed-semantics control; used by no other scenario.</summary>
    private sealed class UntimedEntryMarker;

    private sealed record FiveCacheKeys(
        Type MetadataKey,
        Type TvpKey,
        Type SelectKey,
        ExpressionKey ExpressionsKey,
        ExpressionKey InValuesKey);

    /// <summary>
    /// The five keys the hermetic scenario owns. Both expression identities mention the private entity,
    /// so no unrelated <c>x =&gt; x + 1</c> cache entry can collide with them.
    /// </summary>
    private static FiveCacheKeys CreateFiveCacheKeys() => new(
        typeof(EvictionWindowEntity),
        typeof(EvictionWindowEntity),
        typeof(EvictionWindowEntity),
        new ExpressionKey((Expression<Func<EvictionWindowEntity, int>>)(x => x.Id), new QueryProvider()),
        new ExpressionKey((Expression<Func<EvictionWindowEntity, int>>)(x => x.Id + 1), new QueryProvider()));

    /// <summary>
    /// The five-cache aging scenario. Ownership of every key is asserted <b>before</b> anything is
    /// inserted: a pre-existing entry is a leaked seeder and fails loudly, it is never cleared to pass.
    /// The window is armed before creation, the real <c>From&lt;T&gt;()</c> metadata path seeds the
    /// metadata key, the four remaining keys are inserted through the production cache surface, all five
    /// are asserted present, aged, and asserted absent. The window is restored and the process-wide
    /// caches are cleared in the <c>finally</c>; <paramref name="observeAfterAging"/> runs after the
    /// absence assertions and before that cleanup so a control can inspect the surviving entries.
    /// </summary>
    private static void RunFiveCacheScenario(Action<FiveCacheKeys>? observeAfterAging = null)
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        try
        {
            var keys = CreateFiveCacheKeys();

            DataContextCache.Metadata.ContainsKey(keys.MetadataKey).Should().BeFalse(
                "Metadata[EvictionWindowEntity] must be owned by this scenario; a previous test left it cached");
            DataContextCache.TvpMetadata.ContainsKey(keys.TvpKey).Should().BeFalse(
                "TvpMetadata[EvictionWindowEntity] must be owned by this scenario; a previous test left it cached");
            DataContextCache.SelectListCache.ContainsKey(keys.SelectKey).Should().BeFalse(
                "SelectListCache[EvictionWindowEntity] must be owned by this scenario; a previous test left it cached");
            DataContextCache.ExpressionsCache.ContainsKey(keys.ExpressionsKey).Should().BeFalse(
                "ExpressionsCache[EvictionWindowEntity.Id] must be owned by this scenario; a previous test left it cached");
            DataContextCache.InValuesCache.ContainsKey(keys.InValuesKey).Should().BeFalse(
                "InValuesCache[EvictionWindowEntity.Id + 1] must be owned by this scenario; a previous test left it cached");

            DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(1000);

            using var ctx = new InMemoryDataContext();
            ctx.Data[typeof(EvictionWindowEntity)] = new EvictionWindowEntity[] { new() { Id = 1 } };
            ctx.From<EvictionWindowEntity>();

            DataContextCache.Metadata.ContainsKey(keys.MetadataKey).Should().BeTrue(
                "the real From<EvictionWindowEntity>() metadata path seeds the metadata cache");
            DataContextCache.TvpMetadata[keys.TvpKey] = DataContextCache.Metadata[keys.MetadataKey];
            DataContextCache.SelectListCache[keys.SelectKey] = [];
            DataContextCache.ExpressionsCache[keys.ExpressionsKey] = (Func<EvictionWindowEntity, int>)(x => x.Id);
            DataContextCache.InValuesCache[keys.InValuesKey] = _ => null;

            DataContextCache.Metadata.ContainsKey(keys.MetadataKey).Should().BeTrue(
                "metadata is present before aging");
            DataContextCache.TvpMetadata.ContainsKey(keys.TvpKey).Should().BeTrue(
                "TVP metadata is present before aging");
            DataContextCache.SelectListCache.ContainsKey(keys.SelectKey).Should().BeTrue(
                "the select list is present before aging");
            DataContextCache.ExpressionsCache.ContainsKey(keys.ExpressionsKey).Should().BeTrue(
                "the compiled expression delegate is present before aging");
            DataContextCache.InValuesCache.ContainsKey(keys.InValuesKey).Should().BeTrue(
                "the in-values accessor is present before aging");

            DataContextCache.ExpireTimedEntriesForTesting();

            DataContextCache.Metadata.ContainsKey(keys.MetadataKey).Should().BeFalse();
            DataContextCache.TvpMetadata.ContainsKey(keys.TvpKey).Should().BeFalse();
            DataContextCache.SelectListCache.ContainsKey(keys.SelectKey).Should().BeFalse();
            DataContextCache.ExpressionsCache.ContainsKey(keys.ExpressionsKey).Should().BeFalse();
            DataContextCache.InValuesCache.ContainsKey(keys.InValuesKey).Should().BeFalse();

            observeAfterAging?.Invoke(keys);
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
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
        => RunFiveCacheScenario();

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

    /// <summary>
    /// A2 control: a real retained seeder (the shared <see cref="SimpleEntity"/> entry, created while the
    /// window is disabled) stays live and the private scenario still ages all five of its own keys. This
    /// pins the amplifier as intended product behaviour rather than concealing it.
    /// </summary>
    [Fact]
    public void T342_RetainedSeeder_Should_Not_AffectPrivateScenario()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        CachedPreparationRecipeTests? seeder = null;
        try
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            seeder = new CachedPreparationRecipeTests();
            seeder.Recipe_BareEntitySelect_IsNotProbed();

            DataContextCache.Metadata.ContainsKey(typeof(SimpleEntity)).Should().BeTrue(
                "the retained real seeder stores an untimed SimpleEntity entry while the window is disabled");

            RunFiveCacheScenario(keys =>
            {
                DataContextCache.Metadata.ContainsKey(typeof(SimpleEntity)).Should().BeTrue(
                    "an entry created under a zero window is untimed and must survive the scenario's aging");
                DataContextCache.Metadata.ContainsKey(keys.MetadataKey).Should().BeFalse(
                    "the scenario's own key is still aged even while the unrelated untimed entry survives");
            });
        }
        finally
        {
            if (seeder is not null)
                ((object)seeder as IDisposable)?.Dispose();
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
    }

    /// <summary>
    /// A2 control: an entry inserted while the window is <see cref="TimeSpan.Zero"/> stays untimed after
    /// the window is enabled. The insertion-time timing rule is the documented behaviour; this test pins
    /// it so the scenario's repair is never mistaken for a product change.
    /// </summary>
    [Fact]
    public void T342_UntimedEntry_Should_StayLive_AfterWindowEnabled()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        try
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            var marker = typeof(UntimedEntryMarker);
            DataContextCache.SelectListCache[marker] = [];
            DataContextCache.SelectListCache.ContainsKey(marker).Should().BeTrue(
                "the entry is inserted through the production cache surface while the window is zero");

            DataContextCache.CacheSlidingExpiration = TimeSpan.FromMilliseconds(1000);
            DataContextCache.ExpireTimedEntriesForTesting();

            DataContextCache.SelectListCache.ContainsKey(marker).Should().BeTrue(
                "an entry created under a zero window is untimed by design; enabling the window does not retroactively time it");
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
    }

    /// <summary>
    /// A2 control: the scenario's private expression identities never touch the original
    /// <c>x =&gt; x + 1</c> entries. Sentinels installed under that identity must keep their exact
    /// delegate instances through the scenario's insert/age cycle.
    /// </summary>
    [Fact]
    public void T342_NoCollateralOverwrite_Should_KeepSentinelDelegates()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        try
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;
            var sentinelKey = new ExpressionKey((Expression<Func<int, int>>)(x => x + 1), new QueryProvider());
            Func<int, int> expressionSentinel = x => x + 41;
            Func<object?, object?> inValuesSentinel = _ => "t342-sentinel";
            DataContextCache.ExpressionsCache[sentinelKey] = expressionSentinel;
            DataContextCache.InValuesCache[sentinelKey] = inValuesSentinel;

            RunFiveCacheScenario(_ =>
            {
                DataContextCache.ExpressionsCache[sentinelKey].Should().BeSameAs(
                    expressionSentinel,
                    "the scenario owns private entity identities and must never overwrite the original x => x + 1 delegate");
                DataContextCache.InValuesCache[sentinelKey].Should().BeSameAs(
                    inValuesSentinel,
                    "the scenario owns private entity identities and must never overwrite the original x => x + 1 accessor");
            });
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
    }

    /// <summary>
    /// A2 control: contaminating each owned key in turn fails the scenario setup with that key's own
    /// ownership diagnostic - a named setup failure, never a silent clear or overwrite.
    /// </summary>
    [Fact]
    public void T342_OwnedKeyContamination_Should_FailWithNamedKey()
    {
        var previous = DataContextCache.CacheSlidingExpiration;
        var staleMetadata = DataContextExtensions.ResolveMetadata(null, typeof(UntimedEntryMarker));
        try
        {
            (string Cache, Action<FiveCacheKeys> Prepopulate, string Expected)[] probes =
            [
                ("Metadata",
                    keys => DataContextCache.Metadata[keys.MetadataKey] = staleMetadata,
                    "Metadata[EvictionWindowEntity] must be owned by this scenario"),
                ("TvpMetadata",
                    keys => DataContextCache.TvpMetadata[keys.TvpKey] = staleMetadata,
                    "TvpMetadata[EvictionWindowEntity] must be owned by this scenario"),
                ("SelectListCache",
                    keys => DataContextCache.SelectListCache[keys.SelectKey] = [],
                    "SelectListCache[EvictionWindowEntity] must be owned by this scenario"),
                ("ExpressionsCache",
                    keys => DataContextCache.ExpressionsCache[keys.ExpressionsKey] = (Func<EvictionWindowEntity, int>)(x => x.Id),
                    "ExpressionsCache[EvictionWindowEntity.Id] must be owned by this scenario"),
                ("InValuesCache",
                    keys => DataContextCache.InValuesCache[keys.InValuesKey] = _ => null,
                    "InValuesCache[EvictionWindowEntity.Id + 1] must be owned by this scenario"),
            ];

            foreach (var (cache, prepopulate, expected) in probes)
            {
                DataContextCache.Clear();
                prepopulate(CreateFiveCacheKeys());

                var thrown = FluentActions.Invoking(() => RunFiveCacheScenario())
                    .Should().Throw<Exception>(
                        $"a leftover {cache} entry must fail the scenario setup instead of being cleared")
                    .Which;

                thrown.Message.Should().Contain(
                    expected,
                    $"the setup failure names the contaminated {cache} cache");
            }
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previous;
            DataContextCache.Clear();
        }
    }
}
