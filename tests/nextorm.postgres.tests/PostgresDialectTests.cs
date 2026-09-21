using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Direct assertions on the PostgreSQL dialect hooks. The aggregate remapping (stdevp/var) and the
/// capability flags are not exercised by the existing query-level tests.
/// </summary>
public class PostgresDialectTests
{
    private static readonly ISqlDialect Dialect = PostgresDialect.Instance;

    [Fact]
    public void MakeTypeName_String_ShouldBeText()
    {
        Dialect.MakeTypeName(typeof(string)).Should().Be("text");
    }

    [Fact]
    public void QuoteIdentifier_ShouldUseDoubleQuotes()
    {
        Dialect.QuoteIdentifier("id").Should().Be("\"id\"");
        Dialect.QuoteIdentifier("a\"b").Should().Be("\"a\"\"b\"");
    }

    [Fact]
    public void QueryModifierHooks_ShouldUsePostgresForms()
    {
        Dialect.DistinctOn.Should().NotBeNull();
        Dialect.DistinctOn!.Render(["a", "b"]).Should().Be("distinct on (a, b) ");

        Dialect.TableSample.Should().NotBeNull();
        Dialect.TableSample!.Supports(TableSampleMethod.System).Should().BeTrue();
        Dialect.TableSample!.Supports(TableSampleMethod.Bernoulli).Should().BeTrue();
        Dialect.TableSample!.Render(TableSampleMethod.System, 10, null).Should().Be(" tablesample system (10)");
        Dialect.TableSample!.Render(TableSampleMethod.Bernoulli, 5, 7).Should().Be(" tablesample bernoulli (5) repeatable (7)");

        Dialect.SupportsWithTies.Should().BeTrue();
        Dialect.Lock.Should().NotBeNull();
        Dialect.Lock!.Render(LockMode.Update).Should().Be(" for update");
        Dialect.Lock!.Render(LockMode.Share).Should().Be(" for share");

        Dialect.SupportsTextSearchFunctions.Should().BeTrue();
    }

    [Fact]
    public void MakeJsonExtract_ShouldDefaultToPlainCall()
    {
        Dialect.MakeJsonExtract("json_extract_string", ["json", "'s'"]).Should().Be("json_extract_string(json, 's')");
    }

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
        Dialect.MakeMathFunction("round", ["x", "2"], [typeof(double)]).Should().Be("round((x)::numeric, 2)");
        Dialect.MakeMathFunction("round", ["x", "2"], [typeof(double?)]).Should().Be("round((x)::numeric, 2)");
        Dialect.MakeMathFunction("round", ["x", "2"], [typeof(float)]).Should().Be("round((x)::numeric, 2)");
        Dialect.MakeMathFunction("round", ["x", "2"], [typeof(decimal)]).Should().Be("round(x, 2)");
        Dialect.MakeMathFunction("log", ["x"], [typeof(double)]).Should().Be("ln(x)");
        Dialect.MakeFunction("fn", "app").Should().Be("app.fn");
        Dialect.MakeFunction("fn", null).Should().Be("fn");
        Dialect.MakeBoolCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeBool(true).Should().Be("true");
        Dialect.MakeBool(false).Should().Be("false");
        Dialect.ConcatStringOperator.Should().Be("||");
    }

    [Fact]
    public void DatePartHooks_ShouldSupportExtendedParts()
    {
        Dialect.SupportsDatePart("year").Should().BeTrue();
        Dialect.SupportsDatePart("quarter").Should().BeTrue();
        Dialect.SupportsDatePart("week").Should().BeTrue();
        Dialect.SupportsDatePart("dow").Should().BeTrue();
        Dialect.SupportsDatePart("isodow").Should().BeTrue();
        Dialect.SupportsDatePart("epoch").Should().BeTrue();
        Dialect.SupportsDatePart("nonsense").Should().BeFalse();

        Dialect.MakeDatePart("quarter", "dt").Should().Be("extract(quarter from dt)");
        Dialect.MakeDatePart("week", "dt").Should().Be("extract(week from dt)");
        Dialect.MakeDatePart("dow", "dt").Should().Be("extract(dow from dt)");
        Dialect.MakeDatePart("isodow", "dt").Should().Be("extract(isodow from dt)");
        Dialect.MakeDatePart("epoch", "dt").Should().Be("cast(extract(epoch from dt) as double precision)");
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
        Dialect.SupportsRandomSeed.Should().BeTrue();
        Dialect.SupportsCryptoFunctions.Should().BeTrue();
        Dialect.SupportsTableFunction("generate_series").Should().BeTrue();
        Dialect.SupportsTableFunction("unnest").Should().BeTrue();
        Dialect.SupportsTableFunction("regexp_matches").Should().BeTrue();
        Dialect.SupportsTableFunction("regexp_split_to_table").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_array_elements").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_array_elements_text").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_each").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_each_text").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_object_keys").Should().BeTrue();
        Dialect.SupportsTableFunction("jsonb_path_query").Should().BeTrue();
        Dialect.SupportsTableFunction("ts_stat").Should().BeTrue();
        Dialect.SupportsTableFunction("string_split").Should().BeFalse();
        Dialect.SupportsAnyValueAggregate.Should().BeFalse();
        Dialect.SupportsPercentileWindow.Should().BeFalse();
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
    public void SessionInfoHooks_ShouldUsePostgresForms()
    {
        Dialect.SessionInfoFunctions.Should().NotBeNull();
        Dialect.SessionInfoFunctions!.Supports("current_user").Should().BeTrue();
        Dialect.SessionInfoFunctions!.Supports("current_database").Should().BeTrue();
        Dialect.SessionInfoFunctions!.Render("current_user").Should().Be("current_user");
        Dialect.SessionInfoFunctions!.Render("session_user").Should().Be("session_user");
        Dialect.SessionInfoFunctions!.Render("current_schema").Should().Be("current_schema");
        Dialect.SessionInfoFunctions!.Render("current_database").Should().Be("current_database()");
        Dialect.SessionInfoFunctions!.Render("version").Should().Be("version()");
    }

    [Fact]
    public void UuidHooks_ShouldUsePostgresForms()
    {
        Dialect.UuidGenerators.Should().NotBeNull();
        Dialect.UuidGenerators!.Supports("gen_random_uuid").Should().BeTrue();
        Dialect.UuidGenerators!.Supports("uuidv7").Should().BeTrue();
        Dialect.UuidGenerators!.Render("gen_random_uuid").Should().Be("gen_random_uuid()");
        Dialect.UuidGenerators!.Render("uuidv7").Should().Be("uuidv7()");
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
