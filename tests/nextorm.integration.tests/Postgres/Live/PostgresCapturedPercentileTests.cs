using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public sealed class PostgresCapturedPercentileTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Percentile_ShouldBindAndValidateEveryCapturedFraction(bool useQueryCache)
    {
        Assert.SkipUnless(PostgresContainer.IsAvailable, PostgresContainer.Failure ?? "PostgreSQL is not available.");
        using var context = PostgresTestProvider.Instance.CreateContext(new DataContextBuilder().UseQueryCache(useQueryCache));
        var source = context.FromSql("""
            select amount from (values (10::double precision), (20::double precision), (30::double precision)) as sample(amount)
            """);

        double Read(double fraction) => source
            .Select(row => SqlFunctions.Postgres.percentile_cont(fraction, () => row.GetDouble("amount")))
            .ToList().Single();

        Read(0.25).Should().Be(15);
        Read(0.75).Should().Be(25);
        FluentActions.Invoking(() => Read(1.5)).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("fraction");
    }
}
