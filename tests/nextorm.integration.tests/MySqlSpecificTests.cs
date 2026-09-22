using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert MySQL specific behaviour, backed by a Testcontainers instance unless
/// NEXTORM_MYSQL_CONNECTION points at an existing server.
/// </summary>
public sealed class MySqlSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

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
                Ver = SqlFunctions.Sql.version()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AnyValueAggregate_ShouldReturnGroupValue()
    {
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.any_agg(x.RequiredString) })
            .First();

        value.V.Should().Be("sdf");
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
    public void StringConcat_ShouldUseConcatFunction()
    {
        // MySQL's infix || is a logical OR, so the provider must render concat(...).
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => x.String + "/" + x.RequiredString)
            .First();

        value.Should().Be("dadfasd/sdf");
    }

    [Fact]
    public void OrderByAscending_NullsFirst_ShouldSortData()
    {
        // MySQL sorts NULL first ascending; the row with the NULL nullableint is id 1.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void Offset_WithoutLimit_ShouldReturnRemainingRows()
    {
        var ids = _sut.SimpleEntity.Offset(8).Select(it => it.Id).ToList();

        ids.Should().Equal(9, 10);
    }

    [Fact]
    public void QueryHint_ShouldEmitOptimizerHintAndReturnRows()
    {
        var ids = _sut.SimpleEntity
            .Select(it => it.Id)
            .Hint("MAX_EXECUTION_TIME(1000)")
            .ToList();

        ids.Should().NotBeEmpty();
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
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => SqlFunctions.Sql.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void DateProjection_ShouldMaterialiseColumns()
    {
        // Exercises the row mapper (not the scalar path): the wider/native provider types have to
        // land in the DateTime?/int? projection members.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = SqlFunctions.Sql.date_add("day", 1, x.Datetime),
                Days = SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)),
                Last = SqlFunctions.Sql.end_of_month(x.Datetime)
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Days.Should().Be(3);
        r.Last.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void JsonValue_ShouldExtractScalar()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_value("{\"id\": 7}", "$.id"))
            .First();

        r.Should().Be("7");
    }

    [Fact]
    public void JsonQuery_ShouldReturnFragment()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_query("{\"name\": \"bob\"}", "$.name"))
            .First();

        r.Should().Contain("bob");
    }

    [Fact]
    public void JsonModify_ShouldReplaceValue()
    {
        var r = _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.json_modify("{\"name\": \"a\"}", "$.name", "b"))
            .First();

        r.Should().Contain("b");
    }

    [Fact]
    public void IsJson_ShouldDetectValidJson()
    {
        _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.isjson("{\"id\": 1}"))
            .First()
            .Should().BeTrue();

        _sut.SimpleEntity
            .Select(_ => SqlFunctions.SqlServer.isjson("not json"))
            .First()
            .Should().BeFalse();
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
