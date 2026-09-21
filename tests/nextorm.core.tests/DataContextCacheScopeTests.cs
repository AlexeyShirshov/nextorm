using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Regression tests for F11: there is a single source of truth for the provider-independent caches,
/// and the sharing scope of each cache is intentional rather than accidental.
/// <list type="bullet">
/// <item><see cref="InMemoryDataContext.Metadata"/> and <see cref="InMemoryDataContext.SelectListCache"/>
///   must be the process-wide <see cref="DataContextCache"/> instances. The in-memory provider used
///   to keep a second set of static dictionaries that nothing wrote to and nothing read;</item>
/// <item><see cref="InMemoryDataContext.ExpressionsCache"/> must stay per-instance: its entries embed
///   <c>Expression.Constant(this)</c>, so sharing them process-wide would invoke the wrong context.</item>
/// </list>
/// These never open a database connection.
/// </summary>
public class DataContextCacheScopeTests
{
    [Fact]
    public void Metadata_Should_Be_Shared_With_DataContextCache()
    {
        using var ctx = new InMemoryDataContext();

        ctx.Metadata.Should().BeSameAs(DataContextCache.Metadata);
    }

    [Fact]
    public void SelectListCache_Should_Be_Shared_With_DataContextCache()
    {
        using var ctx = new InMemoryDataContext();

        ctx.SelectListCache.Should().BeSameAs(DataContextCache.SelectListCache);
    }

    [Fact]
    public void ExpressionsCache_Should_Not_Be_Shared_Because_Entries_Capture_The_Context()
    {
        using var ctx = new InMemoryDataContext();

        ctx.ExpressionsCache.Should().NotBeSameAs(DataContextCache.ExpressionsCache);
    }

    [Fact]
    public void Two_Contexts_Should_Not_Share_Their_ExpressionsCache()
    {
        using var first = new InMemoryDataContext();
        using var second = new InMemoryDataContext();

        first.ExpressionsCache.Should().NotBeSameAs(second.ExpressionsCache);
    }
}
