using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL specific surface, second sweep: row-lock wait modes, the string/cast library, the
/// range family and the JSON builders typed as jsonb (so the operators resolve without casts).
/// </summary>
[Collection("Postgres")]
public sealed class PostgresExtendedFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void LockWaitModes_ShouldExecute()
    {
        using var transaction = ((ITransactionManager)_sut.DataProvider).BeginTransaction();

        _sut.SimpleEntity.Where(x => x.Id == 1).ForUpdate(LockWaitMode.NoWait).Select(x => x.Id).ToList().Should().Equal(1);
        _sut.SimpleEntity.Where(x => x.Id == 2).ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Id).ToList().Should().Equal(2);
        _sut.SimpleEntity.Where(x => x.Id == 3).ForShare().Select(x => x.Id).ToList().Should().Equal(3);
        _sut.SimpleEntity.Where(x => x.Id == 4).ForShare(LockWaitMode.NoWait).Select(x => x.Id).ToList().Should().Equal(4);
        _sut.SimpleEntity.Where(x => x.Id == 5).ForShare(LockWaitMode.SkipLocked).Select(x => x.Id).ToList().Should().Equal(5);
    }

    [Fact]
    public void TextSearchAndDigestLibrary_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Match = SqlFunctions.Postgres.ts_match(SqlFunctions.Postgres.to_tsvector("the quick brown fox"), SqlFunctions.Postgres.plainto_tsquery("quick fox")),
                Rank = SqlFunctions.Postgres.ts_rank(SqlFunctions.Postgres.to_tsvector("the quick brown fox"), SqlFunctions.Postgres.plainto_tsquery("quick fox")),
                RankCd = SqlFunctions.Postgres.ts_rank_cd(SqlFunctions.Postgres.to_tsvector("the quick brown fox"), SqlFunctions.Postgres.plainto_tsquery("quick fox")),
                Md5 = SqlFunctions.Postgres.md5("abc"),
                Digest = SqlFunctions.Postgres.digest("abc", "sha256"),
                Nulls = SqlFunctions.Postgres.num_nulls(1, null, 2),
                NonNulls = SqlFunctions.Postgres.num_nonnulls(1, null, 2),
                TypeOf = SqlFunctions.Postgres.pg_typeof(1),
                Setting = SqlFunctions.Postgres.current_setting("server_version"),
                Zone = SqlFunctions.Postgres.timezone("UTC", Value),
                Bin = SqlFunctions.Postgres.date_bin("1 day", Value, new DateTime(2020, 1, 1)),
                Time = SqlFunctions.Postgres.make_time(10, 11, 12.5),
                Timestamp = SqlFunctions.Postgres.make_timestamp(2023, 5, 17, 13, 45, 30),
                JustifyHours = SqlFunctions.Postgres.justify_hours(TimeSpan.FromHours(30)),
                Age = SqlFunctions.Postgres.age(Value, Value.AddDays(-10)),
                ToChar = SqlFunctions.Postgres.to_char(Value, "YYYY-MM-DD"),
                ToDate = SqlFunctions.Postgres.to_date("2023-05-17", "YYYY-MM-DD"),
                ToNumber = SqlFunctions.Postgres.to_number("1,234.5", "9,999.9"),
                ToTimestamp = SqlFunctions.Postgres.to_timestamp("2023-05-17 13:45:30", "YYYY-MM-DD HH24:MI:SS")
            })
            .First();

        r.Match.Should().BeTrue();
        r.Rank.Should().BeGreaterThan(0);
        r.RankCd.Should().BeGreaterThanOrEqualTo(0);
        r.Md5.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        r.Digest.Should().NotBeNullOrEmpty();
        r.Nulls.Should().Be(1);
        r.NonNulls.Should().Be(2);
        r.TypeOf.Should().Contain("int");
        r.Setting.Should().StartWith("17");
        r.Zone.Should().Be(Value);
        r.Bin.Should().Be(new DateTime(2023, 5, 17));
        r.Time.Should().Be(new TimeSpan(10, 11, 12).Add(TimeSpan.FromMilliseconds(500)));
        r.Timestamp.Should().Be(Value);
        r.JustifyHours.Should().Be(TimeSpan.FromDays(1) + TimeSpan.FromHours(6));
        r.Age.Should().NotBeNull();
        r.ToChar.Should().Be("2023-05-17");
        r.ToDate.Should().Be(new DateTime(2023, 5, 17));
        r.ToNumber.Should().Be(1234.5m);
        r.ToTimestamp.Should().Be(Value);
    }

    [Fact]
    public void RangeFamily_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Contains = SqlFunctions.Postgres.range_contains(SqlFunctions.Postgres.int4range(1, 5), 3),
                Overlaps = SqlFunctions.Postgres.overlaps(SqlFunctions.Postgres.int4range(1, 5), SqlFunctions.Postgres.int4range(3, 8)),
                Adjacent = SqlFunctions.Postgres.range_adjacent(SqlFunctions.Postgres.int4range(1, 5), SqlFunctions.Postgres.int4range(5, 7)),
                Lower = SqlFunctions.Postgres.lower(SqlFunctions.Postgres.int4range(1, 5)),
                Upper = SqlFunctions.Postgres.upper(SqlFunctions.Postgres.int4range(1, 5)),
                LowerInc = SqlFunctions.Postgres.lower_inc(SqlFunctions.Postgres.int4range(1, 5)),
                UpperInc = SqlFunctions.Postgres.upper_inc(SqlFunctions.Postgres.int4range(1, 5)),
                LowerInf = SqlFunctions.Postgres.lower_inf(SqlFunctions.Postgres.int4range(1, 5)),
                UpperInf = SqlFunctions.Postgres.upper_inf(SqlFunctions.Postgres.int4range(1, 5)),
                IsEmpty = SqlFunctions.Postgres.isempty(SqlFunctions.Postgres.empty_range<int>()),
                Date = SqlFunctions.Postgres.daterange(new DateOnly(2023, 5, 17), new DateOnly(2023, 5, 18))
            })
            .First();

        r.Contains.Should().BeTrue();
        r.Overlaps.Should().BeTrue();
        r.Adjacent.Should().BeTrue();
        r.Lower.Should().Be(1);
        r.Upper.Should().Be(5);
        r.LowerInc.Should().BeTrue();
        r.UpperInc.Should().BeFalse();
        r.LowerInf.Should().BeFalse();
        r.UpperInf.Should().BeFalse();
        r.IsEmpty.Should().BeTrue();
        r.Date.Should().NotBeNull();
    }
}
