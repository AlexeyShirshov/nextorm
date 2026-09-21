using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Direct assertions on the SQL Server dialect hooks, independent of a query. This pins the pieces
/// (type mapping, boolean/CASE materialisation, subquery predicates, CTE keywords) that are only
/// reached on a specific query shape and are otherwise easy to regress.
/// </summary>
public class SqlServerDialectTests
{
    private static readonly ISqlDialect Dialect = SqlServerDialect.Instance;

    [Theory]
    [InlineData(typeof(byte), "tinyint")]
    [InlineData(typeof(short), "smallint")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "bigint")]
    [InlineData(typeof(float), "real")]
    [InlineData(typeof(double), "float")]
    [InlineData(typeof(decimal), "decimal(38, 10)")]
    [InlineData(typeof(string), "String")]
    public void MakeTypeName_ShouldMapProviderAndFallBackNames(Type type, string expected)
    {
        Dialect.MakeTypeName(type).Should().Be(expected);
    }

    [Theory]
    [InlineData("exists", true, "exists(SELECT 1)")]
    [InlineData("exists", false, "cast(case when exists(SELECT 1) then 1 else 0 end as bit)")]
    [InlineData("any", true, "any(SELECT 1)")]
    [InlineData("all", false, "cast(case when all(SELECT 1) then 1 else 0 end as bit)")]
    [InlineData("in", true, "in(SELECT 1)")]
    [InlineData("in", false, "in(SELECT 1)")]
    public void MakeSubqueryPredicate_ShouldMaterialiseBooleanKeywords(string keyword, bool asPredicate, string expected)
    {
        Dialect.MakeSubqueryPredicate(keyword, "SELECT 1", asPredicate).Should().Be(expected);
    }

    [Fact]
    public void MakeBoolCoalesce_ShouldCompareCoalescedBitWithOne()
    {
        Dialect.MakeBoolCoalesce("a", "b").Should().Be("(isnull(a,b)) = 1");
    }

    [Theory]
    [InlineData(false, false, "c")]
    [InlineData(true, false, "cast(c as bit)")]
    [InlineData(true, true, "cast(c as bit) = 1")]
    public void MakeCase_ShouldMaterialiseBooleanResultsAsBit(bool isBooleanResult, bool asPredicate, string expected)
    {
        Dialect.MakeCase("c", isBooleanResult, asPredicate).Should().Be(expected);
    }

    [Theory]
    [InlineData("p", true, "p")]
    [InlineData("p", false, "cast(case when p then 1 else 0 end as bit)")]
    public void MakeBooleanPredicate_ShouldMaterialiseScalarUse(string predicate, bool asPredicate, string expected)
    {
        Dialect.MakeBooleanPredicate(predicate, asPredicate).Should().Be(expected);
    }

    [Theory]
    [InlineData("trunc", "round(x, 0, 1)")]
    [InlineData("round", "round(x, 0)")]
    [InlineData("abs", "abs(x)")]
    public void MakeMathFunction_ShouldMapTruncationAndRound(string name, string expected)
    {
        Dialect.MakeMathFunction(name, ["x"]).Should().Be(expected);
    }

    [Fact]
    public void ScalarFunctionHooks_ShouldUseSqlServerForms()
    {
        Dialect.MakeStringLength("x").Should().Be("len(x)");
        Dialect.MakeDatePart("year", "d").Should().Be("datepart(year, d)");
        Dialect.MakeFunction("fn", "dbo").Should().Be("dbo.fn");
        Dialect.MakeFunction("fn", null).Should().Be("fn");
    }

    [Fact]
    public void Count_ShouldUseCountBigWhenRequested()
    {
        Dialect.MakeCount(false, false).Should().Be("count(");
        Dialect.MakeCount(true, false).Should().Be("count(distinct ");
        Dialect.MakeCount(false, true).Should().Be("count_big(");
        Dialect.MakeCount(true, true).Should().Be("count_big(distinct ");
    }

    [Fact]
    public void CteHooks_ShouldDropRecursiveAndExposeMaxRecursion()
    {
        Dialect.MakeWith(true).Should().Be("with ");
        Dialect.MakeWith(false).Should().Be("with ");
        Dialect.MakeMaxRecursion(100).Should().Be("option (maxrecursion 100)");
        Dialect.MakeMaxRecursion(1).Should().Be("option (maxrecursion 1)");
    }

    [Fact]
    public void ApplyHooks_ShouldUseCrossAndOuterApply()
    {
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.MakeApply(JoinType.CrossApply, "src").Should().Be(" cross apply src");
        Dialect.MakeApply(JoinType.OuterApply, "src").Should().Be(" outer apply src");
    }

    [Fact]
    public void ForJsonHooks_ShouldRenderClause()
    {
        Dialect.SupportsForJson.Should().BeTrue();
        Dialect.MakeForJson(new ForJsonClause(ForJsonMode.Path)).Should().Be("for json path");
        Dialect.MakeForJson(new ForJsonClause(ForJsonMode.Auto)).Should().Be("for json auto");
        Dialect.MakeForJson(new ForJsonClause(ForJsonMode.Path, "r", true))
            .Should().Be("for json path, root('r'), include_null_values");
    }

    [Fact]
    public void BuiltInTableFunctions_ShouldBeGated()
    {
        Dialect.SupportsTableFunction("string_split").Should().BeTrue();
        Dialect.SupportsTableFunction("openjson").Should().BeTrue();
        Dialect.SupportsTableFunction("generate_series").Should().BeFalse();
    }

    [Fact]
    public void FullTextHooks_ShouldRenderPredicates()
    {
        Dialect.SupportsFullText.Should().BeTrue();
        Dialect.MakeFullText("contains", "c", "@p").Should().Be("contains(c, @p)");
        Dialect.MakeFullText("freetext", "c", "@p").Should().Be("freetext(c, @p)");
    }

    [Fact]
    public void IsJsonHooks_ShouldRenderPredicateAndValue()
    {
        Dialect.SupportsTextJson.Should().BeTrue();
        Dialect.MakeIsJson("@p", asPredicate: true).Should().Be("(isjson(@p)) = 1");
        Dialect.MakeIsJson("@p", asPredicate: false).Should().Be("cast(isjson(@p) as bit)");
    }

    [Fact]
    public void XmlHooks_ShouldRenderPostfixCalls()
    {
        Dialect.XmlFunctions.Should().NotBeNull();
        Dialect.XmlFunctions!.Supports("value").Should().BeTrue();
        Dialect.XmlFunctions!.Supports("query").Should().BeTrue();
        Dialect.XmlFunctions!.Supports("exist").Should().BeTrue();
        Dialect.XmlFunctions!.Supports("nodes").Should().BeFalse();

        Dialect.XmlFunctions!.Render("value", "payload", ["'(/root)[1]'", "'int'"]).Should().Be("payload.value('(/root)[1]', 'int')");
        Dialect.XmlFunctions!.Render("query", "payload", ["'/root'"]).Should().Be("payload.query('/root')");
        Dialect.XmlFunctions!.Render("exist", "payload", ["'/root'"]).Should().Be("payload.exist('/root')");
    }

    [Fact]
    public void ForXmlHooks_ShouldRenderClause()
    {
        Dialect.SupportsForXml.Should().BeTrue();
        Dialect.MakeForXml(new ForXmlClause(ForXmlMode.Path)).Should().Be("for xml path");
        Dialect.MakeForXml(new ForXmlClause(ForXmlMode.Raw, "row", "root", true))
            .Should().Be("for xml raw('row'), root('root'), elements");
        Dialect.MakeForXml(new ForXmlClause(ForXmlMode.Auto, Root: "r")).Should().Be("for xml auto, root('r')");
    }

    [Fact]
    public void TableHintHooks_ShouldUseWithClause()
    {
        Dialect.SupportsTableHints.Should().BeTrue();
        Dialect.MakeTableHints(["nolock"]).Should().Be(" with (nolock)");
        Dialect.MakeTableHints(["nolock", "index(ix)"]).Should().Be(" with (nolock, index(ix))");
    }

    [Fact]
    public void LockingHooks_ShouldUseTableHints()
    {
        Dialect.Lock.Should().NotBeNull();
        Dialect.Lock!.UsesTableHints.Should().BeTrue();
        Dialect.Lock!.Render(LockMode.Update).Should().Be("updlock");
        Dialect.Lock!.Render(LockMode.Share).Should().Be("holdlock");
    }

    [Fact]
    public void QueryHintHooks_ShouldUseOptionClause()
    {
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.RenderQueryHints("select 1", ["recompile"], null).Should().Be("select 1 option (recompile)");
        Dialect.RenderQueryHints("select 1", ["recompile"], "option (maxrecursion 100)")
            .Should().Be("select 1 option (maxrecursion 100, recompile)");
    }

    [Fact]
    public void GreatestLeast_ShouldUseStandardSyntax()
    {
        Dialect.SupportsGreatestLeast.Should().BeTrue();
        Dialect.MakeGreatest(["a", "b"]).Should().Be("greatest(a, b)");
        Dialect.MakeLeast(["a", "b"]).Should().Be("least(a, b)");
    }

    [Fact]
    public void DateTrunc_ShouldMapPluralPartsAndRejectUnsupportedOnes()
    {
        Dialect.SupportsDateTrunc.Should().BeTrue();
        Dialect.MakeDateTrunc("month", "d").Should().Be("datetrunc(month, d)");
        Dialect.MakeDateTrunc("milliseconds", "d").Should().Be("datetrunc(millisecond, d)");
        Dialect.MakeDateTrunc("microseconds", "d").Should().Be("datetrunc(microsecond, d)");

        var act = () => Dialect.MakeDateTrunc("century", "d");
        act.Should().Throw<NotSupportedException>().WithMessage("*century*");
    }

    [Fact]
    public void StringAgg_ShouldBeEnabledWithoutArrayAgg()
    {
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsArrayAgg.Should().BeFalse();
        Dialect.MakeStringAgg("x", "','").Should().Be("string_agg(x, ',')");
    }

    [Fact]
    public void GroupingModifiers_ShouldUseAnsiForm()
    {
        Dialect.SupportsRollup.Should().BeTrue();
        Dialect.SupportsCube.Should().BeTrue();
        Dialect.MakeGrouping("a, b", GroupingType.Rollup).Should().Be("rollup (a, b)");
        Dialect.MakeGrouping("a, b", GroupingType.Cube).Should().Be("cube (a, b)");
        Dialect.MakeGrouping("a, b", GroupingType.None).Should().Be("a, b");
        Dialect.SupportsGroupingSets.Should().BeTrue();
        Dialect.MakeGroupingSets(["(a, b)", "(a)", "()"]).Should().Be("grouping sets ((a, b), (a), ())");
    }

    [Fact]
    public void DateArithmetic_ShouldUseDateaddAndEomonth()
    {
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.MakeDateAdd("day", "n", "d").Should().Be("dateadd(day, n, d)");
        Dialect.MakeDateAdd("milliseconds", "n", "d").Should().Be("dateadd(millisecond, n, d)");
        Dialect.MakeDateAdd("century", "n", "d").Should().Be("dateadd(year, (n) * 100, d)");
        Dialect.MakeDateDiff("day", "a", "b").Should().Be("datediff(day, a, b)");
        Dialect.MakeDateDiff("milliseconds", "a", "b").Should().Be("datediff(millisecond, a, b)");
        Dialect.MakeEndOfMonth("d").Should().Be("eomonth(d)");
        Dialect.MakeDateFromParts("y", "m", "d").Should().Be("datefromparts(y, m, d)");
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchSqlServer()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeFalse();
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.SupportsGreatestLeast.Should().BeTrue();
        Dialect.SupportsDateTrunc.Should().BeTrue();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsRollup.Should().BeTrue();
        Dialect.SupportsCube.Should().BeTrue();
        Dialect.SupportsTextJson.Should().BeTrue();
        Dialect.XmlFunctions.Should().NotBeNull();
        Dialect.SupportsFullText.Should().BeTrue();
        Dialect.SupportsTableHints.Should().BeTrue();
        Dialect.SupportsForJson.Should().BeTrue();
        Dialect.SupportsForXml.Should().BeTrue();
        Dialect.SupportsAnyValueAggregate.Should().BeFalse();
        Dialect.SupportsPercentileWindow.Should().BeTrue();
    }

    [Fact]
    public void SessionInfoHooks_ShouldUseSqlServerForms()
    {
        Dialect.SessionInfoFunctions.Should().NotBeNull();
        Dialect.SessionInfoFunctions!.Supports("session_user").Should().BeTrue();
        Dialect.SessionInfoFunctions!.Render("current_user").Should().Be("current_user");
        Dialect.SessionInfoFunctions!.Render("session_user").Should().Be("session_user");
        Dialect.SessionInfoFunctions!.Render("current_schema").Should().Be("schema_name()");
        Dialect.SessionInfoFunctions!.Render("current_database").Should().Be("db_name()");
        Dialect.SessionInfoFunctions!.Render("version").Should().Be("@@version");
    }

    [Fact]
    public void UuidHooks_ShouldUseSqlServerForms()
    {
        Dialect.UuidGenerators.Should().NotBeNull();
        Dialect.UuidGenerators!.Supports("gen_random_uuid").Should().BeTrue();
        Dialect.UuidGenerators!.Supports("uuidv7").Should().BeFalse();
        Dialect.UuidGenerators!.Render("gen_random_uuid").Should().Be("newid()");
    }
}
