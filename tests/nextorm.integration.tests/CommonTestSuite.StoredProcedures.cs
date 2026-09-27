using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Shared integration coverage for stored procedures. Providers without them (SQLite, ClickHouse)
/// reject the call before opening a connection through <see cref="ISqlDialect.SupportsStoredProcedures"/>;
/// the per-provider procedure tests live in the provider-specific suites.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void ExecuteProcedure_UnsupportedProvider_ThrowsNotSupported()
    {
        Assert.SkipUnless(!Provider.SupportsStoredProcedures, "This provider supports stored procedures.");

        var ctx = _sut.DataProvider;

        var act = () => ctx.ExecuteProcedure("my_proc");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteProcedureAsync_UnsupportedProvider_ThrowsNotSupported()
    {
        Assert.SkipUnless(!Provider.SupportsStoredProcedures, "This provider supports stored procedures.");

        var ctx = _sut.DataProvider;

        var act = async () => await ctx.ExecuteProcedureAsync(
            "my_proc",
            [new ProcedureParameter("p", 1)],
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
