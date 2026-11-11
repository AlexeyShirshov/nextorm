using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for the T349 MySQL 8.4 JSON catalogue (no database): every constructor, query
/// function and aggregate renders its native spelling, the <c>-&gt;</c>/<c>-&gt;&gt;</c>/<c>MEMBER OF</c>
/// operators render their exact operator text, SQL NULL arguments stay NULL, and the compile-time
/// grammar literals (<c>one_or_all</c>, <c>JSON_SEARCH</c>'s escape and paths, the <c>-&gt;</c> path) are
/// validated before any SQL is emitted.
/// </summary>
public class T349JsonSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void JsonConstructorsAndQueries_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_array(1, "a")))
            .Should().Contain("json_array(1, _utf8mb4 X'61')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_array(1, null)))
            .Should().Contain("json_array(1, null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_object("a", 1, "b", null)))
            .Should().Contain("json_object(_utf8mb4 X'61', 1, _utf8mb4 X'62', null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_contains(x.String, "{\"a\":1}")))
            .Should().Contain("json_contains(somestring, _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_contains(x.String, "{\"a\":1}", "$.a")))
            .Should().Contain("json_contains(somestring, _utf8mb4 X'7b2261223a317d', _utf8mb4 X'242e61')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_contains_path(x.String, "all", "$.a", "$.b")))
            .Should().Contain("json_contains_path(somestring, 'all', '$.a', '$.b')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_merge(x.String, "{\"a\":1}", "{\"b\":2}")))
            .Should().Contain("json_merge(somestring, _utf8mb4 X'7b2261223a317d', _utf8mb4 X'7b2262223a327d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_overlaps(x.String, "{\"a\":1}")))
            .Should().Contain("json_overlaps(somestring, _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_overlaps(null, null)))
            .Should().Contain("json_overlaps(null, null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_pretty(x.String)))
            .Should().Contain("json_pretty(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_pretty(null)))
            .Should().Contain("json_pretty(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_quote("a")))
            .Should().Contain("json_quote(_utf8mb4 X'61')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_schema_valid(x.String, "{\"a\":1}")))
            .Should().Contain("json_schema_valid(somestring, _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_schema_validation_report(x.String, "{\"a\":1}")))
            .Should().Contain("json_schema_validation_report(somestring, _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_search(x.String, "one", "joe")))
            .Should().Contain("json_search(somestring, 'one', _utf8mb4 X'6a6f65')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_search(x.String, "all", "joe", "$")))
            .Should().Contain("json_search(somestring, 'all', _utf8mb4 X'6a6f65', '$')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_search(x.String, "all", "joe", null, "$.a")))
            .Should().Contain("json_search(somestring, 'all', _utf8mb4 X'6a6f65', null, '$.a')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_size(x.String)))
            .Should().Contain("json_storage_size(somestring)");
    }

    [Fact]
    public void JsonAggregates_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.json_arrayagg("a") }))
            .Should().Contain("json_arrayagg(_utf8mb4 X'61')");
        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.MySql.json_arrayagg(x.String) }))
            .Should().Contain("json_arrayagg(somestring)");
        SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.MySql.json_objectagg("a", 1) }))
            .Should().Contain("json_objectagg(_utf8mb4 X'61', 1)");
    }

    [Fact]
    public void JsonOperators_ShouldRenderExactOperatorText()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_arrow(x.String, "$.a")))
            .Should().Contain("somestring -> '$.a'");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_arrow_text(x.String, "$.a")))
            .Should().Contain("somestring ->> '$.a'");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.member_of(1, "[\"a\"]")))
            .Should().Contain("1 MEMBER OF ('[\"a\"]')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.member_of(x.String, "[\"a\"]")))
            .Should().Contain("somestring MEMBER OF ('[\"a\"]')");
    }

    [Fact]
    public void JsonGrammarAndArgumentShape_InvalidArguments_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var capturedOneOrAll = "one";

        var oddObject = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_object("a", 1, "b")));
        oddObject.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL JSON_OBJECT() requires an even number of key/value arguments.");

        var capturedContainsPath = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_contains_path(x.String, capturedOneOrAll, "$.a")));
        capturedContainsPath.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON_CONTAINS_PATH()*one_or_all*'one' or 'all'*");

        var wrongContainsPath = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_contains_path(x.String, "any", "$.a")));
        wrongContainsPath.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON_CONTAINS_PATH()*one_or_all*'one' or 'all'*");

        var wrongSearch = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_search(x.String, "any", "joe")));
        wrongSearch.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON_SEARCH()*one_or_all*'one' or 'all'*");

        var longEscape = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_search(x.String, "one", "joe", "\\\\")));
        longEscape.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON_SEARCH()*escape_char*compile-time single-character literal*");

        var pathParameter = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_search(x.String, "one", "joe", null, SqlFunctions.Parameter<string>(0))));
        pathParameter.Should().Throw<NotSupportedException>()
            .WithMessage("*JSON_SEARCH()*every path argument*compile-time string literal*");

        var arrowColumn = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_arrow("literal", "$.a")));
        arrowColumn.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->' requires argument 0 to be a column reference.");

        var arrowPath = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_arrow(x.String, SqlFunctions.Parameter<string>(0))));
        arrowPath.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->' requires argument 1 to be a compile-time string literal.");

        var arrowTextColumn = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_arrow_text("literal", "$.a")));
        arrowTextColumn.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->>' requires argument 0 to be a column reference.");

        var arrowTextPath = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.json_arrow_text(x.String, SqlFunctions.Parameter<string>(0))));
        arrowTextPath.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL operator '->>' requires argument 1 to be a compile-time string literal.");

        var memberOfArray = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.member_of(1, SqlFunctions.Parameter<string>(0))));
        memberOfArray.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL MEMBER OF requires argument 1 to be a compile-time JSON-array string literal.");
    }

    [Fact]
    public void T349JsonFunctions_ShouldAllBeReportedSupported()
    {
        var capability = MySqlDialect.Instance.MySqlFunctions!;
        string[] names =
        [
            "json_array", "json_contains", "json_contains_path", "json_merge", "json_object",
            "json_overlaps", "json_pretty", "json_quote", "json_schema_valid",
            "json_schema_validation_report", "json_search", "json_storage_size",
            "json_arrayagg", "json_objectagg",
            "json_arrow", "json_arrow_text", "member_of"
        ];

        names.Should().HaveCount(17);
        foreach (var name in names)
            capability.Supports(name).Should().BeTrue($"'{name}' is a MySQL 8.4 gate key");

        capability.Supports("json_table").Should().BeFalse("JSON_TABLE is a table construct, not a scalar");
        capability.Supports("json_storage_free").Should().BeTrue("T283 keeps its MySQL gate");
    }
}
