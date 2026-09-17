using FluentAssertions;

namespace nextorm.integration.tests;

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
    public void DateTimeYearMonthDay_ShouldReturnDateParts()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                e.Datetime!.Value.Year,
                e.Datetime!.Value.Month,
                e.Datetime!.Value.Day
            })
            .First();

        // the seed row is 2023-01-01.
        value.Year.Should().Be(2023);
        value.Month.Should().Be(1);
        value.Day.Should().Be(1);
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
}
