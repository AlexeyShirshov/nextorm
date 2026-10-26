using FluentAssertions;

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
}
