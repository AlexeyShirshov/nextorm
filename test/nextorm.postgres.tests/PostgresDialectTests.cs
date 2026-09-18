using System.Text.Json;
using FluentAssertions;
using nextorm.core;
using nextorm.postgres;
using Npgsql;
using NpgsqlTypes;

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
    public void ApplyHooks_ShouldUseLateralJoin()
    {
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.MakeApply(JoinType.CrossApply, "src").Should().Be(" cross join lateral src");
        Dialect.MakeApply(JoinType.OuterApply, "src").Should().Be(" left join lateral src on true");
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchPostgres()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeTrue();
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.SupportsQueryHints.Should().BeFalse();
        Dialect.SupportsArrays.Should().BeTrue();
        Dialect.SupportsJson.Should().BeTrue();
        Dialect.SupportsFilter.Should().BeTrue();
        Dialect.SupportsGreatestLeast.Should().BeTrue();
        Dialect.SupportsDateTrunc.Should().BeTrue();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsStringArrayAggregates.Should().BeTrue();
        Dialect.SupportsFullText.Should().BeTrue();
        Dialect.SupportsTableFunction("generate_series").Should().BeTrue();
        Dialect.SupportsTableFunction("unnest").Should().BeTrue();
        Dialect.SupportsTableFunction("string_split").Should().BeFalse();
    }

    [Fact]
    public void FullTextHooks_ShouldUseTsvectorMatch()
    {
        Dialect.MakeFullText("contains", "c", "@p")
            .Should().Be("to_tsvector(c) @@ plainto_tsquery(@p)");
        Dialect.MakeFullText("freetext", "c", "@p")
            .Should().Be("to_tsvector(c) @@ websearch_to_tsquery(@p)");
    }

    [Fact]
    public void BuiltinFunctionHooks_ShouldUsePostgresForms()
    {
        Dialect.MakeNullIf("x", "y").Should().Be("nullif(x, y)");
        Dialect.MakeGreatest(["a", "b"]).Should().Be("greatest(a, b)");
        Dialect.MakeLeast(["a", "b"]).Should().Be("least(a, b)");
        Dialect.MakeDateTrunc("month", "x").Should().Be("date_trunc('month', x)");
        Dialect.MakeDateAdd("day", "n", "x").Should().Be("x + (n * interval '1 day')");
        Dialect.MakeDateDiff("day", "a", "b").Should().Be("(cast(b as date) - cast(a as date))");
        Dialect.MakeEndOfMonth("x").Should().Be("(date_trunc('month', x) + interval '1 month - 1 day')");
        Dialect.MakeDateFromParts("y", "m", "d").Should().Be("make_date(y, m, d)");
        Dialect.MakeStringAgg("x", "','").Should().Be("string_agg(x, ',')");
        Dialect.MakeArrayAgg("x").Should().Be("array_agg(x)");
    }

    [Fact]
    public void CreateParam_JsonDocument_ShouldBindAsJsonb()
    {
        using var ctx = PostgresTestContext.CreatePostgres();
        using var document = JsonDocument.Parse("{\"a\":1}");

        var parameter = (NpgsqlParameter)ctx.CreateParam("p", document);

        parameter.NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
    }

    [Fact]
    public void CreateParam_PlainString_ShouldStayText()
    {
        using var ctx = PostgresTestContext.CreatePostgres();

        var parameter = (NpgsqlParameter)ctx.CreateParam("p", "{\"a\":1}");

        parameter.NpgsqlDbType.Should().NotBe(NpgsqlDbType.Jsonb);
    }
}
