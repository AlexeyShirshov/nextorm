using System.Linq.Expressions;
using FluentAssertions;
using Xunit.v3;

namespace NextORM.Core.Tests;

/// <summary>
/// T342 (#342) ordered-pair instrument. It reproduces the order-dependent contamination of
/// <see cref="TimedDictionaryEvictionTests.ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches"/>
/// by invoking two real public test methods in one process, in the order the full suite can produce:
/// <see cref="CachedPreparationRecipeTests.Recipe_BareEntitySelect_IsNotProbed"/> seeds the metadata of
/// <see cref="SimpleEntity"/> while the process-wide sliding window is <see cref="TimeSpan.Zero"/> — the
/// insertion-time timing rule then keeps that entry untimed forever — and the eviction fact reuses the
/// same entry and can no longer age it.
/// <para>
/// The pair owns no cache poke of its own: it calls the two real facts, so a rename or signature change
/// fails compilation instead of silently degrading the reproduction. It only saves/restores the window
/// and clears the process-wide caches in its outer <c>finally</c>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class CacheIsolationRegressionTests
{
    /// <summary>
    /// A1 (T342): the minimal seeder → eviction pair is red before the repair and green after it.
    /// Clean ownership of the shared metadata key is asserted, never enforced by clearing here: a
    /// leaked entry is a real finding, not something this harness may erase.
    /// </summary>
    [Fact]
    public void T342_OrderedPair_SeederThenEviction()
    {
        DataContextCache.Metadata.ContainsKey(typeof(SimpleEntity)).Should().BeFalse(
            "the ordered pair starts from cache state that the preceding tests handed over cleanly; "
            + "this harness never clears it to pass");

        var previousWindow = DataContextCache.CacheSlidingExpiration;
        try
        {
            DataContextCache.CacheSlidingExpiration = TimeSpan.Zero;

            TestContext.Current.SendDiagnosticMessage(
                "T342: event=seeder-begin method=NextORM.Core.Tests.CachedPreparationRecipeTests."
                + "Recipe_BareEntitySelect_IsNotProbed window=00:00:00");

            var seeder = new CachedPreparationRecipeTests();
            try
            {
                seeder.Recipe_BareEntitySelect_IsNotProbed();
            }
            finally
            {
                // Compiles both before and after the seeder class owns teardown (D3 makes it IDisposable):
                // the class is sealed and does not implement IDisposable today, so the check goes through
                // object (a sealed type has no direct reference conversion to an unrelated interface).
                ((object)seeder as IDisposable)?.Dispose();
            }

            TestContext.Current.SendDiagnosticMessage(
                "T342: event=lifecycle-boundary seeder-disposed next=NextORM.Core.Tests."
                + "TimedDictionaryEvictionTests.ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches");

            new TimedDictionaryEvictionTests()
                .ExpireTimedEntriesForTesting_Should_Age_All_Five_ProcessWide_Caches();
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = previousWindow;
            DataContextCache.Clear();
        }
    }

    /// <summary>Key type the fixture control owns in all five process-wide caches.</summary>
    private sealed class FixtureSentinel
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// Value source for the metadata sentinel. Resolving it writes only its own auto-path entry, so the
    /// five <see cref="FixtureSentinel"/> keys stay untouched until the control inserts them.
    /// </summary>
    private sealed class FixtureMetadataSource;

    /// <summary>
    /// E12 (T342) fixture control: inserting one sentinel into each of the five process-wide caches and
    /// arming a non-default window, then invoking the assembly fixture's test-finish notification through
    /// the <see cref="INotifyTestLifecycle"/> interface member (with the running test's identity, exactly
    /// as the runner does) must remove every sentinel and restore the window the fixture captured when it
    /// was constructed; the start body is invoked directly, since the control only asserts the finish
    /// contract.
    /// <para>
    /// The fixture is constructed while <c>1234 ms</c> is armed and the window is moved to
    /// <c>4321 ms</c> before the sentinels are seeded, so all three repair sites are individually
    /// observable: a fixture that captured a hard-coded default instead of the live window restores
    /// <see cref="TimeSpan.Zero"/>, one that never restores leaves <c>4321 ms</c> behind, and one that
    /// never clears leaves the five sentinels in place.
    /// </para>
    /// </summary>
    [Fact]
    public void T342_FixtureTeardown_Should_RestoreWindowAndRemoveSentinels()
    {
        var assemblyWindow = DataContextCache.CacheSlidingExpiration;
        var finishCountBefore = ProcessWideCacheIsolationFixture.FinishCount;
        var capturedWindow = TimeSpan.FromMilliseconds(1234);
        var mutatedWindow = TimeSpan.FromMilliseconds(4321);
        var sentinelType = typeof(FixtureSentinel);
        var expressionKey = new ExpressionKey(
            (Expression<Func<FixtureSentinel, int>>)(x => x.Id), new QueryProvider());
        var inValuesKey = new ExpressionKey(
            (Expression<Func<FixtureSentinel, int>>)(x => x.Id + 1), new QueryProvider());
        ProcessWideCacheIsolationFixture? fixture = null;

        try
        {
            DataContextCache.CacheSlidingExpiration = capturedWindow;
            fixture = new ProcessWideCacheIsolationFixture();

            DataContextCache.CacheSlidingExpiration = mutatedWindow;

            var metadata = DataContextExtensions.ResolveMetadata(null, typeof(FixtureMetadataSource));
            DataContextCache.Metadata[sentinelType] = metadata;
            DataContextCache.TvpMetadata[sentinelType] = metadata;
            DataContextCache.SelectListCache[sentinelType] = [];
            DataContextCache.ExpressionsCache[expressionKey] = (Func<FixtureSentinel, int>)(x => x.Id);
            DataContextCache.InValuesCache[inValuesKey] = _ => null;

            DataContextCache.Metadata.ContainsKey(sentinelType).Should().BeTrue("the control seeds the metadata cache");
            DataContextCache.TvpMetadata.ContainsKey(sentinelType).Should().BeTrue("the control seeds the TVP metadata cache");
            DataContextCache.SelectListCache.ContainsKey(sentinelType).Should().BeTrue("the control seeds the select-list cache");
            DataContextCache.ExpressionsCache.ContainsKey(expressionKey).Should().BeTrue("the control seeds the expression cache");
            DataContextCache.InValuesCache.ContainsKey(inValuesKey).Should().BeTrue("the control seeds the in-values cache");

            var currentTest = TestContext.Current.Test as IXunitTest;
            currentTest.Should().NotBeNull(
                "the running test is the IXunitTest identity the runner hands to the lifecycle callback");
            fixture.Start(nameof(T342_FixtureTeardown_Should_RestoreWindowAndRemoveSentinels));
            ((INotifyTestLifecycle)fixture).OnTestFinished(currentTest!);

            DataContextCache.Metadata.ContainsKey(sentinelType).Should().BeFalse("the fixture teardown clears the metadata cache");
            DataContextCache.TvpMetadata.ContainsKey(sentinelType).Should().BeFalse("the fixture teardown clears the TVP metadata cache");
            DataContextCache.SelectListCache.ContainsKey(sentinelType).Should().BeFalse("the fixture teardown clears the select-list cache");
            DataContextCache.ExpressionsCache.ContainsKey(expressionKey).Should().BeFalse("the fixture teardown clears the expression cache");
            DataContextCache.InValuesCache.ContainsKey(inValuesKey).Should().BeFalse("the fixture teardown clears the in-values cache");

            DataContextCache.CacheSlidingExpiration.Should().Be(
                capturedWindow,
                "the fixture restores the window it captured: neither the hard-coded default nor the window the finished test left behind");
            ProcessWideCacheIsolationFixture.LastRestoredWindow.Should().Be(
                capturedWindow, "the restored window is observable to controls");
            ProcessWideCacheIsolationFixture.FinishCount.Should().Be(
                finishCountBefore + 1, "one direct finish callback increments the counter exactly once");
        }
        finally
        {
            DataContextCache.CacheSlidingExpiration = assemblyWindow;
            DataContextCache.Clear();
        }
    }
}
