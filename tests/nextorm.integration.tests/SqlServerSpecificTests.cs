using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert Microsoft SQL Server specific behaviour, backed by a Testcontainers instance
/// unless NEXTORM_SQLSERVER_CONNECTION points at an existing server.
/// </summary>
public sealed class SqlServerSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void IifChoose_ShouldEvaluateCondition()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                I = SqlFunctions.SqlServer.iif(x.Id > 5, "big", "small"),
                C = SqlFunctions.SqlServer.choose(x.Id, "a", "b", "c")
            })
            .First();

        r.I.Should().Be("small");
        r.C.Should().Be("a");
    }

    [Fact]
    public void Choose_OutOfRange_ShouldReturnNull()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.SqlServer.choose(99, "a", "b", "c"))
            .FirstOrDefault()
            .Should().BeNull();
    }

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Session = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version(),
                U = SqlFunctions.Sql.gen_random_uuid()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().NotBeNullOrEmpty();
        r.U.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void PercentileWindow_ShouldReturnValue()
    {
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() })
            .First();

        value.V.Should().Be(1);
    }

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
            .First()
            .Should().BeApproximately(3.0276503540974917, 1e-12);
    }

    [Fact]
    public void OrderByDescending_NullsLast_ShouldSortData()
    {
        // SQL Server treats NULL as the smallest value, so DESC puts the NULL group last
        // (the opposite of PostgreSQL). The shared suite avoids depending on this.
        var r = _sut.ComplexEntity.OrderByDescending(it => it.Int).Select(it => new { it.Id }).ToList();

        r[^1].Id.Should().Be(1);
    }

    [Fact]
    public void Avg_OnIntColumn_ShouldTruncate()
    {
        // T-SQL evaluates AVG over an integer column as an integer, unlike PostgreSQL and SQLite
        // which return the fractional value. The shared suite skips its rounding assertion for
        // this provider; here the actual behaviour is pinned explicitly.
        _sut.SimpleEntity.Select(x => SqlFunctions.Sql.avg(x.Id)).First().Should().Be(5);
    }

    [Fact]
    public void StringSplit_TableFunction_ShouldReturnFragments()
    {
        var csv = "a,b,c";
        var separator = ",";

        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, separator))
            .Select(r => new { r.Value })
            .ToList()
            .Select(r => r.Value)
            .OrderBy(v => v)
            .ToArray();

        values.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void OpenJson_TableFunction_ShouldReturnEntries()
    {
        var json = """{"a":1,"b":2}""";

        var keys = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
            .Select(r => new { r.Key })
            .ToList()
            .Select(r => r.Key)
            .OrderBy(k => k)
            .ToArray();

        keys.Should().Equal("a", "b");
    }

    public interface IOpenJsonTypedRow
    {
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    private static class OpenJsonTvf
    {
        [SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
        public static IQueryable<IOpenJsonTypedRow> Typed(string json) => throw new NotSupportedException();
    }

    [Fact]
    public void OpenJson_WithTypedSchema_ShouldReturnTypedColumns()
    {
        var json = """{"name":"Ada","age":36}""";

        var row = _sut.DataProvider
            .FromTableFunction(() => OpenJsonTvf.Typed(json))
            .Select(r => new { r.Name, r.Age })
            .First();

        row.Name.Should().Be("Ada");
        row.Age.Should().Be(36);
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void Page_WithoutOrderBy_ShouldUseInjectedEmptySort()
    {
        // SQL Server rejects OFFSET/FETCH without ORDER BY, so the provider injects one; the query
        // must not fail and must return just the requested page. The injected sort is a constant,
        // so only the page size is deterministic.
        var r = _sut.SimpleEntity.Page(2, 3).Select(it => it.Id).ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyHaveUniqueItems();
        r.Should().OnlyContain(id => id >= 1 && id <= 10);
    }

    [Fact]
    public void LastIndexOf_ShouldReturnZeroBasedLastPosition()
    {
        // "dadfasd" has its last 'd' at index 6.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.LastIndexOf("d"))
            .First()
            .Should().Be(6);
    }
}
