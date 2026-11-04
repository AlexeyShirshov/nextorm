using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class SqlServerStep2UnitCTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void CrossProviderScalarFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left2 = SqlFunctions.Sql.left("abcde", 2),
                Right2 = SqlFunctions.Sql.right("abcde", 2),
                Lpad2 = SqlFunctions.Sql.lpad("abc", 5),
                Lpad3 = SqlFunctions.Sql.lpad("abc", 5, "x"),
                Rpad2 = SqlFunctions.Sql.rpad("abc", 5),
                Rpad3 = SqlFunctions.Sql.rpad("abc", 5, "x"),
                Repeat = SqlFunctions.Sql.repeat("ab", 3),
                Reverse = SqlFunctions.Sql.reverse("abc"),
                Space = SqlFunctions.Sql.space(3),
                ConcatWs = SqlFunctions.Sql.concat_ws(",", "a", "b", "c"),
                Translate = SqlFunctions.Sql.translate("abc", "abc", "xyz"),
                Ascii = SqlFunctions.Sql.ascii("A"),
                Char = SqlFunctions.Sql.@char(65),
                BitLength = SqlFunctions.Sql.bit_length("abc"),
                OctetLength = SqlFunctions.Sql.octet_length("abc"),
                Cot = SqlFunctions.Sql.cot(1.0),
                Degrees = SqlFunctions.Sql.degrees(1.0),
                Radians = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi()
            })
            .First();

        r.Left2.Should().Be("ab");
        r.Right2.Should().Be("de");
        r.Lpad2.Should().Be("  abc");
        r.Lpad3.Should().Be("xxabc");
        r.Rpad2.Should().Be("abc  ");
        r.Rpad3.Should().Be("abcxx");
        r.Repeat.Should().Be("ababab");
        r.Reverse.Should().Be("cba");
        r.Space.Should().Be("   ");
        r.ConcatWs.Should().Be("a,b,c");
        r.Translate.Should().Be("xyz");
        r.Ascii.Should().Be(65);
        r.Char.Should().Be("A");
        r.BitLength.Should().Be(48);
        r.OctetLength.Should().Be(6);
        r.Cot.Should().BeApproximately(Math.Cos(1) / Math.Sin(1), 1e-12);
        r.Degrees.Should().BeApproximately(57.29577951308232, 1e-12);
        r.Radians.Should().BeApproximately(Math.PI, 1e-12);
        r.Pi.Should().BeApproximately(Math.PI, 1e-12);
    }

    [Fact]
    public void SqlServerSpecificStringMathDate_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Pat = SqlFunctions.SqlServer.patindex("%b%", "abc"),
                Quo1 = SqlFunctions.SqlServer.quotename("abc"),
                Quo2 = SqlFunctions.SqlServer.quotename("abc", "'"),
                Snd = SqlFunctions.SqlServer.soundex("Robert"),
                Dif = SqlFunctions.SqlServer.difference("Robert", "Rupert"),
                Esc = SqlFunctions.SqlServer.string_escape("a\nb", "json"),
                Uni = SqlFunctions.SqlServer.unicode("A"),
                Nch = SqlFunctions.SqlServer.nchar(65),
                Fmt2 = SqlFunctions.SqlServer.format(42, "D6"),
                Fmt3 = SqlFunctions.SqlServer.format(42, "D6", "en-US"),
                Aco = SqlFunctions.SqlServer.acos(0.5),
                Asi = SqlFunctions.SqlServer.asin(0.5),
                Ata = SqlFunctions.SqlServer.atan(0.5),
                Atn = SqlFunctions.SqlServer.atn2(1.0, 2.0),
                Squ = SqlFunctions.SqlServer.square(4.0),
                Name = SqlFunctions.SqlServer.datename("year", x.Datetime!.Value),
                Bucket3 = SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime!.Value),
                Hash = SqlFunctions.SqlServer.hashbytes("SHA2_256", System.Text.Encoding.UTF8.GetBytes("abc")),

                Arr = SqlFunctions.SqlServer.json_array("a", 1, "b"),
                Obj = SqlFunctions.SqlServer.json_object("k", 1),
                Exists = SqlFunctions.SqlServer.json_path_exists("{\"k\":1}", "$.k"),
                SysDt = SqlFunctions.SqlServer.sysdatetime(),
                SysUtc = SqlFunctions.SqlServer.sysutcdatetime(),
                Rand0 = SqlFunctions.SqlServer.rand(),
                Rand1 = SqlFunctions.SqlServer.rand(1),
                Stuff = SqlFunctions.SqlServer.stuff("abcdef", 2, 2, "XX"),
                IsDate = SqlFunctions.SqlServer.isdate("2023-01-01"),
                IsNum = SqlFunctions.SqlServer.isnumeric("42"),
                ParseName = SqlFunctions.SqlServer.parsename("a.b.c", 1),
                Checksum = SqlFunctions.SqlServer.checksum(1, 2, 3),
                BinChecksum = SqlFunctions.SqlServer.binary_checksum(1, 2, 3),
                Compress = SqlFunctions.SqlServer.compress("abc"),
                Str = SqlFunctions.SqlServer.str(42.5, 10, 2),
                Xact = SqlFunctions.SqlServer.xact_state()
            })
            .First();

        r.Pat.Should().Be(2);
        r.Quo1.Should().Be("[abc]");
        r.Quo2.Should().Be("'abc'");
        r.Snd.Should().Be("R163");
        r.Dif.Should().Be(4);
        r.Esc.Should().Be("a\\nb");
        r.Uni.Should().Be(65);
        r.Nch.Should().Be("A");
        r.Fmt2.Should().Be("000042");
        r.Fmt3.Should().Be("000042");
        r.Aco.Should().BeApproximately(Math.Acos(0.5), 1e-12);
        r.Asi.Should().BeApproximately(Math.Asin(0.5), 1e-12);
        r.Ata.Should().BeApproximately(Math.Atan(0.5), 1e-12);
        r.Atn.Should().BeApproximately(Math.Atan2(1.0, 2.0), 1e-12);
        r.Squ.Should().Be(16.0);
        r.Name.Should().Be("2023");
        r.Bucket3.Should().Be(new DateTime(2023, 1, 1));
        r.Hash.Should().Equal(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("abc")));

        r.Arr.Should().Be("[\"a\",1,\"b\"]");
        r.Obj.Should().Be("{\"k\":1}");
        r.Exists.Should().BeTrue();
        r.SysDt.Should().BeAfter(DateTime.MinValue);
        r.SysUtc.Should().BeAfter(DateTime.MinValue);
        r.Rand0.Should().BeInRange(0.0, 1.0);
        r.Rand1.Should().BeInRange(0.0, 1.0);
        r.Stuff.Should().Be("aXXdef");
        r.IsDate.Should().Be(1);
        r.IsNum.Should().Be(1);
        r.ParseName.Should().Be("c");
        r.Checksum.Should().NotBe(0);
        r.BinChecksum.Should().NotBe(0);
        r.Compress.Should().NotBeNull();
        r.Str.Should().Be("     42.50");
        r.Xact.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void TryCastAndConvert_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Cast = SqlFunctions.SqlServer.try_cast<int>("42"),
                Convert = SqlFunctions.SqlServer.try_convert<int>("42")
            })
            .First();

        r.Cast.Should().Be(42);
        r.Convert.Should().Be(42);
    }

    [Fact]
    public void PrecisionFromParts_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                T = SqlFunctions.SqlServer.timefromparts(10, 20, 30, 0, 3),
                D2 = SqlFunctions.SqlServer.datetime2fromparts(2023, 1, 2, 3, 4, 5, 0, 3),
                Do = SqlFunctions.SqlServer.datetimeoffsetfromparts(2023, 1, 2, 3, 4, 5, 0, 2, 0, 3)
            })
            .First();

        r.T.Should().Be(new TimeSpan(10, 20, 30));
        r.D2.Should().Be(new DateTime(2023, 1, 2, 3, 4, 5));
        r.Do.Should().NotBeNull();
    }

    [Fact]
    public void SqlServerQuotenameNull_ShouldReturnNull()
    {
        _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.quotename((string?)null))
            .FirstOrDefault()
            .Should().BeNull();
    }


    [Fact]
    public void MetadataFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                DbId = SqlFunctions.SqlServer.db_id(),
                DbName = SqlFunctions.SqlServer.db_name(),
                SchemaId = SqlFunctions.SqlServer.schema_id(),
                SchemaName = SqlFunctions.SqlServer.schema_name(),
                ObjectId = SqlFunctions.SqlServer.object_id("simple_entity"),
                ObjectName = SqlFunctions.SqlServer.object_name(SqlFunctions.SqlServer.object_id("simple_entity")),
                ColLength = SqlFunctions.SqlServer.col_length("simple_entity", "id"),
                CurrentTz = SqlFunctions.SqlServer.current_timezone(),
                XactState = SqlFunctions.SqlServer.xact_state()
            })
            .First();

        r.DbId.Should().BeGreaterThan(0);
        r.DbName.Should().NotBeNullOrEmpty();
        r.SchemaId.Should().BeGreaterThanOrEqualTo(0);
        r.SchemaName.Should().NotBeNullOrEmpty();
        r.ObjectId.Should().BeGreaterThan(0);
        r.ObjectName.Should().Be("simple_entity");
        r.ColLength.Should().BeGreaterThan(0);
        r.CurrentTz.Should().NotBeNullOrEmpty();
        r.XactState.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void RefusalGatedFunctions_ShouldThrowOnUnversionedDialect()
    {
        FluentActions.Invoking(() => _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.left_shift((byte)1, 1))
            .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*LEFT_SHIFT*major version 16*");

        FluentActions.Invoking(() => _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.bit_count((int)1))
            .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*BIT_COUNT*major version 16*");

        FluentActions.Invoking(() => _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.regexp_count("abc", "a"))
            .First())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*REGEXP_COUNT*major version 17*");
    }

    [Fact]
    public void VersionedBitFunctions_ShouldCompute()
    {
        using var ctx = new SqlServerStep2UnitCVersionedContext(SqlServerContainer.ConnectionString);
        var repo = new TestDataRepository(ctx);
        var r = repo.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ls = SqlFunctions.SqlServer.left_shift((int)1, 1),
                Rc = SqlFunctions.SqlServer.bit_count((int)7)
            })
            .First();

        r.Ls.Should().Be(2);
        r.Rc.Should().Be(3);
    }
}

internal sealed class SqlServerStep2UnitCVersionedContext(string connectionString)
    : SqlServerDataContext(new Version(17, 0), connectionString);
