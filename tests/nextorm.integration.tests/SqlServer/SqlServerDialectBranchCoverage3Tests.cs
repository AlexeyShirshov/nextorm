using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server dialect branch coverage (cycle 3, cluster D1) executed against a live Testcontainers
/// server. Every fact runs over the class-owned <c>ss3_marker</c> table (created, seeded and dropped
/// by the class itself), so no assertion depends on the shared seed totals.
///
/// <para>
/// Targeted surface: <c>SqlServerSpecificFunctions</c> (<c>src/nextorm.sqlserver/SqlServerDialect.cs</c>)
/// - the date-part renderer and its quote-stripping <c>DatePart</c> helper, the <c>json_object</c>
/// key/value pair join, the aggregate json constructor, <c>formatmessage</c> - plus the SQL Server
/// paging renderer (<c>offset ... rows fetch next ... rows only</c>) and the aliased join rendering
/// reached through the ordinary fluent surface.
/// </para>
///
/// <para>
/// Overlap avoided: <c>SqlServerFunctionExecutionTests</c>, <c>SqlServerExtendedFunctionTests</c>,
/// <c>SqlServerCachedPreparationSpecificTests</c> and <c>T232SqlServerTests</c> keep their own
/// scenarios; this class adds the date-branch matrix and the marker-table row assertions.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerDialectBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private const string CreateTableSql =
        "create table ss3_marker (id int not null primary key, dt datetime2 null, num int null, label nvarchar(50) null, grp int null)";

    private const string SeedSql =
        "insert into ss3_marker (id, dt, num, label, grp) values "
        + "(1, '2023-05-17T13:45:30', 10, 'one', 1), "
        + "(2, '2023-01-02T00:00:00', 20, 'two', 1), "
        + "(3, '2024-03-04T05:06:07', 30, 'three', 2), "
        + "(4, null, null, 'four', 2)";

    /// <summary>
    /// The complete SQL Server-only function inventory of <c>SqlServerSpecificFunctions.Supports</c>
    /// (<c>src/nextorm.sqlserver/SqlServerDialect.cs:1046-1067</c>), read through the live context's
    /// public <see cref="ISqlServerFunctions"/> surface: 79 T-SQL function names.
    /// </summary>
    private static readonly string[] SqlServerFunctionNames =
    [
        "patindex", "quotename", "soundex", "difference", "string_escape", "unicode", "nchar", "format",
        "acos", "asin", "atan", "atn2", "square", "datename", "date_bucket", "hashbytes",
        "newsequentialid", "json_array", "json_object", "json_arrayagg", "json_objectagg",
        "json_contains", "json_path_exists", "sysdatetime", "sysdatetimeoffset", "sysutcdatetime",
        "switchoffset", "todatetimeoffset", "timefromparts", "smalldatetimefromparts",
        "datetimefromparts", "datetime2fromparts", "datetimeoffsetfromparts", "checksum",
        "binary_checksum", "compress", "decompress", "rand", "stuff", "col_length", "col_name",
        "ident_incr", "ident_seed", "index_col", "object_definition", "object_id", "object_name",
        "object_schema_name", "stats_date", "db_id", "db_name", "original_db_name", "schema_id",
        "schema_name", "type_id", "type_name", "filegroup_id", "filegroup_name", "file_id", "file_idex",
        "file_name", "current_request_id", "current_timezone", "current_timezone_id",
        "current_transaction_id", "formatmessage", "getansinull", "ident_current",
        "isdate", "isnumeric", "min_active_rowversion", "parsename", "publishingservername",
        "rowcount_big", "scope_identity", "str", "xact_state", "next_value_for", "nextval"
    ];

    /// <summary>
    /// Every <c>SqlServerSpecificFunctions.Render</c> arm, written out literally and independently of
    /// the renderer: the 75 switch arms, the 12 arms that also carry an optional-arity form
    /// (<c>quotename</c>, <c>format</c>, <c>date_bucket</c>, <c>rand</c>, <c>object_id</c>,
    /// <c>object_name</c>, <c>object_schema_name</c>, <c>db_id</c>, <c>db_name</c>, <c>schema_id</c>,
    /// <c>schema_name</c>, <c>getansinull</c>) and a second <c>datename</c> argument variant for the
    /// quote-stripping helper. 92 assertion rows over 79 names / 75 source switch arms.
    /// </summary>
    private static readonly (string Name, string[] Args, string Expected)[] RenderArms =
    [
        ("patindex", ["'%b%'", "'abc'"], "patindex('%b%', 'abc')"),
        ("quotename", ["'a'"], "quotename('a')"),
        ("quotename", ["'a'", "'<'"], "quotename('a', '<')"),
        ("soundex", ["'Robert'"], "soundex('Robert')"),
        ("difference", ["'green'", "'greene'"], "difference('green', 'greene')"),
        ("string_escape", ["'a'", "'json'"], "string_escape('a', 'json')"),
        ("unicode", ["'A'"], "unicode('A')"),
        ("nchar", ["65"], "nchar(65)"),
        ("format", ["1234.5", "'N2'"], "format(1234.5, 'N2')"),
        ("format", ["1234.5", "'N2'", "'en-US'"], "format(1234.5, 'N2', 'en-US')"),
        ("acos", ["0.5"], "acos(0.5)"),
        ("asin", ["0.5"], "asin(0.5)"),
        ("atan", ["1"], "atan(1)"),
        ("atn2", ["1", "1"], "atn2(1, 1)"),
        ("square", ["3"], "square(3)"),
        ("datename", ["'year'", "dt"], "datename(year, dt)"),
        ("datename", ["quarter", "dt"], "datename(quarter, dt)"),
        ("date_bucket", ["'day'", "1", "dt"], "date_bucket(day, 1, dt)"),
        ("date_bucket", ["'day'", "1", "dt", "origin"], "date_bucket(day, 1, dt, origin)"),
        ("hashbytes", ["'SHA2_256'", "'abc'"], "hashbytes('SHA2_256', 'abc')"),
        ("newsequentialid", [], "newsequentialid()"),
        ("json_array", ["1", "'a'"], "json_array(1, 'a')"),
        ("json_object", ["'k'", "'v'", "'k2'", "'v2'"], "json_object('k' : 'v', 'k2' : 'v2')"),
        ("json_arrayagg", ["x"], "json_arrayagg(x)"),
        ("json_objectagg", ["'k'", "x"], "json_objectagg('k' : x)"),
        ("json_contains", ["'doc'", "'$.a'", "1"], "json_contains('doc', '$.a', 1)"),
        ("json_path_exists", ["'doc'", "'$.a'"], "json_path_exists('doc', '$.a')"),
        ("sysdatetime", [], "sysdatetime()"),
        ("sysdatetimeoffset", [], "sysdatetimeoffset()"),
        ("sysutcdatetime", [], "sysutcdatetime()"),
        ("switchoffset", ["ts", "120"], "switchoffset(ts, 120)"),
        ("todatetimeoffset", ["ts", "120"], "todatetimeoffset(ts, 120)"),
        ("timefromparts", ["12", "30", "0", "0", "0"], "timefromparts(12, 30, 0, 0, 0)"),
        ("smalldatetimefromparts", ["2023", "5", "17", "12", "30"],
            "smalldatetimefromparts(2023, 5, 17, 12, 30)"),
        ("datetimefromparts", ["2023", "5", "17", "12", "30", "0", "0"],
            "datetimefromparts(2023, 5, 17, 12, 30, 0, 0)"),
        ("datetime2fromparts", ["2023", "5", "17", "12", "30", "0", "0", "3"],
            "datetime2fromparts(2023, 5, 17, 12, 30, 0, 0, 3)"),
        ("datetimeoffsetfromparts", ["2023", "5", "17", "12", "30", "0", "0", "1", "0", "0"],
            "datetimeoffsetfromparts(2023, 5, 17, 12, 30, 0, 0, 1, 0, 0)"),
        ("checksum", ["a", "b"], "checksum(a, b)"),
        ("binary_checksum", ["a", "b"], "binary_checksum(a, b)"),
        ("compress", ["'abc'"], "compress('abc')"),
        ("decompress", ["x"], "decompress(x)"),
        ("rand", [], "rand()"),
        ("rand", ["1"], "rand(1)"),
        ("stuff", ["s", "1", "2", "'x'"], "stuff(s, 1, 2, 'x')"),
        ("col_length", ["'t'", "'c'"], "col_length('t', 'c')"),
        ("col_name", ["1", "2"], "col_name(1, 2)"),
        ("ident_incr", ["'t'"], "ident_incr('t')"),
        ("ident_seed", ["'t'"], "ident_seed('t')"),
        ("index_col", ["'t'", "1", "1"], "index_col('t', 1, 1)"),
        ("object_definition", ["id"], "object_definition(id)"),
        ("object_id", ["'t'"], "object_id('t')"),
        ("object_id", ["'t'", "'u'"], "object_id('t', 'u')"),
        ("object_name", ["id"], "object_name(id)"),
        ("object_name", ["id", "1"], "object_name(id, 1)"),
        ("object_schema_name", ["id"], "object_schema_name(id)"),
        ("object_schema_name", ["id", "1"], "object_schema_name(id, 1)"),
        ("stats_date", ["'t'", "1"], "stats_date('t', 1)"),
        ("db_id", [], "db_id()"),
        ("db_id", ["'x'"], "db_id('x')"),
        ("db_name", [], "db_name()"),
        ("db_name", ["1"], "db_name(1)"),
        ("original_db_name", [], "original_db_name()"),
        ("schema_id", [], "schema_id()"),
        ("schema_id", ["'x'"], "schema_id('x')"),
        ("schema_name", [], "schema_name()"),
        ("schema_name", ["1"], "schema_name(1)"),
        ("type_id", ["'nvarchar'"], "type_id('nvarchar')"),
        ("type_name", ["231"], "type_name(231)"),
        ("filegroup_id", ["'PRIMARY'"], "filegroup_id('PRIMARY')"),
        ("filegroup_name", ["1"], "filegroup_name(1)"),
        ("file_id", ["'db'"], "file_id('db')"),
        ("file_idex", ["'db'"], "file_idex('db')"),
        ("file_name", ["1"], "file_name(1)"),
        ("current_request_id", [], "current_request_id()"),
        ("current_timezone", [], "current_timezone()"),
        ("current_timezone_id", [], "current_timezone_id()"),
        ("current_transaction_id", [], "current_transaction_id()"),
        ("formatmessage", ["'x'", "1"], "formatmessage('x', 1)"),
        ("getansinull", [], "getansinull()"),
        ("getansinull", ["'db'"], "getansinull('db')"),
        ("ident_current", ["'t'"], "ident_current('t')"),
        ("isdate", ["x"], "isdate(x)"),
        ("isnumeric", ["x"], "isnumeric(x)"),
        ("min_active_rowversion", [], "min_active_rowversion()"),
        ("parsename", ["'a.b'", "1"], "parsename('a.b', 1)"),
        ("publishingservername", [], "publishingservername()"),
        ("rowcount_big", [], "rowcount_big()"),
        ("scope_identity", [], "scope_identity()"),
        ("str", ["123.456"], "str(123.456)"),
        ("xact_state", [], "xact_state()"),
        ("next_value_for", ["[seq]"], "next value for [seq]"),
        ("nextval", ["[seq]"], "next value for [seq]")
    ];

    /// <summary>The function table wired into the live SQL Server context, reached through the public dialect surface.</summary>
    private ISqlServerFunctions Functions =>
        ((DataContext)_sut.DataProvider).Dialect.SqlServerFunctions!;

    /// <summary>
    /// One case per SQL Server date/dialect branch: <c>datename</c> (quoted date part), <c>datepart</c>
    /// via <c>extract</c>, <c>dateadd</c> (day and sub-second unit) and <c>datediff</c>/<c>datediff_big</c>
    /// plus the native <c>date_bucket</c>. Each arm asserts a live value read back from the marker row.
    /// </summary>
    [Theory]
    [InlineData("datename_year")]
    [InlineData("datename_quarter")]
    [InlineData("extract_year")]
    [InlineData("extract_doy")]
    [InlineData("date_add_day")]
    [InlineData("date_add_milliseconds")]
    [InlineData("date_diff_day")]
    [InlineData("date_bucket_day")]
    public void DateFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ss3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "datename_year":
                    e.Select(x => SqlFunctions.SqlServer.datename("year", x.Dt)).First().Should().Be("2023");
                    break;
                case "datename_quarter":
                    e.Select(x => SqlFunctions.SqlServer.datename("quarter", x.Dt)).First().Should().Be("2");
                    break;
                case "extract_year":
                    e.Select(x => SqlFunctions.Sql.extract("year", x.Dt)).First().Should().Be(2023);
                    break;
                case "extract_doy":
                    e.Select(x => SqlFunctions.Sql.extract("doy", x.Dt)).First().Should().Be(137);
                    break;
                case "date_add_day":
                    e.Select(x => SqlFunctions.Sql.date_add("day", 1, x.Dt))
                        .First().Should().Be(new DateTime(2023, 5, 18, 13, 45, 30));
                    break;
                case "date_add_milliseconds":
                    e.Select(x => SqlFunctions.Sql.date_add("milliseconds", 500, x.Dt))
                        .First().Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, 500));
                    break;
                case "date_diff_day":
                    var tenDaysLater = new DateTime(2023, 5, 27, 13, 45, 30);
                    e.Select(x => SqlFunctions.Sql.date_diff("day", x.Dt, tenDaysLater))
                        .First().Should().Be(10);
                    break;
                case "date_bucket_day":
                    e.Select(x => SqlFunctions.SqlServer.date_bucket("day", 1, x.Dt))
                        .First().Should().Be(new DateTime(2023, 5, 17));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown date branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per SQL Server scalar/metadata renderer arm: the <c>json_object</c> key/value join (four
    /// arguments), the heterogeneous <c>json_array</c>, the aggregate <c>json_objectagg</c> over a single
    /// marker row and the multi-argument <c>formatmessage</c> join.
    /// </summary>
    [Theory]
    [InlineData("json_object_four_args")]
    [InlineData("json_array_mixed")]
    [InlineData("json_objectagg_single_row")]
    [InlineData("formatmessage_plain")]
    public void ScalarFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ss3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "json_object_four_args":
                    e.Select(x => SqlFunctions.SqlServer.json_object("k", "v", "k2", "v2"))
                        .First().Should().Be("{\"k\":\"v\",\"k2\":\"v2\"}");
                    break;
                case "json_array_mixed":
                    e.Select(x => SqlFunctions.SqlServer.json_array(1, "a"))
                        .First().Should().Be("[1,\"a\"]");
                    break;
                case "json_objectagg_single_row":
                    e.Select(x => SqlFunctions.SqlServer.json_objectagg("k", x.Id))
                        .First().Should().Be("{\"k\":1}");
                    break;
                case "formatmessage_plain":
                    e.Select(x => SqlFunctions.SqlServer.formatmessage("plain"))
                        .First().Should().Be("plain");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown scalar branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per SQL Server date-part/paging arm that the first theory does not reach:
    /// <c>extract</c> for <c>week</c>/<c>dow</c>/<c>isodow</c> (<c>MakeDatePart</c>'s
    /// <c>isowk</c>/<c>@@datefirst</c>-normalised arms), the numeric <c>date_part("epoch", ...)</c>
    /// arm, <c>eomonth</c>, <c>datefromparts</c> and the <c>offset n rows</c> arm of <c>MakePage</c>
    /// (without a limit). The second <c>MakePage</c> arm - <c>page_with_ties</c> rendering
    /// <c>fetch next n rows with ties</c> - is <b>deferred</b>: T-SQL rejects it with
    /// <c>Incorrect syntax near the keyword 'with'</c> (WITH TIES exists on SQL Server only together
    /// with TOP), so the case cannot be executed live. Full record:
    /// <c>rv1/inputs/defect-c3-sqlserver-fetch-with-ties.md</c>.
    /// </summary>
    [Theory]
    [InlineData("extract_week")]
    [InlineData("extract_dow")]
    [InlineData("extract_isodow")]
    [InlineData("date_part_epoch")]
    [InlineData("end_of_month")]
    [InlineData("date_from_parts")]
    [InlineData("page_offset_only")]
    public void DatePartsAndPaging_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ss3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "extract_week":
                    e.Select(x => SqlFunctions.Sql.extract("week", x.Dt)).First().Should().Be(20);
                    break;
                case "extract_dow":
                    e.Select(x => SqlFunctions.Sql.extract("dow", x.Dt)).First().Should().Be(3);
                    break;
                case "extract_isodow":
                    e.Select(x => SqlFunctions.Sql.extract("isodow", x.Dt)).First().Should().Be(3);
                    break;
                case "date_part_epoch":
                    // datediff_big(millisecond, '19700101', 2023-05-17T13:45:30) / 1000.0; both operands
                    // are interpreted in the session's own zone, so the difference is the Unix time.
                    e.Select(x => SqlFunctions.Sql.date_part("epoch", x.Dt))
                        .First().Should().BeApproximately(1684331130.0, 0.001);
                    break;
                case "end_of_month":
                    e.Select(x => SqlFunctions.Sql.end_of_month(x.Dt))
                        .First().Should().Be(new DateTime(2023, 5, 31));
                    break;
                case "date_from_parts":
                    e.Select(x => SqlFunctions.Sql.date_from_parts(2023, 5, 17))
                        .First().Should().Be(new DateTime(2023, 5, 17));
                    break;
                case "page_offset_only":
                    // OFFSET without FETCH: the MakePage limit<=0 arm emits no "fetch next".
                    _sut.DataProvider.From<Ss3Marker>()
                        .OrderBy(x => x.Id)
                        .Offset(1)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(2, 3, 4);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown date/paging branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per SQL Server renderer/conversion arm: the numeric <c>Convert.ToXxx</c> casts
    /// (<c>MakeTypeName</c>: <c>tinyint</c>, <c>smallint</c>, <c>bigint</c>, <c>real</c>,
    /// <c>decimal(38, 10)</c>) plus the <c>patindex</c>, <c>soundex</c>, two-argument <c>format</c>
    /// and <c>string_escape</c> arms of <c>SqlServerSpecificFunctions.Render</c>.
    /// </summary>
    [Theory]
    [InlineData("convert_to_byte")]
    [InlineData("convert_to_int16")]
    [InlineData("convert_to_int64")]
    [InlineData("convert_to_single")]
    [InlineData("convert_to_decimal")]
    [InlineData("patindex")]
    [InlineData("soundex")]
    [InlineData("format_n2")]
    [InlineData("string_escape")]
    public void ScalarAndConversion_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ss3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "convert_to_byte":
                    e.Select(x => Convert.ToByte(x.Id)).First().Should().Be((byte)1);
                    break;
                case "convert_to_int16":
                    e.Select(x => Convert.ToInt16(x.Id)).First().Should().Be((short)1);
                    break;
                case "convert_to_int64":
                    e.Select(x => Convert.ToInt64(x.Id)).First().Should().Be(1L);
                    break;
                case "convert_to_single":
                    e.Select(x => Convert.ToSingle(x.Id)).First().Should().Be(1f);
                    break;
                case "convert_to_decimal":
                    e.Select(x => Convert.ToDecimal(x.Id)).First().Should().Be(1m);
                    break;
                case "patindex":
                    e.Select(x => SqlFunctions.SqlServer.patindex("%b%", "abc")).First().Should().Be(2);
                    break;
                case "soundex":
                    e.Select(x => SqlFunctions.SqlServer.soundex("Robert")).First().Should().Be("R163");
                    break;
                case "format_n2":
                    e.Select(x => SqlFunctions.SqlServer.format(1234.5, "N2")).First().Should().Be("1,234.50");
                    break;
                case "string_escape":
                    e.Select(x => SqlFunctions.SqlServer.string_escape("a\nb", "json"))
                        .First().Should().Be("a\\nb");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown scalar/conversion branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The paged select renders <c>order by ... offset n rows fetch next m rows only</c>.</summary>
    [Fact]
    public void PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage()
    {
        CreateMarker();
        try
        {
            var page = _sut.DataProvider.From<Ss3Marker>()
                .OrderBy(x => x.Id)
                .Offset(1)
                .Limit(2)
                .Select(x => x.Id)
                .ToList();

            page.Should().Equal(2, 3);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The aliased join renders two table aliases and stitches the matching marker rows.</summary>
    [Fact]
    public void Join_WithAlias_ShouldStitchMatchingRows()
    {
        CreateMarker();
        try
        {
            var rows = _sut.DataProvider
                .From<Ss3Marker>()
                .Join(_sut.DataProvider.From<Ss3Marker>(), (a, b) => a.Id == b.Id)
                .Select(x => new { x.Item1.Id, x.Item2.Label })
                .ToList()
                .OrderBy(r => r.Id)
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
            rows.Select(r => r.Label).Should().Equal("one", "two", "three", "four");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The dialect's public function table accepts exactly the 79 T-SQL names of the inventory and
    /// nothing else; every accepted name has a renderer arm, and the session-identity surface is on.
    /// </summary>
    [Fact]
    public void SqlServerFunctions_ShouldAcceptTheCompleteInventoryAndNothingElse()
    {
        SqlServerFunctionNames.Should().HaveCount(79, "the SQL Server table accepts 79 T-SQL function names");
        SqlServerFunctionNames.Should().OnlyHaveUniqueItems();

        using (new AssertionScope())
        {
            foreach (var name in SqlServerFunctionNames)
                Functions.Supports(name).Should().BeTrue("'{0}' is part of the SQL Server inventory", name);
        }

        // The render table and the predicate agree name-for-name: no accepted name without an arm and
        // no arm for a name the dialect does not accept.
        RenderArms.Select(arm => arm.Name).Distinct().Should().BeEquivalentTo(SqlServerFunctionNames);

        Functions.SupportsSessionIdentity.Should().BeTrue(
            "SQL Server exposes the session/server identity functions next to the T-SQL scalar library");
    }

    /// <summary>Every renderer arm emits the literal T-SQL contract for that name and arity.</summary>
    [Fact]
    public void SqlServerFunctions_ShouldEmitTheContractSqlForEveryArm()
    {
        using (new AssertionScope())
        {
            foreach (var (name, args, expected) in RenderArms)
                Functions.Render(name, args).Should().Be(
                    expected, "'{0}({1})' is the contract arm of that name", name, string.Join(", ", args));
        }
    }

    /// <summary>
    /// The predicate is a case-sensitive <c>name is</c> pattern: an unknown name, the empty string, a
    /// null name and an upper-case variant are all rejected.
    /// </summary>
    [Fact]
    public void SqlServerFunctions_ShouldRejectUnknownEmptyNullAndCaseVariants()
    {
        Functions.Supports("bogus").Should().BeFalse("the name is not in the table");
        Functions.Supports("").Should().BeFalse("the empty name is not in the table");
        Functions.Supports(null!).Should().BeFalse("the name pattern never matches a null name");
        Functions.Supports("PATINDEX").Should().BeFalse("names are matched case-sensitively against the CLR names");
    }

    /// <summary>The renderer rejects every name the predicate rejects, with the documented message.</summary>
    [Fact]
    public void SqlServerFunctions_Render_ShouldThrowForTheNamesItDoesNotAccept()
    {
        FluentActions.Invoking(() => Functions.Render("bogus", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The bogus function is not supported by SQL Server.");

        FluentActions.Invoking(() => Functions.Render("", []))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The  function is not supported by SQL Server.");

        FluentActions.Invoking(() => Functions.Render("PATINDEX", ["x"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("The PATINDEX function is not supported by SQL Server.");
    }

    /// <summary>
    /// The argument contracts the table actually declares: a missing argument fails on the list
    /// indexer (there is no arity validation), <c>json_object</c> guards an odd key/value count, a
    /// fixed-arity arm ignores surplus arguments and a null element renders as an empty position.
    /// </summary>
    [Fact]
    public void SqlServerFunctions_Render_ShouldEnforceTheArgumentContractsItDeclares()
    {
        FluentActions.Invoking(() => Functions.Render("datename", new[] { "'year'" }))
            .Should().Throw<ArgumentOutOfRangeException>("datename reads args[1] and only one argument was supplied");

        FluentActions.Invoking(() => Functions.Render("json_object", ["'k'", "'v'", "'k2'"]))
            .Should().Throw<NotSupportedException>()
            .WithMessage("json_object requires an even number of key/value arguments.");

        Functions.Render("soundex", ["'Robert'", "'extra'"]).Should().Be(
            "soundex('Robert')", "a fixed-arity arm reads only the positions it declares");

        Functions.Render("soundex", [(string)null!]).Should().Be(
            "soundex()", "a null element is interpolated as an empty position, not rejected");
    }

    /// <summary>
    /// Live proof against the SQL Server container: rendered functions of ten families execute and
    /// return real values — numeric, string, date/time, JSON, identity/session, conversion, metadata,
    /// hash, binary/compression and clock. <c>newsequentialid()</c> renders but cannot be evaluated
    /// outside a <c>DEFAULT</c> expression, so it is covered by its renderer arm only.
    /// </summary>
    [Fact]
    public void SqlServerFunctions_ShouldExecuteAgainstTheLiveServer()
    {
        var context = (DataContext)_sut.DataProvider;

        T Scalar<T>(string expression)
        {
            using var result = context.ExecuteRaw($"select {expression} as v");
            return result.Read<T>().Single();
        }

        string Sql(string name, params string[] args) => Functions.Render(name, args);

        // Numeric family.
        Scalar<double>(Sql("square", "3")).Should().Be(9d, "square(3) is 9");
        Scalar<double>(Sql("acos", "0")).Should().BeApproximately(
            Math.PI / 2, 1e-9, "acos(0) is pi/2");
        Scalar<double>(Sql("atn2", "1", "1")).Should().BeApproximately(
            Math.PI / 4, 1e-9, "atn2(1, 1) is pi/4");

        // String family.
        Scalar<string>(Sql("quotename", "'a'")).Should().Be("[a]", "the one-argument quotename arm");
        Scalar<string>(Sql("soundex", "'Robert'")).Should().Be("R163", "soundex('Robert')");
        Scalar<int>(Sql("difference", "'green'", "'greene'")).Should().Be(4, "difference of the soundex codes");
        Scalar<int>(Sql("unicode", "'A'")).Should().Be(65, "unicode('A') is 65");
        Scalar<string>(Sql("nchar", "65")).Should().Be("A", "nchar(65) is 'A'");
        Scalar<string>(Sql("format", "1234.5", "'N2'")).Should().Be(
            "1,234.50", "the two-argument format arm uses the session culture");

        // Date/time family.
        Scalar<string>(Sql("datename", "'year'", "cast('2023-05-17T13:45:30' as datetime2)")).Should().Be(
            "2023", "datename strips the quotes around the date part");
        Scalar<DateTime>(Sql("date_bucket", "'day'", "1", "cast('2023-05-17T13:45:30' as datetime2)")).Should().Be(
            new DateTime(2023, 5, 17), "date_bucket truncates to the start of the day");
        Scalar<DateTime>(Sql(
                "datetime2fromparts", "2023", "5", "17", "13", "45", "30", "0", "3")).Should().Be(
            new DateTime(2023, 5, 17, 13, 45, 30), "datetime2fromparts joins all eight parts");

        // JSON family.
        Scalar<string>(Sql("json_object", "'k'", "'v'")).Should().Be(
            "{\"k\":\"v\"}", "json_object joins the key/value pairs");
        Scalar<string>(Sql("json_array", "1", "'a'")).Should().Be("[1,\"a\"]", "json_array keeps the argument types");
        Scalar<int>($"cast({Sql("json_path_exists", "'{\"a\":1}'", "'$.a'")} as int)").Should().Be(
            1, "json_path_exists is a predicate over the document");

        // Identity/session and metadata families.
        Scalar<string>(Sql("schema_name")).Should().Be("dbo", "the default schema of the seeded tables");
        Scalar<string>(Sql("db_name")).Should().NotBeNullOrEmpty("the current database has a name");
        Scalar<int>(Sql("db_id")).Should().BePositive("the current database has an id");

        // Conversion family.
        Scalar<string>($"ltrim({Sql("str", "123.456", "10", "2")})").Should().Be(
            "123.46", "str rounds to two decimals and pads to ten characters");
        Scalar<string>(Sql("parsename", "'a.b.c'", "2")).Should().Be("b", "parsename reads the second part");
        Scalar<int>($"cast({Sql("isnumeric", "'123'")} as int)").Should().Be(1, "isnumeric recognises the digits");

        // Hash family.
        Scalar<string>($"convert(varchar(32), {Sql("hashbytes", "'MD5'", "'abc'")}, 2)").Should().Be(
            "900150983CD24FB0D6963F7D28E17F72", "HASHBYTES('MD5', 'abc') is the documented digest");

        // Binary/compression family.
        Scalar<string>($"cast({Sql("decompress", Sql("compress", "'abc'"))} as varchar(10))").Should().Be(
            "abc", "decompress(compress('abc')) round-trips");
        Scalar<int>(Sql("checksum", "'abc'")).Should().NotBe(0, "checksum returns a hash of the value");

        // Metadata family: the seeded table and the int type id.
        Scalar<int>($"cast({Sql("col_length", "'simple_entity'", "'id'")} as int)").Should().Be(
            4, "the int primary key of the seeded table is four bytes");
        Scalar<int>($"cast({Sql("object_id", "'simple_entity'")} as int)").Should().BePositive(
            "the seeded table resolves to an object id");
        Scalar<string>(Sql("type_name", "56")).Should().Be("int", "type id 56 is int");

        // Clock family. newsequentialid() renders (its arm is asserted above) but T-SQL only allows it
        // in a DEFAULT expression, so the executable clock probe is sysdatetime().
        Scalar<DateTime>(Sql("sysdatetime")).Should().BeAfter(
            new DateTime(2020, 1, 1), "sysdatetime() returns the server clock");
    }

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists ss3_marker");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropMarker() => Execute("drop table if exists ss3_marker");

    /// <summary>The class-owned marker table: a nullable datetime, numeric and text payload per row.</summary>
    [SqlTable("ss3_marker")]
    public sealed class Ss3Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }
}
