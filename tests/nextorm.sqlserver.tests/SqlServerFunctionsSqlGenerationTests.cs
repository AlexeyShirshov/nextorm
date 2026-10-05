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

        sql.Should().Contain("patindex('%a%', somestring)");
        sql.Should().Contain("quotename(somestring)");
        sql.Should().Contain("quotename(somestring, '[')");
        sql.Should().Contain("soundex(somestring)");
        sql.Should().Contain("difference(somestring, 'abc')");
        sql.Should().Contain("string_escape(somestring, 'json')");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("nchar(65)");
        sql.Should().Contain("format(id, 'N')");
        sql.Should().Contain("format(id, 'N', 'en-US')");
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

        sql.Should().Contain("hashbytes('SHA2_256'");
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

        sql.Should().Contain("json_array('a', id, 'b')");
        sql.Should().Contain("json_array()");
        sql.Should().Contain("json_object('k' : id, 'k2' : 'v2')");
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

        sql.Should().Contain("cast(case when json_contains(somestring, 'a', '$.x') = 1 then 1 else 0 end as bit)");
        sql.Should().Contain("cast(case when json_path_exists(somestring, '$.x') = 1 then 1 else 0 end as bit)");
    }

    [Fact]
    public void JsonPredicate_InWhere_ShouldBeAPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.json_path_exists(x.String, "$.x")).Select(x => new { x.Id }))
            .Should().Contain("where json_path_exists(somestring, '$.x') = 1");
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
        sql.Should().Contain("switchoffset(sysdatetimeoffset(), '-08:00')");
        sql.Should().Contain("switchoffset(sysdatetimeoffset(), 480)");
        sql.Should().Contain("todatetimeoffset(dt, '+02:00')");
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
        sql.Should().Contain("stuff(somestring, 2, 3, 'xy')");
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

        sql.Should().Contain("col_length('t', somestring)");
        sql.Should().Contain("col_name(nullableint, 1)");
        sql.Should().Contain("ident_incr('t')");
        sql.Should().Contain("ident_seed('t')");
        sql.Should().Contain("index_col(somestring, 1, 2)");
        sql.Should().Contain("object_definition(nullableint)");
        sql.Should().Contain("object_id(somestring)");
        sql.Should().Contain("object_id(somestring, 'U')");
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
        sql.Should().Contain("db_id('db')");
        sql.Should().Contain("db_name()");
        sql.Should().Contain("db_name(1)");
        sql.Should().Contain("original_db_name()");
        sql.Should().Contain("schema_id()");
        sql.Should().Contain("schema_id('s')");
        sql.Should().Contain("schema_name()");
        sql.Should().Contain("schema_name(1)");
        sql.Should().Contain("type_id('t')");
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

        sql.Should().Contain("filegroup_id('fg')");
        sql.Should().Contain("filegroup_name(1)");
        sql.Should().Contain("file_id('f')");
        sql.Should().Contain("file_idex('f')");
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
        sql.Should().Contain("getansinull('db')");
        sql.Should().Contain("parsename('a.b.c.d', 1)");
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

        sql.Should().Contain("formatmessage('plain')");
        sql.Should().Contain("formatmessage('hello %s', somestring)");
        sql.Should().Contain("formatmessage('a %s b %d', somestring, nullableint)");
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

        sql.Should().Contain("formatmessage('m', 'a1', 'a2'");
        sql.Should().Contain("'a20'");
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
        functions.Supports("log10").Should().BeFalse();

        var act = () => functions.Render("unknown", ["x"]);
        act.Should().Throw<NotSupportedException>().WithMessage("*unknown*not supported*");
    }
}
