using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation for the SQL Server-only T-SQL scalar functions (no database).</summary>
public class SqlServerFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            P = SqlFunctions.SqlServer.patindex("%a%", x.String),
            Q1 = SqlFunctions.SqlServer.quotename(x.String),
            Q2 = SqlFunctions.SqlServer.quotename(x.String, "["),
            S = SqlFunctions.SqlServer.soundex(x.String),
            D = SqlFunctions.SqlServer.difference(x.String, "abc"),
            E = SqlFunctions.SqlServer.string_escape(x.String, "json"),
            U = SqlFunctions.SqlServer.unicode(x.String),
            N = SqlFunctions.SqlServer.nchar(65),
            F = SqlFunctions.SqlServer.format(x.Id, "N"),
            F2 = SqlFunctions.SqlServer.format(x.Id, "N", "en-US")
        }));

        sql.Should().Contain("patindex(N'%a%', somestring)");
        sql.Should().Contain("quotename(somestring)");
        sql.Should().Contain("quotename(somestring, N'[')");
        sql.Should().Contain("soundex(somestring)");
        sql.Should().Contain("difference(somestring, N'abc')");
        sql.Should().Contain("string_escape(somestring, N'json')");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("nchar(65)");
        sql.Should().Contain("format(id, N'N')");
        sql.Should().Contain("format(id, N'N', N'en-US')");
    }

    [Fact]
    public void TrigFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            A = SqlFunctions.SqlServer.acos(0.5),
            As = SqlFunctions.SqlServer.asin(0.5),
            At = SqlFunctions.SqlServer.atan(0.5),
            A2 = SqlFunctions.SqlServer.atn2(1.0, 2.0),
            Sq = SqlFunctions.SqlServer.square((double)x.Int!)
        }));

        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("asin(0.5)");
        sql.Should().Contain("atan(0.5)");
        sql.Should().Contain("atn2(1, 2)");
        sql.Should().Contain("square(cast(nullableint as float))");
    }

    [Fact]
    public void DateFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var origin = new DateTime(2020, 1, 1);

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Name = SqlFunctions.SqlServer.datename("month", x.Datetime),
            Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime),
            BucketOrigin = SqlFunctions.SqlServer.date_bucket("week", 2, x.Datetime, origin)
        }));

        sql.Should().Contain("datename(month, dt)");
        sql.Should().Contain("date_bucket(day, 1, dt)");
        sql.Should().Contain("date_bucket(week, 2, dt,");
    }

    [Fact]
    public void BinaryAndSystemFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var data = new byte[] { 1, 2, 3 };

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            H = SqlFunctions.SqlServer.hashbytes("SHA2_256", data),
            N = SqlFunctions.SqlServer.newsequentialid()
        }));

        sql.Should().Contain("hashbytes(N'SHA2_256'");
        sql.Should().Contain("newsequentialid()");
    }

    [Fact]
    public void JsonConstructors_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            A = SqlFunctions.SqlServer.json_array("a", x.Id, "b"),
            Empty = SqlFunctions.SqlServer.json_array(),
            O = SqlFunctions.SqlServer.json_object("k", x.Id, "k2", "v2")
        }));

        sql.Should().Contain("json_array(N'a', id, N'b')");
        sql.Should().Contain("json_array()");
        sql.Should().Contain("json_object(N'k' : id, N'k2' : N'v2')");
    }

    [Fact]
    public void JsonAggregates_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.SqlServer.json_arrayagg(x.String),
            O = SqlFunctions.SqlServer.json_objectagg(x.String, x.Id)
        }));

        sql.Should().Contain("json_arrayagg(somestring)");
        sql.Should().Contain("json_objectagg(somestring : id)");
    }

    [Fact]
    public void JsonPredicates_ShouldMaterialiseBooleanValue()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            C = SqlFunctions.SqlServer.json_contains(x.String, "a", "$.x"),
            P = SqlFunctions.SqlServer.json_path_exists(x.String, "$.x")
        }));

        sql.Should().Contain("cast(case when json_contains(somestring, N'a', N'$.x') = 1 then 1 else 0 end as bit)");
        sql.Should().Contain("cast(case when json_path_exists(somestring, N'$.x') = 1 then 1 else 0 end as bit)");
    }

    [Fact]
    public void JsonPredicate_InWhere_ShouldBeAPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.json_path_exists(x.String, "$.x")).Select(x => new { x.Id }))
            .Should().Contain("where json_path_exists(somestring, N'$.x') = 1");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void DateClockOffsetAndFromParts_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Now = SqlFunctions.SqlServer.sysdatetime(),
            NowOffset = SqlFunctions.SqlServer.sysdatetimeoffset(),
            UtcNow = SqlFunctions.SqlServer.sysutcdatetime(),
            Switched = SqlFunctions.SqlServer.switchoffset(SqlFunctions.SqlServer.sysdatetimeoffset(), "-08:00"),
            SwitchedMin = SqlFunctions.SqlServer.switchoffset(SqlFunctions.SqlServer.sysdatetimeoffset(), 480),
            Offset = SqlFunctions.SqlServer.todatetimeoffset(x.Datetime, "+02:00"),
            OffsetMin = SqlFunctions.SqlServer.todatetimeoffset(x.Datetime, -120),
            Time = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7),
            Small = SqlFunctions.SqlServer.smalldatetimefromparts(2020, 1, 2, 3, 4),
            DateTimeParts = SqlFunctions.SqlServer.datetimefromparts(2020, 1, 2, 3, 4, 5, 6),
            DateTime2 = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 2, 3, 4, 5, 6, 3),
            DateTimeOffset = SqlFunctions.SqlServer.datetimeoffsetfromparts(2020, 1, 2, 3, 4, 5, 6, 7, -30, 3)
        }));

        sql.Should().Contain("sysdatetime()");
        sql.Should().Contain("sysdatetimeoffset()");
        sql.Should().Contain("sysutcdatetime()");
        sql.Should().Contain("switchoffset(sysdatetimeoffset(), N'-08:00')");
        sql.Should().Contain("switchoffset(sysdatetimeoffset(), 480)");
        sql.Should().Contain("todatetimeoffset(dt, N'+02:00')");
        sql.Should().Contain("todatetimeoffset(dt, -120)");
        sql.Should().Contain("timefromparts(1, 2, 3, 4, 7)");
        sql.Should().Contain("smalldatetimefromparts(2020, 1, 2, 3, 4)");
        sql.Should().Contain("datetimefromparts(2020, 1, 2, 3, 4, 5, 6)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 2, 3, 4, 5, 6, 3)");
        sql.Should().Contain("datetimeoffsetfromparts(2020, 1, 2, 3, 4, 5, 6, 7, -30, 3)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FromPartsPrecision_Boundaries_ShouldEmit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        const byte bytePrecision = 5;
        const short shortPrecision = 6;

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Time0 = SqlFunctions.SqlServer.timefromparts(0, 0, 0, 0, 0),
            Time7 = SqlFunctions.SqlServer.timefromparts(0, 0, 0, 0, 7),
            Dt2_0 = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 0),
            Dt2_7 = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 7),
            Dt2Byte = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, bytePrecision),
            Dto_7 = SqlFunctions.SqlServer.datetimeoffsetfromparts(2020, 1, 1, 0, 0, 0, 0, 0, 0, 7),
            DtoShort = SqlFunctions.SqlServer.datetimeoffsetfromparts(2020, 1, 1, 0, 0, 0, 0, 0, 0, shortPrecision)
        }));

        sql.Should().Contain("timefromparts(0, 0, 0, 0, 0)");
        sql.Should().Contain("timefromparts(0, 0, 0, 0, 7)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 0)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 7)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 5)");
        sql.Should().Contain("datetimeoffsetfromparts(2020, 1, 1, 0, 0, 0, 0, 0, 0, 7)");
        sql.Should().Contain("datetimeoffsetfromparts(2020, 1, 1, 0, 0, 0, 0, 0, 0, 6)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FromPartsPrecision_OutOfRangeOrNonConstant_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var precision = 3;

        var tooLarge = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 8)
        }));
        tooLarge.Should().Throw<NotSupportedException>().WithMessage("*precision*between*");

        var negative = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, -1)
        }));
        negative.Should().Throw<NotSupportedException>().WithMessage("*precision*between*");

        var nonConstant = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, precision)
        }));
        nonConstant.Should().Throw<NotSupportedException>().WithMessage("*precision*constant*");

        var nullPrecision = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, null)
        }));
        nullPrecision.Should().Throw<NotSupportedException>().WithMessage("*precision*constant*");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FromPartsPrecision_NonIntIntegralConstant_ShouldEmit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        const sbyte sbytePrecision = 2;
        const byte bytePrecision = 3;
        const short shortPrecision = 4;
        const ushort ushortPrecision = 1;

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Sbyte = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, sbytePrecision),
            Byte = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, bytePrecision),
            Short = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, shortPrecision),
            Ushort = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, ushortPrecision)
        }));

        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 2)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 3)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 4)");
        sql.Should().Contain("datetime2fromparts(2020, 1, 1, 0, 0, 0, 0, 1)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void BinaryChecksumCompressFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var data = new byte[] { 1, 2, 3 };

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            C1 = SqlFunctions.SqlServer.checksum(x.Id, x.String),
            C2 = SqlFunctions.SqlServer.checksum(x.Id),
            Bc = SqlFunctions.SqlServer.binary_checksum(x.String, x.Int),
            Cs = SqlFunctions.SqlServer.compress(x.String),
            Cb = SqlFunctions.SqlServer.compress(data),
            D = SqlFunctions.SqlServer.decompress(data)
        }));

        sql.Should().Contain("checksum(id, somestring)");
        sql.Should().Contain("checksum(id)");
        sql.Should().Contain("binary_checksum(somestring, nullableint)");
        sql.Should().Contain("compress(somestring)");
        // The byte[] overloads bind the binary value as a single parameter (`@pN`), not inline.
        sql.Should().Contain("compress(@");
        sql.Should().Contain("decompress(@");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void ChecksumEmpty_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var checksum = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.checksum() }));
        checksum.Should().Throw<NotSupportedException>().WithMessage("*checksum*at least one*");

        var binaryChecksum = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.binary_checksum() }));
        binaryChecksum.Should().Throw<NotSupportedException>().WithMessage("*binary_checksum*at least one*");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void RandAndStuff_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            R = SqlFunctions.SqlServer.rand(),
            R0 = SqlFunctions.SqlServer.rand(0),
            RNeg = SqlFunctions.SqlServer.rand(-7),
            S = SqlFunctions.SqlServer.stuff(x.String, 2, 3, "xy")
        }));

        sql.Should().Contain("rand()");
        sql.Should().Contain("rand(0)");
        sql.Should().Contain("rand(-7)");
        sql.Should().Contain("stuff(somestring, 2, 3, N'xy')");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void MetadataAFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Cl = SqlFunctions.SqlServer.col_length("t", x.String),
            Cn = SqlFunctions.SqlServer.col_name(x.Int, 1),
            Ii = SqlFunctions.SqlServer.ident_incr("t"),
            Is = SqlFunctions.SqlServer.ident_seed("t"),
            Ic = SqlFunctions.SqlServer.index_col(x.String, 1, 2),
            Od = SqlFunctions.SqlServer.object_definition(x.Int),
            O1 = SqlFunctions.SqlServer.object_id(x.String),
            O2 = SqlFunctions.SqlServer.object_id(x.String, "U"),
            On1 = SqlFunctions.SqlServer.object_name(x.Int),
            On2 = SqlFunctions.SqlServer.object_name(x.Int, 1),
            Os1 = SqlFunctions.SqlServer.object_schema_name(x.Int),
            Os2 = SqlFunctions.SqlServer.object_schema_name(x.Int, 1),
            Sd = SqlFunctions.SqlServer.stats_date(x.Int, 1)
        }));

        sql.Should().Contain("col_length(N't', somestring)");
        sql.Should().Contain("col_name(nullableint, 1)");
        sql.Should().Contain("ident_incr(N't')");
        sql.Should().Contain("ident_seed(N't')");
        sql.Should().Contain("index_col(somestring, 1, 2)");
        sql.Should().Contain("object_definition(nullableint)");
        sql.Should().Contain("object_id(somestring)");
        sql.Should().Contain("object_id(somestring, N'U')");
        sql.Should().Contain("object_name(nullableint)");
        sql.Should().Contain("object_name(nullableint, 1)");
        sql.Should().Contain("object_schema_name(nullableint)");
        sql.Should().Contain("object_schema_name(nullableint, 1)");
        sql.Should().Contain("stats_date(nullableint, 1)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void MetadataBFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Di0 = SqlFunctions.SqlServer.db_id(),
            Di1 = SqlFunctions.SqlServer.db_id("db"),
            Dn0 = SqlFunctions.SqlServer.db_name(),
            Dn1 = SqlFunctions.SqlServer.db_name(1),
            Odn = SqlFunctions.SqlServer.original_db_name(),
            Si0 = SqlFunctions.SqlServer.schema_id(),
            Si1 = SqlFunctions.SqlServer.schema_id("s"),
            Sn0 = SqlFunctions.SqlServer.schema_name(),
            Sn1 = SqlFunctions.SqlServer.schema_name(1),
            Tyd = SqlFunctions.SqlServer.type_id("t"),
            Tyn = SqlFunctions.SqlServer.type_name(1)
        }));

        sql.Should().Contain("db_id()");
        sql.Should().Contain("db_id(N'db')");
        sql.Should().Contain("db_name()");
        sql.Should().Contain("db_name(1)");
        sql.Should().Contain("original_db_name()");
        sql.Should().Contain("schema_id()");
        sql.Should().Contain("schema_id(N's')");
        sql.Should().Contain("schema_name()");
        sql.Should().Contain("schema_name(1)");
        sql.Should().Contain("type_id(N't')");
        sql.Should().Contain("type_name(1)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void MetadataCFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Fgid = SqlFunctions.SqlServer.filegroup_id("fg"),
            Fgn = SqlFunctions.SqlServer.filegroup_name(1),
            Fid = SqlFunctions.SqlServer.file_id("f"),
            Fidx = SqlFunctions.SqlServer.file_idex("f"),
            Fn = SqlFunctions.SqlServer.file_name(1)
        }));

        sql.Should().Contain("filegroup_id(N'fg')");
        sql.Should().Contain("filegroup_name(1)");
        sql.Should().Contain("file_id(N'f')");
        sql.Should().Contain("file_idex(N'f')");
        sql.Should().Contain("file_name(1)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void MetadataDFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Tz = SqlFunctions.SqlServer.current_timezone(),
            TzId = SqlFunctions.SqlServer.current_timezone_id(),
            An0 = SqlFunctions.SqlServer.getansinull(),
            An1 = SqlFunctions.SqlServer.getansinull("db"),
            Isd = SqlFunctions.SqlServer.isdate(x.String),
            Isn = SqlFunctions.SqlServer.isnumeric(x.String),
            Pn = SqlFunctions.SqlServer.parsename("a.b.c.d", 1),
            Psn = SqlFunctions.SqlServer.publishingservername()
        }));

        sql.Should().Contain("current_timezone()");
        sql.Should().Contain("current_timezone_id()");
        sql.Should().Contain("getansinull()");
        sql.Should().Contain("getansinull(N'db')");
        sql.Should().Contain("parsename(N'a.b.c.d', 1)");
        sql.Should().Contain("publishingservername()");
        // ISDATE/ISNUMERIC return SQL int, so they must not be materialised as a bit predicate.
        sql.Should().Contain("isdate(somestring)");
        sql.Should().Contain("isnumeric(somestring)");
        sql.Should().NotContain("isdate(somestring) = 1");
        sql.Should().NotContain("isnumeric(somestring) = 1");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void StrFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            S1 = SqlFunctions.SqlServer.str(1.5),
            S2 = SqlFunctions.SqlServer.str(1.5, 10),
            S3 = SqlFunctions.SqlServer.str(1.5, 10, 2)
        }));

        sql.Should().Contain("str(1.5)");
        sql.Should().Contain("str(1.5, 10)");
        sql.Should().Contain("str(1.5, 10, 2)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FormatMessageFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Text0 = SqlFunctions.SqlServer.formatmessage("plain"),
            Text1 = SqlFunctions.SqlServer.formatmessage("hello %s", x.String),
            Text2 = SqlFunctions.SqlServer.formatmessage("a %s b %d", x.String, x.Int),
            Id0 = SqlFunctions.SqlServer.formatmessage(50001),
            Id1 = SqlFunctions.SqlServer.formatmessage(50001, x.String)
        }));

        sql.Should().Contain("formatmessage(N'plain')");
        sql.Should().Contain("formatmessage(N'hello %s', somestring)");
        sql.Should().Contain("formatmessage(N'a %s b %d', somestring, nullableint)");
        sql.Should().Contain("formatmessage(50001)");
        sql.Should().Contain("formatmessage(50001, somestring)");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FormatMessageTwentyArgs_ShouldEmit()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            V = SqlFunctions.SqlServer.formatmessage(
                "m", "a1", "a2", "a3", "a4", "a5", "a6", "a7", "a8", "a9", "a10",
                "a11", "a12", "a13", "a14", "a15", "a16", "a17", "a18", "a19", "a20")
        }));

        sql.Should().Contain("formatmessage(N'm', N'a1', N'a2'");
        sql.Should().Contain("N'a20'");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void FormatMessageMoreThanTwentyArgs_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.formatmessage(
                "m", "a1", "a2", "a3", "a4", "a5", "a6", "a7", "a8", "a9", "a10",
                "a11", "a12", "a13", "a14", "a15", "a16", "a17", "a18", "a19", "a20", "a21")
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*formatmessage*at most*");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void VariadicRuntimeArray_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        object[] values = [1, "a"];

        var checksum = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.checksum(values) }));
        checksum.Should().Throw<NotSupportedException>().WithMessage("*checksum*runtime array*");

        var binaryChecksum = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.binary_checksum(values) }));
        binaryChecksum.Should().Throw<NotSupportedException>().WithMessage("*binary_checksum*runtime array*");

        var jsonArray = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.json_array(values) }));
        jsonArray.Should().Throw<NotSupportedException>().WithMessage("*json_array*runtime array*");

        var formatMessage = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.formatmessage("m", values) }));
        formatMessage.Should().Throw<NotSupportedException>().WithMessage("*formatmessage*runtime array*");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void IdentIncrAndSeed_ShouldReturnDecimal()
    {
        // T-SQL IDENT_INCR/IDENT_SEED return numeric(38,0), whose ADO.NET mapping is decimal; declaring
        // them int? would overflow for an identity increment/seed above the 32-bit range.
        typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.ident_incr))!.ReturnType
            .Should().Be(typeof(decimal?));
        typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.ident_seed))!.ReturnType
            .Should().Be(typeof(decimal?));
    }

    [Fact]
    public void Functions_ShouldBeGated()
    {
        // The SQL Server renderer reports exactly the SQL Server-only names.
        var functions = SqlServerDialect.Instance.SqlServerFunctions;
        functions.Should().NotBeNull();
        functions!.Supports("patindex").Should().BeTrue();
        functions.Supports("hashbytes").Should().BeTrue();
        functions.Supports("json_object").Should().BeTrue();
        functions.Supports("sysdatetime").Should().BeTrue();
        functions.Supports("datetime2fromparts").Should().BeTrue();
        functions.Supports("checksum").Should().BeTrue();
        functions.Supports("compress").Should().BeTrue();
        functions.Supports("rand").Should().BeTrue();
        functions.Supports("stuff").Should().BeTrue();
        functions.Supports("object_id").Should().BeTrue();
        functions.Supports("db_name").Should().BeTrue();
        functions.Supports("file_name").Should().BeTrue();
        functions.Supports("formatmessage").Should().BeTrue();
        functions.Supports("isnumeric").Should().BeTrue();
        functions.Supports("str").Should().BeTrue();
        functions.Supports("next_value_for").Should().BeTrue();
        functions.Supports("nextval").Should().BeTrue();
        functions.Supports("log10").Should().BeFalse();

        functions.Render("next_value_for", ["[s]"]).Should().Be("next value for [s]");
        functions.Render("nextval", ["[s]"]).Should().Be("next value for [s]");

        var act = () => functions.Render("unknown", ["x"]);
        act.Should().Throw<NotSupportedException>().WithMessage("*unknown*not supported*");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_ShouldEmitNextValueForForBothAliases()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var viaLongName = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("order_seq")
        }));
        viaLongName.Should().Contain("next value for [order_seq]");

        var viaAlias = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            V = SqlFunctions.SqlServer.nextval("order_seq")
        }));
        viaAlias.Should().Contain("next value for [order_seq]");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_ShouldRenderBothComponentsSeparately()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("dbo.order_seq")
        }));

        sql.Should().Contain("next value for [dbo].[order_seq]");
        sql.Should().NotContain("[dbo.order_seq]");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_ShouldEscapeClosingBracketAndNotEscapeTheIdentifier()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var spaces = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("order seq")
        }));
        spaces.Should().Contain("next value for [order seq]");

        // A raw ']' inside the schema component is doubled too, so neither component can terminate
        // its bracket-quoted identifier.
        var schemaEscaping = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("s]q.t")
        }));
        schemaEscaping.Should().Contain("next value for [s]]q].[t]");
        schemaEscaping.Should().NotContain("next value for [s]q].[t]");

        // A raw ']' is doubled, so the name text cannot terminate the bracket-quoted identifier.
        var escaping = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("x]; drop table t; --")
        }));
        escaping.Should().Contain("next value for [x]]; drop table t; --]");
        escaping.Should().NotContain("next value for [x]; drop table t; --]");

        // The nextval alias routes through the same per-component quoter.
        var aliasEscaping = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.SqlServer.nextval("x]; drop table t; --")
        }));
        aliasEscaping.Should().Contain("next value for [x]]; drop table t; --]");
        aliasEscaping.Should().NotContain("next value for [x]; drop table t; --]");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_Guards_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        const string Name128 =
            "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" +
            "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa";
        const string Name129 = Name128 + "a";
        // Concatenating two constants stays a compile-time constant, so the schema-qualified name
        // reaches the component-length guard (not the non-constant guard).
        const string Schema129 = Name129 + ".t";

        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(null!) })), "*next_value_for*constant*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(default(string)!) })), "*next_value_for*constant*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for("") })), "*next_value_for*empty*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for("a..b") })), "*next_value_for*empty component*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for("a.") })), "*next_value_for*empty component*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(".b") })), "*next_value_for*empty component*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(".") })), "*next_value_for*empty component*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for("a\u0000b") })), "*next_value_for*NUL*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for("s.a.b") })), "*next_value_for*at most two*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(Name129) })), "*next_value_for*exceed*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(Schema129) })), "*next_value_for*exceed*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.nextval(null!) })), "*nextval*constant*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.nextval("s.a.b") })), "*nextval*at most two*");

        // The 128-character boundary itself is accepted (one component, quoted).
        var atBoundary = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(Name128) }));
        atBoundary.Should().Contain("next value for [" + Name128 + "]");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_NonConstantName_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var captured = "order_seq";

        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(captured) })), "*next_value_for*constant*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.next_value_for(x.String!) })), "*next_value_for*constant*");
        AssertUnsupported(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.nextval(captured) })), "*nextval*constant*");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_ShouldNotBindNameAsParameter()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            V = SqlFunctions.SqlServer.next_value_for("order_seq")
        }));

        sql.Should().Contain("next value for [order_seq]");
        sql.Should().NotContain("@p");
    }

    [Fact]
    [Trait("Issue", "218")]
    public void SequenceFunctions_ShouldReturnLong()
    {
        typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.next_value_for))!.ReturnType
            .Should().Be(typeof(long));
        typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.nextval))!.ReturnType
            .Should().Be(typeof(long));
    }

    // --- #258: QUOTENAME(value, quote) must escape inline string literals. The second argument is a
    // --- string VALUE, so its apostrophe is doubled exactly like any other inline literal — it is not
    // --- a hand-rolled delimiter concatenation. These assertions name the exact function expression so
    // --- the malformed three-apostrophe argument can never satisfy them.

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_ApostropheLiteral_ShouldBeEscaped()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Q = SqlFunctions.SqlServer.quotename("abc", "'")
        }));

        sql.Should().Contain("quotename(N'abc', N'''')",
            "the apostrophe delimiter is an inline string value whose apostrophe is doubled, not the malformed three-apostrophe argument");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_ApostropheValue_ShouldBeEscaped()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.SqlServer.quotename("O'Brien", "'")
        }));

        sql.Should().Contain("quotename(N'O''Brien', N'''')",
            "the apostrophe inside the value is doubled, and the delimiter argument is doubled as well");
        sql.Should().NotContain("quotename('O'Brien'",
            "the raw value with a single embedded apostrophe terminates the literal early and must never be emitted");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_QuoteCharacterVariants_ShouldRenderCorrectArguments()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // Each delimiter is a one-character string VALUE: only its apostrophe (when it is one) is
        // doubled; every other character is emitted verbatim and leaves the delimiter choice to the server.
        var cases = new (string Expected, string Sql)[]
        {
            ("quotename(N'abc', N'''')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "'") }))),
            ("quotename(N'abc', N'\"')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "\"") }))),
            ("quotename(N'abc', N'[')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "[") }))),
            ("quotename(N'abc', N']')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "]") }))),
            ("quotename(N'abc', N'(')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "(") }))),
            ("quotename(N'abc', N')')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", ")") }))),
            ("quotename(N'abc', N'<')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "<") }))),
            ("quotename(N'abc', N'>')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", ">") }))),
            ("quotename(N'abc', N'`')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "`") }))),
            ("quotename(N'abc', N'{')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "{") }))),
            ("quotename(N'abc', N'}')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "}") }))),
            ("quotename(N'abc')", SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc") })))
        };

        foreach (var (expected, sql) in cases)
            sql.Should().Contain(expected);
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_NullAndInvalidArguments_ShouldPreserveNativeSemantics()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // Rendering/binding only: the server semantics of the non-delimiter forms are deferred (the
        // capability gate forwards them unchanged), so no client-side validation is invented here.
        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            NullValue = SqlFunctions.SqlServer.quotename(null, "'"),
            NullQuote = SqlFunctions.SqlServer.quotename("abc", null),
            EmptyValue = SqlFunctions.SqlServer.quotename("", "'"),
            SingleChar = SqlFunctions.SqlServer.quotename("abc", "x"),
            MultiChar = SqlFunctions.SqlServer.quotename("abc", "aa"),
            EmptyQuote = SqlFunctions.SqlServer.quotename("abc", "")
        }));

        sql.Should().Contain("quotename(null, N'''')", "a null value stays SQL null");
        sql.Should().Contain("quotename(N'abc', null)", "a null delimiter stays SQL null, not the default bracket");
        sql.Should().Contain("quotename(N'', N'''')", "an empty value is a paired-delimiter case, not null");
        sql.Should().Contain("quotename(N'abc', N'x')");
        sql.Should().Contain("quotename(N'abc', N'aa')");
        sql.Should().Contain("quotename(N'abc', N'')");
        sql.Should().NotContain("'null'", "a null argument must never become the text 'null'");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_ParameterizedArguments_ShouldPreserveBindings()
    {
        using var ctx = SqlServerTestContext.Create();
        var id = 1L;
        var value = "O'Brien";
        var quote = "'";

        QueryCommand<string?> Command() => ctx.From<IComplexEntity>()
            .Where(x => x.Id == id)
            .OrderBy(x => x.Id)
            .Select(x => SqlFunctions.SqlServer.quotename(value, quote));

        var first = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(Command(), false, true, CancellationToken.None);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstParams = first.DbCommand.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName);

        firstSql.Should().Contain("quotename(@value, @quote)");
        firstSql.Should().NotContain("O''Brien", "the value is bound, never spliced into the statement");
        firstParams["value"].Value.Should().Be("O'Brien");
        firstParams["quote"].Value.Should().Be("'");

        // The ORDER BY keeps this shape off the positional refresh recipe, so the cache hit re-renders the
        // parameter-extraction pass with ParamMode = true: a literal emitted on that pass would either
        // corrupt the command or null-reference the absent builder.
        value = "O'Brien's";
        quote = "[";
        var second = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(Command(), false, true, CancellationToken.None);
        second.Should().BeSameAs(first, "the second execution must hit the cached plan");
        Normalize(second.DbCommand.CommandText).Should().Be(firstSql, "parameter mode must not splice or re-render values");
        var secondParams = second.DbCommand.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName);
        secondParams["value"].Value.Should().Be("O'Brien's", "the bound value stays raw; escaping is a SQL-literal concern only");
        secondParams["quote"].Value.Should().Be("[");

        // A null capture binds as a null parameter, never as the text 'null'.
        string? nullValue = null;
        var nullQuote = "'";
        var nullPrepared = (DbPreparedQueryCommand<string?>)ctx.GetPreparedQueryCommand(
            ctx.From<IComplexEntity>().Where(x => x.Id == id).Select(x => SqlFunctions.SqlServer.quotename(nullValue, nullQuote)),
            false, false, CancellationToken.None);
        Normalize(nullPrepared.DbCommand.CommandText).Should().Contain("quotename(@nullValue, @nullQuote)");
        var nullParams = nullPrepared.DbCommand.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName);
        (nullParams["nullValue"].Value is null || nullParams["nullValue"].Value is DBNull)
            .Should().BeTrue("a null capture binds as a null/DBNull parameter");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_NonConstantQuoteCharacter_ShouldRemainAnExpression()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Q = SqlFunctions.SqlServer.quotename("abc", x.String)
        }));

        sql.Should().Contain("quotename(N'abc', somestring)",
            "a column delimiter is forwarded as a SQL expression, never quoted as an identifier or evaluated client-side");
        sql.Should().NotContain("quotename('abc', 'somestring')");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_InputLengthBoundaries_ShouldPreserveArguments()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        const string Value128 =
            "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" +
            "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa" + "aaaaaaaaaaaaaaaa";
        const string Value129 = Value128 + "a";

        var sql = SqlOf(ctx, e.Select(x => new
        {
            At128 = SqlFunctions.SqlServer.quotename(Value128, "'"),
            At129 = SqlFunctions.SqlServer.quotename(Value129, "'")
        }));

        sql.Should().Contain("quotename(N'" + Value128 + "', N'''')", "no client-side truncation at the 128-character boundary");
        sql.Should().Contain("quotename(N'" + Value129 + "', N'''')", "the 129-character input is forwarded unchanged; the server decides");
    }

    [Fact]
    [Trait("Issue", "258")]
    public void InlineGuidLiteral_ShouldRemainQuoted()
    {
        using var ctx = SqlServerTestContext.Create();

        // The string/Guid branch of the shared emitter is one arm: a Guid constant must keep the canonical
        // 'D' formatting and stay enclosed in single quotes after the escaping substitution.
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var body = Expression.Constant(guid, typeof(Guid));
        var projection = Expression.Lambda<Func<IComplexEntity, Guid>>(
            body, Expression.Parameter(typeof(IComplexEntity), "x"));

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>().Select(projection));

        sql.Should().Contain("'00112233-4455-6677-8899-aabbccddeeff'");
    }

    [Fact]
    public void T282_DecimalParse_ShouldUseTheFixedEnUsCaseForm()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Parsed = SqlFunctions.SqlServer.parse(x.String),
            ParsedNull = SqlFunctions.SqlServer.parse(null),
            Tried = SqlFunctions.SqlServer.try_parse(x.String),
            TriedNull = SqlFunctions.SqlServer.try_parse(null)
        }));

        sql.Should().Contain("CASE WHEN somestring IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE PARSE(somestring AS decimal(38,18) USING N'en-US') END");
        sql.Should().Contain("CASE WHEN CAST(NULL AS nvarchar(4000)) IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE PARSE(CAST(NULL AS nvarchar(4000)) AS decimal(38,18) USING N'en-US') END");
        sql.Should().Contain("CASE WHEN somestring IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE TRY_PARSE(somestring AS decimal(38,18) USING N'en-US') END");
        sql.Should().Contain("CASE WHEN CAST(NULL AS nvarchar(4000)) IS NULL THEN CAST(NULL AS decimal(38,18)) ELSE TRY_PARSE(CAST(NULL AS nvarchar(4000)) AS decimal(38,18) USING N'en-US') END");

        // The culture is a grammar literal, never a bound parameter.
        sql.Should().NotMatchRegex(@"USING\s+@\w+");
        sql.Should().NotContain("culture");
    }

    private static void AssertUnsupported(Action act, string pattern)
        => act.Should().Throw<NotSupportedException>().WithMessage(pattern);
}
