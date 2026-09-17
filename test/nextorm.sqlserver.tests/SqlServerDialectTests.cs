using FluentAssertions;
using nextorm.core;
using nextorm.sqlserver;

namespace nextorm.sqlserver.tests;

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
    public void CapabilityFlags_ShouldMatchSqlServer()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeFalse();
    }
}
