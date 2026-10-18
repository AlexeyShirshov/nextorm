using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

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
