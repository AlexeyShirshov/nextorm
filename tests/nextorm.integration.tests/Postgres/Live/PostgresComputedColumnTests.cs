using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public sealed class PostgresComputedColumnTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetColumn_ShouldReadTheColumnCapturedByEachQuery(bool useQueryCache)
    {
        Assert.SkipUnless(PostgresContainer.IsAvailable, PostgresContainer.Failure ?? "PostgreSQL is not available.");
        using var context = PostgresTestProvider.Instance.CreateContext(new DataContextBuilder().UseQueryCache(useQueryCache));
        var cancellationToken = TestContext.Current.CancellationToken;
        var settings = context.From("pg_catalog.pg_settings")
            .Where(row => row.GetString("name") == "server_version_num");

        async Task<List<string>> ReadColumn(string column)
        {
            var result = new List<string>();
            var query = settings.Select(row => (string)row.GetColumn(column));
            await foreach (var value in query.ToAsyncEnumerable(cancellationToken: cancellationToken))
                result.Add(value);
            return result;
        }

        (await ReadColumn("name")).Should().Equal("server_version_num");
        (await ReadColumn("vartype")).Should().Equal("integer");
    }
}
