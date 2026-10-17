using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Executes the PostgreSQL specific surface against a live server: the text/regexp library, the
/// array functions, the session and locking renderers and the format-string translation. The
/// shared suite covers only the portable core.
/// </summary>
[Collection("Postgres")]
public sealed class PostgresFunctionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public void StringAndTextFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                SplitPart = SqlFunctions.Postgres.split_part("a,b,c", ",", 2),
                StrPos = SqlFunctions.Postgres.strpos("abc", "b"),
                InitCap = SqlFunctions.Postgres.initcap("hello world"),
                Overlay = SqlFunctions.Postgres.overlay("abcdef", "XY", 2, 3),
                Format = SqlFunctions.Postgres.format("%s-%s", "a", "b"),
                Md5 = SqlFunctions.Postgres.md5("abc"),
                RegexpLike = SqlFunctions.Postgres.regexp_like("abc123", "[0-9]+"),
                RegexpCount = SqlFunctions.Postgres.regexp_count("a1b2", "[0-9]"),
                RegexpSubstr = SqlFunctions.Postgres.regexp_substr("abc123", "[0-9]+"),
                RegexpReplace = SqlFunctions.Postgres.regexp_replace("abc123", "[0-9]+", "x"),
                RegexpSplit = SqlFunctions.Postgres.regexp_split_to_array("a,b;c", "[,;]"),
                ConcatWs = SqlFunctions.Sql.concat_ws("-", "a", "b")
            })
            .First();

        r.SplitPart.Should().Be("b");
        r.StrPos.Should().Be(2);
        r.InitCap.Should().Be("Hello World");
        r.Overlay.Should().Be("aXYef");
        r.Format.Should().Be("a-b");
        r.Md5.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        r.RegexpLike.Should().BeTrue();
        r.RegexpCount.Should().Be(2);
        r.RegexpSubstr.Should().Be("123");
        r.RegexpReplace.Should().Be("abcx");
        r.RegexpSplit.Should().Equal("a", "b", "c");
        r.ConcatWs.Should().Be("a-b");
    }

    [Fact]
    public void MathFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Asin = SqlFunctions.Postgres.asin(0.5),
                Acos = SqlFunctions.Postgres.acos(0.5),
                Atan = SqlFunctions.Postgres.atan(1),
                Atan2 = SqlFunctions.Postgres.atan2(1, 1),
                Cbrt = SqlFunctions.Postgres.cbrt(27),
                Sinh = SqlFunctions.Postgres.sinh(0),
                Cosh = SqlFunctions.Postgres.cosh(0),
                Tanh = SqlFunctions.Postgres.tanh(0),
                Asinh = SqlFunctions.Postgres.asinh(0),
                Acosh = SqlFunctions.Postgres.acosh(1),
                Atanh = SqlFunctions.Postgres.atanh(0),
                Gcd = SqlFunctions.Postgres.gcd(12, 18),
                Lcm = SqlFunctions.Postgres.lcm(4, 6),
                Factorial = SqlFunctions.Postgres.factorial(5),
                WidthBucket = SqlFunctions.Postgres.width_bucket(5, 0, 10, 2)
            })
            .First();

        r.Asin.Should().BeApproximately(Math.Asin(0.5), 1e-9);
        r.Acos.Should().BeApproximately(Math.Acos(0.5), 1e-9);
        r.Atan.Should().BeApproximately(Math.Atan(1), 1e-9);
        r.Atan2.Should().BeApproximately(Math.Atan2(1, 1), 1e-9);
        r.Cbrt.Should().BeApproximately(3, 1e-9);
        r.Sinh.Should().Be(0);
        r.Cosh.Should().Be(1);
        r.Tanh.Should().Be(0);
        r.Asinh.Should().Be(0);
        r.Acosh.Should().Be(0);
        r.Atanh.Should().Be(0);
        r.Gcd.Should().Be(6);
        r.Lcm.Should().Be(12);
        r.Factorial.Should().Be(120);
        r.WidthBucket.Should().Be(2);
    }

    [Fact]
    public void ArrayFunctions_ShouldExecute()
    {
        var array = new[] { 3, 1, 2 };

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Cardinality = SqlFunctions.Postgres.cardinality(array),
                Length = SqlFunctions.Postgres.array_length(array, 1),
                Ndims = SqlFunctions.Postgres.array_ndims(array),
                Lower = SqlFunctions.Postgres.array_lower(array, 1),
                Upper = SqlFunctions.Postgres.array_upper(array, 1),
                Position = SqlFunctions.Postgres.array_position(array, 1),
                Contains = SqlFunctions.Postgres.array_contains(array, new[] { 1, 2 }),
                Overlaps = SqlFunctions.Postgres.array_overlaps(array, new[] { 2, 9 }),
                ContainedBy = SqlFunctions.Postgres.array_contained_by(array, new[] { 1, 2, 3, 4 }),
                ToString = SqlFunctions.Postgres.array_to_string(array, ","),
                Append = SqlFunctions.Postgres.array_append(array, 9),
                Prepend = SqlFunctions.Postgres.array_prepend(0, array),
                Cat = SqlFunctions.Postgres.array_cat(array, array),
                Remove = SqlFunctions.Postgres.array_remove(array, 1),
                Replace = SqlFunctions.Postgres.array_replace(array, 1, 9),
                Concat = SqlFunctions.Postgres.array_concat(array, new[] { 4 }),
                FromString = SqlFunctions.Postgres.string_to_array("a,b,c", ",")
            })
            .First();

        r.Cardinality.Should().Be(3);
        r.Length.Should().Be(3);
        r.Ndims.Should().Be(1);
        r.Lower.Should().Be(1);
        r.Upper.Should().Be(3);
        r.Position.Should().Be(2);
        r.Contains.Should().BeTrue();
        r.Overlaps.Should().BeTrue();
        r.ContainedBy.Should().BeTrue();
        r.ToString.Should().Be("3,1,2");
        r.Append.Should().Equal(3, 1, 2, 9);
        r.Prepend.Should().Equal(0, 3, 1, 2);
        r.Cat.Should().Equal(3, 1, 2, 3, 1, 2);
        r.Remove.Should().Equal(3, 2);
        r.Replace.Should().Equal(3, 9, 2);
        r.Concat.Should().Equal(3, 1, 2, 4);
        r.FromString.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldExecute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                SessionUser = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Database = SqlFunctions.Sql.current_database(),
                Version = SqlFunctions.Sql.version(),
                Backend = SqlFunctions.Sql.pg_backend_pid(),
                ServerAddr = SqlFunctions.Sql.inet_server_addr(),
                ClientAddr = SqlFunctions.Sql.inet_client_addr(),
                Uuid = SqlFunctions.Sql.gen_random_uuid(),
                Setting = SqlFunctions.Postgres.current_setting("server_version")
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.SessionUser.Should().NotBeNullOrEmpty();
        r.Schema.Should().Be("public");
        r.Database.Should().NotBeNullOrEmpty();
        r.Version.Should().Contain("PostgreSQL");
        r.Backend.Should().BeGreaterThan(0);
        r.ServerAddr.Should().NotBeNullOrEmpty();
        r.ClientAddr.Should().NotBeNullOrEmpty();
        r.Uuid.Should().NotBe(Guid.Empty);
        r.Setting.Should().StartWith("17");
    }

    [Fact]
    public void Locking_ShouldExecute()
    {
        using var transaction = ((ITransactionManager)_sut.DataProvider).BeginTransaction();

        _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .ForUpdate()
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(1);

        _sut.SimpleEntity
            .Where(x => x.Id == 2)
            .ForShare()
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(2);
    }

    [Fact]
    public void FormatStrings_ShouldTranslate()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fixed = 1234.5.ToString("F1"),
                Padded = 12.ToString("D4"),
                Hex = 255.ToString("X"),
                Date = Value.ToString("yyyy-MM-dd"),
                Clock = Value.ToString("HH:mm:ss"),
                ShortYear = Value.ToString("yy"),
                Separators = Value.ToString("yyyy/MM/dd HH:mm")
            })
            .First();

        r.Fixed.Should().Be("1234.5");
        r.Padded.Should().Be("0012");
        r.Hex.Should().Be("FF");
        r.Date.Should().Be("2023-05-17");
        r.Clock.Should().Be("13:45:30");
        r.ShortYear.Should().Be("23");
        r.Separators.Should().Be("2023/05/17 13:45");
    }
}
