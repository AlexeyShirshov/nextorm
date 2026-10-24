using FluentAssertions;
using NextORM.Core;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse specific surface, fourth sweep: the hash family (xxHash/cityHash/sipHash/murmurHash and
/// the digest functions), ULID generation, the argument-trimming forms, the format/parse date
/// renderers and the shared math library. Every call renders ClickHouse SQL and executes it.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseHashAndFormatTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public async Task HashFamily_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Xx32 = SqlFunctions.ClickHouse.xx_hash32("abc"),
                Xx64 = SqlFunctions.ClickHouse.xx_hash64("abc"),
                Xxh3 = SqlFunctions.ClickHouse.xxh3("abc"),
                City = SqlFunctions.ClickHouse.city_hash64("abc"),
                Sip64 = SqlFunctions.ClickHouse.sip_hash64("abc"),
                Sip128 = SqlFunctions.ClickHouse.sip_hash128("abc"),
                Murmur32 = SqlFunctions.ClickHouse.murmur_hash2_32("abc"),
                Murmur64 = SqlFunctions.ClickHouse.murmur_hash2_64("abc"),
                Murmur3 = SqlFunctions.ClickHouse.murmur_hash3_32("abc"),
                Murmur364 = SqlFunctions.ClickHouse.murmur_hash3_64("abc"),
                Murmur3128 = SqlFunctions.ClickHouse.murmur_hash3_128("abc"),
                Ulid = SqlFunctions.ClickHouse.generate_ulid(),
                Md5 = SqlFunctions.ClickHouse.md5("abc"),
                Sha1 = SqlFunctions.ClickHouse.sha1("abc"),
                Sha256 = SqlFunctions.ClickHouse.sha256("abc"),
                Sha512 = SqlFunctions.ClickHouse.sha512("abc"),
                SameXx = SqlFunctions.ClickHouse.xx_hash64("abc") == SqlFunctions.ClickHouse.xx_hash64("abc")
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseHashAndFormatTests.HashFamily_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);


        r.Xx32.Should().NotBe(0);
        r.Xx64.Should().NotBe(0);
        r.Xxh3.Should().NotBe(0);
        r.City.Should().NotBe(0);
        r.Sip64.Should().NotBe(0);
        r.Sip128.Should().NotBeNullOrEmpty();
        r.Murmur32.Should().NotBe(0);
        r.Murmur64.Should().NotBe(0);
        r.Murmur3.Should().NotBe(0);
        r.Murmur364.Should().NotBe(0);
        r.Murmur3128.Should().NotBeNullOrEmpty();
        r.Ulid.Should().HaveLength(26);
        r.Md5.Should().NotBeNullOrEmpty();
        r.Sha1.Should().NotBeNullOrEmpty();
        r.Sha256.Should().NotBeNullOrEmpty();
        r.Sha512.Should().NotBeNullOrEmpty();
        r.SameXx.Should().BeTrue();
    }

    [Fact]
    public async Task TrimAndFormatFamily_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left = SqlFunctions.ClickHouse.trim_left("xxabcxx", "x"),
                Right = SqlFunctions.ClickHouse.trim_right("xxabcxx", "x"),
                Both = SqlFunctions.ClickHouse.trim_both("xxabcxx", "x"),
                LeftDefault = SqlFunctions.ClickHouse.trim_left("  abc  "),
                RightDefault = SqlFunctions.ClickHouse.trim_right("  abc  "),
                BothDefault = SqlFunctions.ClickHouse.trim_both("  abc  "),
                Formatted = SqlFunctions.ClickHouse.format_date_time(Value, "%Y-%m-%d %H:%i:%S"),
                FormattedTz = SqlFunctions.ClickHouse.format_date_time(Value, "%Y-%m-%d", "UTC"),
                Parsed = SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-05-17 13:45:30"),
                ParsedTz = SqlFunctions.ClickHouse.parse_date_time_best_effort("2023-05-17 13:45:30", "UTC"),
                Pi = SqlFunctions.Sql.pi(),
                Degrees = SqlFunctions.Sql.degrees(SqlFunctions.Sql.pi()),
                Radians = SqlFunctions.Sql.radians(180.0),
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseHashAndFormatTests.TrimAndFormatFamily_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);


        r.Left.Should().Be("abcxx");
        r.Right.Should().Be("xxabc");
        r.Both.Should().Be("abc");
        r.LeftDefault.Should().Be("abc  ");
        r.RightDefault.Should().Be("  abc");
        r.BothDefault.Should().Be("abc");
        r.Formatted.Should().Be("2023-05-17 13:45:30");
        r.FormattedTz.Should().Be("2023-05-17");
        r.Parsed.Should().Be(Value);
        r.ParsedTz.Should().Be(Value);
        r.Pi.Should().BeApproximately(Math.PI, 1e-6);
        r.Degrees.Should().BeApproximately(180, 1e-6);
        r.Radians.Should().BeApproximately(Math.PI, 1e-6);
    }
}
