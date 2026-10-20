using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>
/// MySQL dialect branch coverage (cycle 3, cluster D4) executed against a live Testcontainers server.
/// Every fact runs over the class-owned <c>my3_marker</c> table, created, seeded and dropped by the
/// class, so no assertion depends on the shared seed totals.
///
/// <para>
/// Targeted surface: the MySQL session-identity renderer (<c>user()</c>), the MySQL paging renderer
/// (<c>limit n offset m</c>) and the aliased join rendering (<c>SqlSourceRenderer.MakeJoin</c>)
/// reached through the ordinary fluent surface.
/// </para>
///
/// <para>
/// Overlap avoided: <c>MySqlFunctionExecutionTests</c> and <c>MySqlSpecificTests</c> keep their own
/// scenarios; this class adds the marker-table user/paging/join row assertions.
/// </para>
/// </summary>
[Collection("MySql")]
public sealed class MySqlDialectBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    private const string CreateTableSql =
        "create table my3_marker (id int not null primary key, num int null, label varchar(50) null, grp int null)";

    private const string SeedSql =
        "insert into my3_marker (id, num, label, grp) values "
        + "(1, 10, 'one', 1), "
        + "(2, 20, 'two', 1), "
        + "(3, 30, 'three', 2), "
        + "(4, 40, 'four', 2)";

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    /// <summary>One case per MySQL dialect branch: the session user, the paging renderer and the aliased join.</summary>
    [Theory]
    [InlineData("session_user")]
    [InlineData("paged_select")]
    [InlineData("join_alias")]
    public void DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            switch (fn)
            {
                case "session_user":
                    _sut.DataProvider.From<My3Marker>()
                        .Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.Sql.user())
                        .First().Should().NotBeNullOrEmpty();
                    break;
                case "paged_select":
                    _sut.DataProvider.From<My3Marker>()
                        .OrderBy(x => x.Id)
                        .Offset(1)
                        .Limit(2)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(2, 3);
                    break;
                case "join_alias":
                    var rows = _sut.DataProvider
                        .From<My3Marker>()
                        .Join(_sut.DataProvider.From<My3Marker>(), (a, b) => a.Id == b.Id)
                        .Select(x => new { x.Item1.Id, x.Item2.Label })
                        .ToList()
                        .OrderBy(r => r.Id)
                        .ToList();

                    rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
                    rows.Select(r => r.Label).Should().Equal("one", "two", "three", "four");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown MySQL branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// One case per MySQL native/date/string renderer arm already exercised by
    /// <c>MySqlFunctionExecutionTests</c>: <c>find_in_set</c>, <c>elt</c>, <c>substring_index</c>,
    /// <c>md5</c>, the ISO <c>week</c> date part and the <c>date_format</c> renderer. Each case is
    /// projected over the marker row so the value comes back from the live server.
    /// </summary>
    [Theory]
    [InlineData("find_in_set")]
    [InlineData("elt")]
    [InlineData("substring_index")]
    [InlineData("md5")]
    [InlineData("extract_week")]
    [InlineData("date_format")]
    public void Functions_DialectBranches_ShouldReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "find_in_set":
                    e.Select(x => SqlFunctions.MySql.find_in_set("b", "a,b,c")).First().Should().Be(2);
                    break;
                case "elt":
                    e.Select(x => SqlFunctions.MySql.elt(2, "a", "b", "c")).First().Should().Be("b");
                    break;
                case "substring_index":
                    e.Select(x => SqlFunctions.MySql.substring_index("a,b,c", ",", 2)).First().Should().Be("a,b");
                    break;
                case "md5":
                    e.Select(x => SqlFunctions.MySql.md5("abc"))
                        .First().Should().Be("900150983cd24fb0d6963f7d28e17f72");
                    break;
                case "extract_week":
                    e.Select(x => SqlFunctions.Sql.extract("week", Value)).First().Should().Be(20);
                    break;
                case "date_format":
                    e.Select(x => SqlFunctions.MySql.date_format(Value, "%Y/%m/%d"))
                        .First().Should().Be("2023/05/17");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown MySQL function branch case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The correlated <c>CROSS APPLY</c> surface (enabled by <c>MySqlDialect.SupportsApply</c>) renders
    /// the lateral subquery form and evaluates it per outer marker row; each row matches itself, so
    /// every label comes back exactly once.
    /// </summary>
    [Fact]
    public void CrossApply_ShouldEvaluateTheCorrelatedSourcePerRow()
    {
        CreateMarker();
        try
        {
            var rows = _sut.DataProvider.From<My3Marker>()
                .CrossApply(x => _sut.DataProvider.From<My3Marker>()
                    .Where(y => y.Id == x.Id)
                    .Select(y => new { y.Label }))
                .Select(p => new { p.Item1.Id, p.Item2.Label })
                .ToList();

            rows.Should().HaveCount(4);
            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
            rows.Select(r => r.Label).Should().BeEquivalentTo(new[] { "one", "two", "three", "four" });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The full public <c>SqlFunctions.MySql</c> native table (28 names) rendered through the dialect's
    /// native renderer and evaluated by the live server, one case per name. Each case projects the call
    /// over the marker row, so the value asserted comes back from MySQL, not from the renderer alone.
    /// </summary>
    [Theory]
    [InlineData("find_in_set")]
    [InlineData("field")]
    [InlineData("elt")]
    [InlineData("substring_index")]
    [InlineData("format")]
    [InlineData("str_to_date")]
    [InlineData("date_format")]
    [InlineData("from_unixtime")]
    [InlineData("unix_timestamp")]
    [InlineData("md5")]
    [InlineData("sha1")]
    [InlineData("sha2")]
    [InlineData("inet_aton")]
    [InlineData("inet_ntoa")]
    [InlineData("json_set")]
    [InlineData("json_insert")]
    [InlineData("json_replace")]
    [InlineData("json_remove")]
    [InlineData("json_merge_patch")]
    [InlineData("json_merge_preserve")]
    [InlineData("json_array_append")]
    [InlineData("json_array_insert")]
    [InlineData("json_depth")]
    [InlineData("json_keys")]
    [InlineData("json_length")]
    [InlineData("json_type")]
    [InlineData("uuid_to_bin")]
    [InlineData("bin_to_uuid")]
    public void NativeFunctionTable_ShouldRenderAndReadLiveValues(string fn)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1);

            switch (fn)
            {
                case "find_in_set":
                    e.Select(x => SqlFunctions.MySql.find_in_set("b", "a,b,c")).First().Should().Be(2);
                    break;
                case "field":
                    e.Select(x => SqlFunctions.MySql.field("b", "a", "b", "c")).First().Should().Be(2);
                    break;
                case "elt":
                    e.Select(x => SqlFunctions.MySql.elt(2, "a", "b", "c")).First().Should().Be("b");
                    break;
                case "substring_index":
                    e.Select(x => SqlFunctions.MySql.substring_index("a,b,c", ",", 2)).First().Should().Be("a,b");
                    break;
                case "format":
                    e.Select(x => SqlFunctions.MySql.format(1234.567m, 2)).First().Should().Be("1,234.57");
                    break;
                case "str_to_date":
                    e.Select(x => SqlFunctions.MySql.str_to_date("2023-05-17", "%Y-%m-%d"))
                        .First().Should().Be(new DateTime(2023, 5, 17));
                    break;
                case "date_format":
                    e.Select(x => SqlFunctions.MySql.date_format(Value, "%Y/%m/%d"))
                        .First().Should().Be("2023/05/17");
                    break;
                case "from_unixtime":
                    e.Select(x => SqlFunctions.MySql.from_unixtime(1684324800))
                        .First().Should().Be(new DateTime(2023, 5, 17, 12, 0, 0));
                    break;
                case "unix_timestamp":
                    e.Select(x => SqlFunctions.MySql.unix_timestamp(Value)).First().Should().Be(1684331130);
                    break;
                case "md5":
                    e.Select(x => SqlFunctions.MySql.md5("abc"))
                        .First().Should().Be("900150983cd24fb0d6963f7d28e17f72");
                    break;
                case "sha1":
                    e.Select(x => SqlFunctions.MySql.sha1("abc"))
                        .First().Should().Be("a9993e364706816aba3e25717850c26c9cd0d89d");
                    break;
                case "sha2":
                    e.Select(x => SqlFunctions.MySql.sha2("abc", 256))
                        .First().Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
                    break;
                case "inet_aton":
                    e.Select(x => SqlFunctions.MySql.inet_aton("1.2.3.4")).First().Should().Be(16909060);
                    break;
                case "inet_ntoa":
                    e.Select(x => SqlFunctions.MySql.inet_ntoa(16909060)).First().Should().Be("1.2.3.4");
                    break;
                case "json_set":
                    e.Select(x => SqlFunctions.MySql.json_set("{\"a\":1}", "$.b", "2"))
                        .First().Should().Contain("\"b\": \"2\"");
                    break;
                case "json_insert":
                    e.Select(x => SqlFunctions.MySql.json_insert("{\"a\":1}", "$.b", "2"))
                        .First().Should().Contain("\"b\": \"2\"");
                    break;
                case "json_replace":
                    e.Select(x => SqlFunctions.MySql.json_replace("{\"a\":1}", "$.a", "9"))
                        .First().Should().Contain("\"a\": \"9\"");
                    break;
                case "json_remove":
                    e.Select(x => SqlFunctions.MySql.json_remove("{\"a\":1,\"b\":2}", "$.b"))
                        .First().Should().NotContain("\"b\"");
                    break;
                case "json_merge_patch":
                    e.Select(x => SqlFunctions.MySql.json_merge_patch("{\"a\":1}", "{\"b\":2}"))
                        .First().Should().Contain("\"b\": 2");
                    break;
                case "json_merge_preserve":
                    e.Select(x => SqlFunctions.MySql.json_merge_preserve("{\"a\":1}", "{\"b\":2}"))
                        .First().Should().Contain("\"a\": 1");
                    break;
                case "json_array_append":
                    e.Select(x => SqlFunctions.MySql.json_array_append("{\"a\":[1]}", "$.a", "2"))
                        .First().Should().Contain("[1, \"2\"]");
                    break;
                case "json_array_insert":
                    e.Select(x => SqlFunctions.MySql.json_array_insert("{\"a\":[1,2]}", "$.a[1]", "9"))
                        .First().Should().Contain("[1, \"9\", 2]");
                    break;
                case "json_depth":
                    e.Select(x => SqlFunctions.MySql.json_depth("{\"a\":{\"b\":1}}")).First().Should().Be(3);
                    break;
                case "json_keys":
                    e.Select(x => SqlFunctions.MySql.json_length(
                            SqlFunctions.MySql.json_keys("{\"a\":1,\"b\":2}")))
                        .First().Should().Be(2);
                    break;
                case "json_length":
                    e.Select(x => SqlFunctions.MySql.json_length("{\"a\":1,\"b\":2}")).First().Should().Be(2);
                    break;
                case "json_type":
                    e.Select(x => SqlFunctions.MySql.json_type("{\"a\":1}")).First().Should().Be("OBJECT");
                    break;
                case "uuid_to_bin":
                    e.Select(x => SqlFunctions.MySql.uuid_to_bin("550e8400-e29b-41d4-a716-446655440000"))
                        .First().Should().HaveCount(16);
                    break;
                case "bin_to_uuid":
                    e.Select(x => SqlFunctions.MySql.bin_to_uuid(
                            SqlFunctions.MySql.uuid_to_bin("550e8400-e29b-41d4-a716-446655440000")))
                        .First().Should().Be("550e8400-e29b-41d4-a716-446655440000");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fn), fn, "Unknown MySQL native function case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The negative edge of the native surface, on a live connection: a name outside the table is not
    /// claimed by <c>Supports</c> and is rejected by <c>Render</c> with the documented message. The
    /// marker row query keeps the case a live-database fact.
    /// </summary>
    [Fact]
    public void NativeFunctionTable_ShouldRejectNamesOutsideTheSurface()
    {
        CreateMarker();
        try
        {
            _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1).Select(x => x.Id)
                .First().Should().Be(1);

            var native = MySqlDialect.Instance.MySqlFunctions;
            native.Should().NotBeNull();

            native!.Supports("not_a_mysql_function").Should().BeFalse();
            native.Supports(null!).Should().BeFalse();
            FluentActions.Invoking(() => native.Render("not_a_mysql_function", Array.Empty<string>()))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*not supported by MySQL/MariaDB*");
            FluentActions.Invoking(() => native.Render(null!, Array.Empty<string>()))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*not supported by MySQL/MariaDB*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Reopened ledger row <c>C8-D3-UNSUPPORTED-GROUPING</c> (<c>SqlBuilder.cs:243-250</c>): MySQL reports
    /// <c>SupportsCube</c>/<c>SupportsGroupingSets</c> false (it overrides only <c>SupportsRollup</c>), so
    /// both super-aggregate forms are rejected at render time with the documented capability message.
    /// </summary>
    [Theory]
    [InlineData("cube")]
    [InlineData("grouping_sets")]
    public void UnsupportedGroupingModifiers_ShouldBeRejected(string grouping)
    {
        CreateMarker();
        try
        {
            switch (grouping)
            {
                case "cube":
                    FluentActions.Invoking(() => _sut.DataProvider.From<My3Marker>()
                            .GroupByCube(x => new { x.Grp })
                            .Select(x => new { x.Grp, Count = SqlFunctions.Sql.count() })
                            .ToList())
                        .Should().Throw<NotSupportedException>()
                        .WithMessage("*CUBE grouping modifier is not supported by this SQL dialect*");
                    break;
                case "grouping_sets":
                    FluentActions.Invoking(() => _sut.DataProvider.From<My3Marker>()
                            .GroupByGroupingSets(x => new { x.Grp }, new[] { 0 })
                            .Select(x => new { x.Grp, Count = SqlFunctions.Sql.count() })
                            .ToList())
                        .Should().Throw<NotSupportedException>()
                        .WithMessage("*GROUPING SETS modifier is not supported by this SQL dialect*");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "Unknown MySQL grouping case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The capability-specific control for the reopened row: <c>GroupByRollup</c> is supported by the same
    /// provider and returns the two groups plus the grand-total row (a NULL group with every marker row).
    /// </summary>
    [Fact]
    public void GroupByRollup_ShouldAddTheGrandTotalRow()
    {
        CreateMarker();
        try
        {
            var rollup = _sut.DataProvider.From<My3Marker>()
                .GroupByRollup(x => new { x.Grp })
                .Select(x => new { x.Grp, Count = SqlFunctions.Sql.count() })
                .ToList();

            rollup.Should().HaveCount(3);
            rollup.Single(r => r.Grp is null).Count.Should().Be(4);
            rollup.Where(r => r.Grp is not null).Select(r => r.Count).Should().BeEquivalentTo(new[] { 2, 2 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// <c>MakeDateAdd</c> folded/scaled units (milliseconds and microseconds fold onto
    /// <c>microsecond</c>; decade/century/millennium scale a year) executed by the live server.
    /// </summary>
    [Fact]
    public void DateAddUnits_ShouldFoldAndScale()
    {
        CreateMarker();
        try
        {
            var row = _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1)
                .Select(x => new
                {
                    Decade = SqlFunctions.Sql.date_add("decade", 1, Value),
                    Century = SqlFunctions.Sql.date_add("century", 1, Value),
                    Millennium = SqlFunctions.Sql.date_add("millennium", 1, Value),
                    Millis = SqlFunctions.Sql.date_add("milliseconds", 250, Value),
                    Micros = SqlFunctions.Sql.date_add("microseconds", 500000, Value),
                })
                .Single();

            row.Decade.Should().Be(Value.AddYears(10));
            row.Century.Should().Be(Value.AddYears(100));
            row.Millennium.Should().Be(Value.AddYears(1000));
            row.Millis.Should().BeCloseTo(Value.AddMilliseconds(250), TimeSpan.FromMilliseconds(1));
            row.Micros.Should().BeCloseTo(Value.AddMilliseconds(500), TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// <c>MakeDateDiff</c> units rendered through <c>timestampdiff</c> and the scaled cast, executed by
    /// the live server over a 14-month span.
    /// </summary>
    [Fact]
    public void DateDiffUnits_ShouldMeasureTheSpan()
    {
        CreateMarker();
        try
        {
            var later = Value.AddMonths(14);

            var row = _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1)
                .Select(x => new
                {
                    Year = SqlFunctions.Sql.date_diff("year", Value, later),
                    Quarter = SqlFunctions.Sql.date_diff("quarter", Value, later),
                    Month = SqlFunctions.Sql.date_diff("month", Value, later),
                })
                .Single();

            row.Year.Should().Be(1);
            row.Quarter.Should().Be(4);
            row.Month.Should().Be(14);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The dialect-local guard arms and type map: an unknown date unit is rejected by the renderer with
    /// its documented message, the CLR type map names every numeric/text/temporal target, and the index
    /// hint renderer is offered. A live marker read keeps the fact on the database.
    /// </summary>
    [Fact]
    public void DialectGuardsAndTypeMap_ShouldRejectUnknownUnitsAndNameTypes()
    {
        var dialect = ((DataContext)_sut.DataProvider).Dialect;

        FluentActions.Invoking(() => dialect.MakeDateAdd("fortnight", "1", "d"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*MySQL date_add does not support the 'fortnight' field*");
        FluentActions.Invoking(() => dialect.MakeDateDiff("fortnight", "a", "b"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*MySQL date_diff does not support the 'fortnight' field*");

        dialect.MakeTypeName(typeof(byte)).Should().Be("unsigned");
        dialect.MakeTypeName(typeof(short)).Should().Be("signed");
        dialect.MakeTypeName(typeof(uint)).Should().Be("bigint");
        dialect.MakeTypeName(typeof(float)).Should().Be("float");
        dialect.MakeTypeName(typeof(double)).Should().Be("double");
        dialect.MakeTypeName(typeof(decimal)).Should().Be("decimal");
        dialect.MakeTypeName(typeof(string)).Should().Be("char");
        dialect.MakeTypeName(typeof(bool)).Should().Be("signed");
        dialect.MakeTypeName(typeof(DateTimeOffset)).Should().Be("datetime");
        dialect.MakeTypeName(typeof(TimeSpan)).Should().Be("time");
        dialect.MakeTypeName(typeof(Guid)).Should().Be(nameof(Guid));
        dialect.IndexHints.Should().NotBeNull();

        CreateMarker();
        try
        {
            _sut.DataProvider.From<My3Marker>().Where(x => x.Id == 1)
                .Select(x => x.Id).First().Should().Be(1);
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
        Execute("drop table if exists my3_marker");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropMarker() => Execute("drop table if exists my3_marker");

    /// <summary>The class-owned marker table: a nullable numeric, text payload and grouping column.</summary>
    [SqlTable("my3_marker")]
    public sealed class My3Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }
}
