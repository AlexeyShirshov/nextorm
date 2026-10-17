using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Core translation sweep executed against a single provider (SQLite, in-process): the CLR string,
/// math and date member surface that the shared suite only touches at its edges. Each call has to
/// render portable SQL and run, which also covers the translator's overload and rejection arms.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteTranslationSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void StringInstanceMethods_ShouldTranslateAndExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                U = x.String!.ToUpper(),
                UI = x.String!.ToUpperInvariant(),
                L = x.String!.ToLower(),
                LI = x.String!.ToLowerInvariant(),
                T = x.String!.Trim(),
                TS = x.String!.TrimStart(),
                TE = x.String!.TrimEnd(),
                Sub = x.String!.Substring(1, 1),
                Sub2 = x.String!.Substring(1),
                Rep = x.String!.Replace("x", "y"),
                Rem = x.String!.Remove(1),
                Rem2 = x.String!.Remove(1, 1),
                Ins = x.String!.Insert(1, "z"),
                Idx = x.String!.IndexOf("x"),
                Idx2 = x.String!.IndexOf("x", 1),
                IdxOrd = x.String!.IndexOf("x", StringComparison.Ordinal),
                PL = x.String!.PadLeft(5),
                PL2 = x.String!.PadLeft(5, '0'),
                PR = x.String!.PadRight(5),
                PR2 = x.String!.PadRight(5, '0'),
                C = x.String!.Contains("x"),
                SW = x.String!.StartsWith("x"),
                EW = x.String!.EndsWith("x"),
                Eq = x.String!.Equals("xxx"),
                EqOrd = x.String!.Equals("xxx", StringComparison.Ordinal),
                Len = x.String!.Length
            })
            .First();

        r.U.Should().Be("XXX");
        r.UI.Should().Be("XXX");
        r.L.Should().Be("xxx");
        r.LI.Should().Be("xxx");
        r.T.Should().Be("xxx");
        r.TS.Should().Be("xxx");
        r.TE.Should().Be("xxx");
        r.Sub.Should().Be("x");
        r.Sub2.Should().Be("xx");
        r.Rep.Should().Be("yyy");
        r.Rem.Should().Be("x");
        r.Rem2.Should().Be("xx");
        r.Ins.Should().Be("xzxx");
        r.Idx.Should().Be(0);
        r.Idx2.Should().Be(1);
        r.IdxOrd.Should().Be(0);
        r.PL.Should().Be("  xxx");
        r.PL2.Should().Be("00xxx");
        r.PR.Should().Be("xxx  ");
        r.PR2.Should().Be("xxx00");
        r.C.Should().BeTrue();
        r.SW.Should().BeTrue();
        r.EW.Should().BeTrue();
        r.Eq.Should().BeTrue();
        r.EqOrd.Should().BeTrue();
        r.Len.Should().Be(3);
    }

    [Fact]
    public void StaticStringHelpers_ShouldTranslateAndExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Empty = string.IsNullOrEmpty(x.String),
                NotEmpty = string.IsNullOrEmpty("x"),
                Eq = string.Equals(x.String, "xxx"),
                EqOrd = string.Equals(x.String, "xxx", StringComparison.Ordinal),
                CmpLt = string.Compare(x.String, "yyy", StringComparison.Ordinal) < 0,
                CmpOrd = string.CompareOrdinal(x.String, "xxx")
            })
            .First();

        r.Empty.Should().BeFalse();
        r.NotEmpty.Should().BeFalse();
        r.Eq.Should().BeTrue();
        r.EqOrd.Should().BeTrue();
        r.CmpLt.Should().BeTrue();
        r.CmpOrd.Should().Be(0);
    }

    [Fact]
    public void StringSplit_ShouldBeRejectedByProvidersWithoutASplitRenderer()
    {
        var act = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = "a,b,c".Split(',', StringSplitOptions.None) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Split*");
    }

    [Fact]
    public void CultureSensitiveCompare_ShouldBeRejected()
    {
        var act = () => _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new { V = string.Compare(x.String, "xxx") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*culture-sensitive*");
    }

    [Fact]
    public void DateTrunc_ShouldBeRejectedByProvidersWithoutTruncation()
    {
        var value = new DateTime(2023, 5, 17, 13, 45, 30);
        var act = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.date_trunc("day", value) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*date_trunc*");
    }

    [Fact]
    public void TrimWithCharacter_ShouldBeRejected()
    {
        var act = () => _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new { V = x.String!.Trim('x') })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*trims whitespace only*");
    }

    [Fact]
    public void MathFunctions_ShouldTranslateAndExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Abs = Math.Abs(-3.5),
                Ceil = Math.Ceiling(2.2),
                Floor = Math.Floor(2.8),
                Round = Math.Round(2.345, 2),
                Pow = Math.Pow(2, 10),
                Sqrt = Math.Sqrt(16),
                Exp = Math.Exp(0),
                Ln = Math.Log(Math.E),
                Log10 = Math.Log10(1000),
                Sign = Math.Sign(-42),
                Truncate = Math.Truncate(2.9),
                Min = Math.Min(3, 7),
                Max = Math.Max(3, 7),
                Sin = Math.Sin(0),
                Cos = Math.Cos(0),
                Tan = Math.Tan(0),
                Atan2 = Math.Atan2(0, 1)
            })
            .First();

        r.Abs.Should().Be(3.5);
        r.Ceil.Should().Be(3);
        r.Floor.Should().Be(2);
        r.Round.Should().Be(2.35);
        r.Pow.Should().Be(1024);
        r.Sqrt.Should().Be(4);
        r.Exp.Should().Be(1);
        r.Ln.Should().Be(1);
        r.Log10.Should().Be(3);
        r.Sign.Should().Be(-1);
        r.Truncate.Should().Be(2);
        r.Min.Should().Be(3);
        r.Max.Should().Be(7);
        r.Sin.Should().Be(0);
        r.Cos.Should().Be(1);
        r.Tan.Should().Be(0);
        r.Atan2.Should().Be(0);
    }

    [Fact]
    public void DateTimeMembers_ShouldTranslateAndExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Year = x.Datetime!.Value.Year,
                Month = x.Datetime!.Value.Month,
                Day = x.Datetime!.Value.Day,
                Hour = x.Datetime!.Value.Hour,
                Minute = x.Datetime!.Value.Minute,
                Second = x.Datetime!.Value.Second,
                Doy = x.Datetime!.Value.DayOfYear
            })
            .First();

        r.Year.Should().Be(2023);
        r.Month.Should().Be(1);
        r.Day.Should().Be(1);
        r.Hour.Should().Be(0);
        r.Minute.Should().Be(0);
        r.Second.Should().Be(0);
        r.Doy.Should().Be(1);
    }

    [Fact]
    public void DateFunctions_ShouldTranslateAndExecute()
    {
        var value = new DateTime(2023, 5, 17, 13, 45, 30);

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                AddDays = SqlFunctions.Sql.date_add("day", 3, value),
                AddMonths = SqlFunctions.Sql.date_add("month", 2, value),
                DiffDays = SqlFunctions.Sql.date_diff("day", value, value.AddDays(10)),
                ExtractYear = SqlFunctions.Sql.extract("year", value),
                PartEpoch = SqlFunctions.Sql.date_part("epoch", value),
                EndOfMonth = SqlFunctions.Sql.end_of_month(value),
                FromParts = SqlFunctions.Sql.date_from_parts(2020, 2, 29)
            })
            .First();

        r.AddDays.Should().Be(value.AddDays(3));
        r.AddMonths.Should().Be(value.AddMonths(2));
        r.DiffDays.Should().Be(10);
        r.ExtractYear.Should().Be(2023);
        r.PartEpoch.Should().BeApproximately(
            new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds(), 1);
        r.EndOfMonth.Should().Be(new DateTime(2023, 5, 31));
        r.FromParts.Should().Be(new DateTime(2020, 2, 29));
    }

    [Fact]
    public void Aggregates_ShouldTranslateAndExecute()
    {
        var rows = _sut.ComplexEntity
            .Select(x => new
            {
                Rows = SqlFunctions.Sql.count(),
                Distinct = SqlFunctions.Sql.count_distinct(x.Int)
            })
            .First();

        rows.Rows.Should().Be(3);
        rows.Distinct.Should().Be(1);
        _sut.ComplexEntity.Sum(x => x.Int).Should().Be(2);
        _sut.ComplexEntity.Min(x => x.Int).Should().Be(1);
        _sut.ComplexEntity.Max(x => x.Int).Should().Be(1);
        _sut.ComplexEntity.Count().Should().Be(3);
    }
}
