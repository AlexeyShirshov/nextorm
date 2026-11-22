using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

public sealed class MariaDbBoundedDeleteTests
{
    [Fact]
    public async Task D187_BoundedDeleteKeepsCompositeKeysAndRespectsCap()
    {
        MariaDbContainer.IsAvailable.Should().BeTrue(MariaDbContainer.Failure ?? "MariaDB is required");
        await using var context = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        await CommonTestSuite.RunD187BoundedDelete(context, TestContext.Current.CancellationToken);
    }
}
