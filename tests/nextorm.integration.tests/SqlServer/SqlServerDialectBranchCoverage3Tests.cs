using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
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
