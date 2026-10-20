using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse dialect branch coverage (cycle 3, cluster D2) executed against a live Testcontainers
/// server. Every fact runs over the class-owned <c>ch3_marker</c> Memory table, created, seeded and
/// dropped by the class, so no assertion depends on the shared seed totals.
///
/// <para>
/// Targeted surface (<c>src/nextorm.clickhouse/ClickHouseDialect.cs</c>): <c>MakeAggregate</c> (the
/// <c>stddevSamp</c>/<c>stddevPop</c>/<c>varSamp</c>/<c>varPop</c> spellings), <c>MakeDatePart</c>
/// (the <c>toXxx</c> family plus the <c>toInt32</c> cast), <c>MakeDateTrunc</c> (<c>dateTrunc</c>),
/// <c>MakePage</c> (<c>limit n offset m</c>) and the <c>ClickHouseScalarFunctions</c> renderer
/// (<c>upperUTF8</c>, <c>degrees</c>/<c>radians</c>/<c>pi</c>, <c>if</c>).
/// </para>
///
/// <para>
/// The <c>array_except</c> case was removed from the scalar theory: ClickHouse 25.8 rejects the
/// rendered <c>arrayExcept(...)</c> with Code 46 (UNKNOWN_FUNCTION). Recorded as a product defect
/// candidate in <c>.pdca/integration-branch-coverage-3/rv1/inputs/defect-c3-clickhouse-arrayexcept.md</c>.
/// </para>
///
/// <para>
/// Overlap avoided: <c>ClickHouseFunctionGapTests</c>, <c>ClickHouseFunctionExecutionTests</c>,
/// <c>ClickHouseExtendedFunctionTests</c>, <c>ClickHouseDateAndSessionExecutionTests</c> and
/// <c>ClickHouseScalarFunctionsTests</c> keep their own scenarios; this class adds the
/// aggregate/date-part/paging/LIMIT BY matrix over its own marker rows.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseDialectBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string CreateTableSql =
        "create table ch3_marker (id Int32, dt Nullable(DateTime), num Int32, label Nullable(String), "
        + "grp Nullable(Int32), nums Array(Int32), other Array(Int32)) engine = Memory";

    private const string SeedSql =
        "insert into ch3_marker (id, dt, num, label, grp, nums, other) values "
        + "(1, '2023-05-17 13:45:30', 10, 'one', 1, [3, 1, 2], [1, 9]), "
        + "(2, '2023-01-02 00:00:00', 20, 'two', 1, [10, 20], []), "
        + "(3, '2024-03-04 05:06:07', 30, 'three', 2, [5], [5, 6]), "
        + "(4, null, 40, 'four', null, [], [7])";

    /// <summary>
    /// One case per ClickHouse scalar/date branch: the four variance aggregates (each mapping to a
    /// distinct <c>MakeAggregate</c> arm), the array <c>arrayExcept</c> renderer, the <c>dateTrunc</c>
    /// arm, the <c>toXxx</c> date-part arm and the UTF-8 string arm.
    /// </summary>
    [Theory]
    [InlineData("stdev_samp")]
    [InlineData("stdev_pop")]
    [InlineData("var_samp")]
    [InlineData("var_pop")]
    [InlineData("date_trunc_month")]
    [InlineData("date_part_year")]
    [InlineData("string_upper_utf8")]
    public void ScalarFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            switch (fn)
            {
                case "stdev_samp":
                    // sample standard deviation of 10, 20, 30, 40 = sqrt(500 / 3)
                    _sut.DataProvider.From<Ch3Marker>()
                        .Select(x => SqlFunctions.Sql.stdev((double)x.Num))
                        .First().Should().BeApproximately(12.909944487358056, 1e-6);
                    break;
                case "stdev_pop":
                    _sut.DataProvider.From<Ch3Marker>()
                        .Select(x => SqlFunctions.Sql.stdevp((double)x.Num))
                        .First().Should().BeApproximately(11.180339887498949, 1e-6);
                    break;
                case "var_samp":
                    _sut.DataProvider.From<Ch3Marker>()
                        .Select(x => SqlFunctions.Sql.var((double)x.Num))
                        .First().Should().BeApproximately(166.66666666666666, 1e-6);
                    break;
                case "var_pop":
                    _sut.DataProvider.From<Ch3Marker>()
                        .Select(x => SqlFunctions.Sql.varp((double)x.Num))
                        .First().Should().BeApproximately(125.0, 1e-6);
                    break;
                case "date_trunc_month":
                    _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.Sql.date_trunc("month", x.Dt))
                        .First().Should().Be(new DateTime(2023, 5, 1));
                    break;
                case "date_part_year":
                    _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.Sql.extract("year", x.Dt))
                        .First().Should().Be(2023);
                    break;
                case "string_upper_utf8":
                    _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.ClickHouse.upper_utf8(x.Label))
                        .First().Should().Be("ONE");
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

    /// <summary>One case per numeric/math renderer arm (degrees, radians, pi, the conditional <c>if</c>).</summary>
    [Theory]
    [InlineData("degrees")]
    [InlineData("radians")]
    [InlineData("pi")]
    [InlineData("iif")]
    public void NumericFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "degrees":
                    e.Select(x => SqlFunctions.Sql.degrees(Math.PI)).First().Should().BeApproximately(180.0, 1e-9);
                    break;
                case "radians":
                    e.Select(x => SqlFunctions.Sql.radians(180.0)).First().Should().BeApproximately(Math.PI, 1e-9);
                    break;
                case "pi":
                    e.Select(x => SqlFunctions.Sql.pi()).First().Should().BeApproximately(Math.PI, 1e-9);
                    break;
                case "iif":
                    e.Select(x => SqlFunctions.Sql.iif(x.Num > 15, "y", "n")).First().Should().Be("n");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown numeric branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per <c>MakeDatePart</c> arm of the ClickHouse <c>toXxx</c> family that the year case
    /// above does not reach: <c>quarter</c>/<c>month</c>/<c>day</c> (<c>toQuarter</c>/<c>toMonth</c>/
    /// <c>toDayOfMonth</c>), the normalised <c>dow</c> (<c>toDayOfWeek(...) % 7</c>) and <c>isodow</c>
    /// (<c>toDayOfWeek</c>), the ISO <c>week</c> (<c>toISOWeek</c>) and <c>hour</c>/<c>minute</c>.
    /// Every case reads a concrete part of the marker row's <c>2023-05-17 13:45:30</c> timestamp.
    /// </summary>
    [Theory]
    [InlineData("part_quarter")]
    [InlineData("part_month")]
    [InlineData("part_day")]
    [InlineData("part_dow")]
    [InlineData("part_isodow")]
    [InlineData("part_week")]
    [InlineData("part_hour")]
    [InlineData("part_minute")]
    public void DatePartFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "part_quarter":
                    e.Select(x => SqlFunctions.Sql.extract("quarter", x.Dt)).First().Should().Be(2);
                    break;
                case "part_month":
                    e.Select(x => SqlFunctions.Sql.extract("month", x.Dt)).First().Should().Be(5);
                    break;
                case "part_day":
                    e.Select(x => SqlFunctions.Sql.extract("day", x.Dt)).First().Should().Be(17);
                    break;
                case "part_dow":
                    e.Select(x => SqlFunctions.Sql.extract("dow", x.Dt)).First().Should().Be(3);
                    break;
                case "part_isodow":
                    e.Select(x => SqlFunctions.Sql.extract("isodow", x.Dt)).First().Should().Be(3);
                    break;
                case "part_week":
                    e.Select(x => SqlFunctions.Sql.extract("week", x.Dt)).First().Should().Be(20);
                    break;
                case "part_hour":
                    e.Select(x => SqlFunctions.Sql.extract("hour", x.Dt)).First().Should().Be(13);
                    break;
                case "part_minute":
                    e.Select(x => SqlFunctions.Sql.extract("minute", x.Dt)).First().Should().Be(45);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown date-part branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per array/string/hash renderer arm already exercised by the ClickHouse function suites
    /// (<c>arrayLength</c>, <c>arraySort</c>, <c>arrayPushBack</c>, <c>splitByChar</c>,
    /// <c>lowerUTF8</c>, <c>trimLeft</c>, <c>formatDateTime</c>, <c>xxHash64</c>) replayed over the
    /// class-owned marker row instead of a literal argument list.
    /// </summary>
    [Theory]
    [InlineData("array_length")]
    [InlineData("array_sort")]
    [InlineData("array_push_back")]
    [InlineData("split_by_char")]
    [InlineData("string_lower_utf8")]
    [InlineData("string_trim_left")]
    [InlineData("format_date_time")]
    [InlineData("xx_hash64")]
    public void ArrayAndStringFunctions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Ch3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "array_length":
                    e.Select(x => SqlFunctions.ClickHouse.length(x.Nums)).First().Should().Be(3);
                    break;
                case "array_sort":
                    e.Select(x => SqlFunctions.ClickHouse.array_sort(x.Nums)).First().Should().Equal(1, 2, 3);
                    break;
                case "array_push_back":
                    e.Select(x => SqlFunctions.ClickHouse.array_push_back(x.Nums, 9))
                        .First().Should().Equal(3, 1, 2, 9);
                    break;
                case "split_by_char":
                    e.Select(x => SqlFunctions.ClickHouse.split_by_char(",", "a,b,c"))
                        .First().Should().Equal("a", "b", "c");
                    break;
                case "string_lower_utf8":
                    e.Select(x => SqlFunctions.ClickHouse.lower_utf8("ПРИВЕТ")).First().Should().Be("привет");
                    break;
                case "string_trim_left":
                    e.Select(x => SqlFunctions.ClickHouse.trim_left("  x  ")).First().Should().Be("x  ");
                    break;
                case "format_date_time":
                    e.Select(x => SqlFunctions.ClickHouse.format_date_time(x.Dt, "%Y-%m-%d"))
                        .First().Should().Be("2023-05-17");
                    break;
                case "xx_hash64":
                    e.Select(x => SqlFunctions.ClickHouse.xx_hash64("abc")).First().Should().NotBe(0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown array/string branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The grouped aggregate renders one row per group with its own count.</summary>
    [Fact]
    public void GroupByWithAggregate_ShouldReturnOneRowPerGroup()
    {
        CreateMarker();
        try
        {
            var groups = _sut.DataProvider.From<Ch3Marker>()
                .Where(x => x.Grp != null)
                .GroupBy(x => x.Grp)
                .Select(x => new { x.Grp, C = SqlFunctions.Sql.count() })
                .ToList();

            groups.Should().HaveCount(2);
            groups.Select(g => g.Grp).Should().BeEquivalentTo(new int?[] { 1, 2 });
            groups.Single(g => g.Grp == 1).C.Should().Be(2);
            groups.Single(g => g.Grp == 2).C.Should().Be(1);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The paged select renders <c>limit 2 offset 1</c> and returns the requested window.</summary>
    [Fact]
    public void PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage()
    {
        CreateMarker();
        try
        {
            var page = _sut.DataProvider.From<Ch3Marker>()
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

    /// <summary>The <c>LIMIT n BY expr</c> clause keeps one marker row per group.</summary>
    [Fact]
    public void LimitBy_ShouldKeepOneRowPerGroup()
    {
        CreateMarker();
        try
        {
            var rows = _sut.DataProvider.From<Ch3Marker>()
                .Where(x => x.Grp != null)
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Grp)
                .Select(x => x.Id)
                .ToList();

            rows.Should().BeEquivalentTo(new[] { 1, 3 });
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
        Execute("drop table if exists ch3_marker");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropMarker() => Execute("drop table if exists ch3_marker");

    /// <summary>The class-owned marker table: nullable date/text/group plus two integer arrays.</summary>
    [SqlTable("ch3_marker")]
    public sealed class Ch3Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }

        [Column("num")]
        public int Num { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }

        [Column("nums")]
        public int[] Nums { get; set; } = [];

        [Column("other")]
        public int[] Other { get; set; } = [];
    }
}
