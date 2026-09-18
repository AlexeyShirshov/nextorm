using FluentAssertions;
using Microsoft.Data.Sqlite;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Tests for behaviour that is specific to the SQLite provider and therefore not part of the
/// shared suite (SQLite has no ANY/ALL subquery support and sorts NULLs first).
/// </summary>
public sealed class SqliteSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public async Task WhereAnySubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == NORM.SQL.any(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task WhereAllSubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == NORM.SQL.all(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public void OrderBy2_NullsFirst_ShouldSortData()
    {
        // SQLite orders NULLs first, so the NULL Int group (id = 1) is the leading sort key.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).OrderByDescending(it => it.Id).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
        r[1].Id.Should().Be(3);
        r[2].Id.Should().Be(2);
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.date_diff("day", x.Datetime, NORM.SQL.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => NORM.SQL.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void DateProjection_ShouldMaterialiseColumns()
    {
        // Exercises the row mapper (not the scalar path): SQLite returns datetime() as TEXT and the
        // diff as INTEGER, so both have to land in the DateTime?/int? projection members.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = NORM.SQL.date_add("day", 1, x.Datetime),
                Days = NORM.SQL.date_diff("day", x.Datetime, NORM.SQL.date_add("day", 3, x.Datetime)),
                Last = NORM.SQL.end_of_month(x.Datetime)
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Days.Should().Be(3);
        r.Last.Should().Be(new DateTime(2023, 1, 31));
    }
}
