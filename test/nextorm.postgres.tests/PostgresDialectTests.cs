using FluentAssertions;
using nextorm.core;
using nextorm.postgres;

namespace nextorm.postgres.tests;

/// <summary>
/// Direct assertions on the PostgreSQL dialect hooks. The aggregate remapping (stdevp/var) and the
/// capability flags are not exercised by the existing query-level tests.
/// </summary>
public class PostgresDialectTests
{
    private static readonly ISqlDialect Dialect = PostgresDialect.Instance;

    [Theory]
    [InlineData("stdev", "stddev")]
    [InlineData("stdevp", "stddev_pop")]
    [InlineData("var", "variance")]
    [InlineData("varp", "var_pop")]
    [InlineData("sum", "sum")]
    public void MakeAggregate_ShouldMapProviderNames(string name, string expected)
    {
        Dialect.MakeAggregate(name).Should().Be(expected);
    }

    [Fact]
    public void ScalarFunctionHooks_ShouldUsePostgresForms()
    {
        Dialect.MakeStringLength("x").Should().Be("length(x)");
        Dialect.MakeMathFunction("log", ["x"]).Should().Be("ln(x)");
        Dialect.MakeMathFunction("round", ["x", "2"]).Should().Be("round(x, 2)");
        Dialect.MakeFunction("fn", "app").Should().Be("app.fn");
        Dialect.MakeFunction("fn", null).Should().Be("fn");
        Dialect.MakeBoolCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeBool(true).Should().Be("true");
        Dialect.MakeBool(false).Should().Be("false");
        Dialect.ConcatStringOperator.Should().Be("||");
    }

    [Fact]
    public void CteHooks_ShouldUseRecursiveKeywordAndNoMaxRecursion()
    {
        Dialect.MakeWith(true).Should().Be("with recursive ");
        Dialect.MakeWith(false).Should().Be("with ");
        Dialect.MakeMaxRecursion(50).Should().BeNull();
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchPostgres()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeTrue();
    }
}
