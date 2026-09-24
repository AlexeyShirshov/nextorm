using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void ToUpper_ShouldConvertToUpperCase()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => e.String!.ToUpper())
            .First();

        value.Should().Be("XXX");
    }

    [Fact]
    public void Contains_ShouldFilterBySubstring()
    {
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.Contains("df"))
            .Select(e => e.Id)
            .ToList();

        // only row 1 has "dadfasd" containing "df".
        ids.Should().Equal(1L);
    }

    [Fact]
    public void Contains_WithWildcardCharacter_ShouldMatchLiterally()
    {
        // No seeded value contains a literal '%', so the escaped pattern must match nothing.
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.Contains("%"))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void StartsWith_ShouldFilterByPrefix()
    {
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.StartsWith("x"))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }

    [Fact]
    public void Substring_ShouldReturnSubstring()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.Substring(1, 2))
            .First();

        value.Should().Be("ad");
    }

    [Fact]
    public void StringLength_ShouldReturnLength()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.Length)
            .First();

        value.Should().Be(7);
    }

    [Fact]
    public void IndexOf_ShouldReturnZeroBasedPosition()
    {
        // "dadfasd": the first 'a' is at index 1, the next one at or after index 2 is at index 4.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.IndexOf("a"))
            .First()
            .Should().Be(1);

        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.IndexOf("a", 2))
            .First()
            .Should().Be(4);
    }

    [Fact]
    public void PadLeftRight_ShouldPadValue()
    {
        // row 2 is "xxx", so both pads only appear when the target length is greater.
        _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => e.String!.PadLeft(5, '0'))
            .First()
            .Should().Be("00xxx");

        _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => e.String!.PadRight(5, '.'))
            .First()
            .Should().Be("xxx..");
    }

    [Fact]
    public void RemoveAndInsert_ShouldSpliceValue()
    {
        // "dadfasd" with three characters removed from the front, the tail removed, then a splice at 3.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.Remove(0, 3))
            .First()
            .Should().Be("fasd");

        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.Remove(4))
            .First()
            .Should().Be("dadf");

        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.Insert(3, "-"))
            .First()
            .Should().Be("dad-fasd");
    }

    [Fact]
    public void NewString_ShouldRepeatCharacter()
    {
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new string('*', 4))
            .First()
            .Should().Be("****");
    }

    [Fact]
    public void MathAbs_ShouldReturnAbsoluteValue()
    {
        var values = _sut.ComplexEntity
            .Select(e => Math.Abs(e.Id - 5))
            .ToList();

        // ids 1, 2 and 3 become 4, 3 and 2.
        values.OrderBy(x => x).Should().Equal(2L, 3L, 4L);
    }

    [Fact]
    public void MathRound_ShouldRoundValue()
    {
        var values = _sut.ComplexEntity
            .Select(e => Math.Round(e.Id / 2.0 + 0.2))
            .ToList();

        // 0.7, 1.2 and 1.7 round to 1, 1 and 2 under either rounding convention.
        values.OrderBy(x => x).Should().Equal(1.0, 1.0, 2.0);
    }

    [Fact]
    public void MathRoundWithDigits_ShouldRoundValue()
    {
        var values = _sut.ComplexEntity
            .Select(e => Math.Round(e.Id / 2.0 + 0.24, 1))
            .ToList();

        // 0.74, 1.24 and 1.74 round to 0.7, 1.2 and 1.7.
        values.OrderBy(x => x).Should().Equal(0.7, 1.2, 1.7);
    }

    [Fact]
    public void DateTimeYearMonthDay_ShouldReturnDateParts()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                e.Datetime!.Value.Year,
                e.Datetime!.Value.Month,
                e.Datetime!.Value.Day,
                e.Datetime!.Value.DayOfYear
            })
            .First();

        // the seed row is 2023-01-01.
        value.Year.Should().Be(2023);
        value.Month.Should().Be(1);
        value.Day.Should().Be(1);
        value.DayOfYear.Should().Be(1);
    }

    [Fact]
    public void DateTimeHour_ShouldReturnHourPart()
    {
        var values = _sut.ComplexEntity
            .Where(e => e.Id <= 2)
            .Select(e => new { e.Id, e.Datetime!.Value.Hour })
            .ToList();

        // row 1 is 10:00, row 2 is 00:00.
        values.Single(x => x.Id == 1).Hour.Should().Be(10);
        values.Single(x => x.Id == 2).Hour.Should().Be(0);
    }

    [Fact]
    public void Extract_ShouldReturnNormalisedDateParts()
    {
        var rows = _sut.ComplexEntity
            .Where(e => e.Datetime != null)
            .Select(e => new
            {
                e.Datetime,
                Quarter = SqlFunctions.Sql.extract("quarter", e.Datetime),
                Week = SqlFunctions.Sql.extract("week", e.Datetime),
                Dow = SqlFunctions.Sql.extract("dow", e.Datetime),
                IsoDow = SqlFunctions.Sql.extract("isodow", e.Datetime),
                Epoch = SqlFunctions.Sql.date_part("epoch", e.Datetime)
            })
            .ToList();

        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            var value = row.Datetime!.Value;
            row.Quarter.Should().Be((value.Month + 2) / 3);
            row.Week.Should().Be(System.Globalization.ISOWeek.GetWeekOfYear(value));
            row.Dow.Should().Be((int)value.DayOfWeek);
            row.IsoDow.Should().Be(value.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)value.DayOfWeek);
            row.Epoch.Should().BeApproximately(
                new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                86400.0);
        }
    }

    [Fact]
    public void NullIf_ShouldReturnNullWhenEqual()
    {
        var rows = _sut.ComplexEntity
            .Where(e => e.Id <= 2)
            .Select(e => new { e.Id, Value = SqlFunctions.Sql.nullif(e.String, "xxx") })
            .ToList();

        rows.Single(r => r.Id == 2).Value.Should().BeNull();
        rows.Single(r => r.Id == 1).Value.Should().Be("dadfasd");
    }

    [Fact]
    public void LikeFunction_ShouldMatchPattern()
    {
        var ids = _sut.ComplexEntity
            .Where(e => SqlFunctions.Sql.like(e.String, "dad%"))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L);
    }
}
