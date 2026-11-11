using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL dialect branch coverage (cycle 3, cluster D3) executed against a live Testcontainers
/// server. Every fact runs over the class-owned <c>pg3_marker</c> table, created, seeded and dropped
/// by the class, so no assertion depends on the shared seed totals.
///
/// <para>
/// Targeted surface: <c>PostgresEntityBuilderExtensions.DistinctOn</c> and the PostgreSQL
/// <c>distinct on (...)</c> / leading-<c>order by</c> rendering (single-column key, composite
/// anonymous key, descending secondary ordering, and the derived-table redirect of a filtered
/// source), plus the PostgreSQL paging renderer.
/// </para>
///
/// <para>
/// Overlap avoided: <c>PostgresSpecificTests</c>, <c>PostgresFunctionsTests</c> and
/// <c>PostgresExtremeRowNativeSpecificTests</c> keep their own scenarios; this class adds the
/// DISTINCT ON ordering matrix plus the derived-source and paging row assertions.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresDialectBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string CreateTableSql =
        "create table pg3_marker (id int not null primary key, num int null, label text null, grp int null)";

    private const string SeedSql =
        "insert into pg3_marker (id, num, label, grp) values "
        + "(1, 10, 'one', 1), "
        + "(2, 10, 'two', 2), "
        + "(3, 20, 'three', 2), "
        + "(4, 20, 'four', 2), "
        + "(5, 30, 'five', 1)";

    /// <summary>
    /// One case per DISTINCT ON ordering branch: a single ascending key, a single key with a
    /// descending secondary ordering, a composite anonymous key and the composite key with a
    /// descending tie-breaker.
    /// </summary>
    [Theory]
    [InlineData("single_asc")]
    [InlineData("single_desc_second")]
    [InlineData("multi_key_asc")]
    [InlineData("multi_key_desc_second")]
    public void DistinctOn_OrderingVariants_ShouldKeepTheFirstRowPerKey(string variant)
    {
        CreateMarker();
        try
        {
            var e = _sut.DataProvider.From<Pg3Marker>();

            switch (variant)
            {
                case "single_asc":
                    e.DistinctOn(x => x.Num)
                        .OrderBy(x => x.Num)
                        .OrderBy(x => x.Id)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(1, 3, 5);
                    break;
                case "single_desc_second":
                    e.DistinctOn(x => x.Num)
                        .OrderBy(x => x.Num)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(2, 4, 5);
                    break;
                case "multi_key_asc":
                    e.DistinctOn(x => new { x.Num, x.Grp })
                        .OrderBy(x => x.Num)
                        .OrderBy(x => x.Grp)
                        .OrderBy(x => x.Id)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(1, 2, 3, 5);
                    break;
                case "multi_key_desc_second":
                    e.DistinctOn(x => new { x.Num, x.Grp })
                        .OrderBy(x => x.Num)
                        .OrderBy(x => x.Grp)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Id)
                        .ToList().Should().Equal(1, 2, 4, 5);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown DistinctOn case.");
            }
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A filtered source is redirected into a derived table whose <c>DISTINCT ON</c> key column is
    /// qualified through the derived alias; the surviving rows are asserted.
    /// </summary>
    [Fact]
    public void DistinctOn_OverDerivedSource_ShouldRedirectToTheInnerColumn()
    {
        CreateMarker();
        try
        {
            var rows = _sut.DataProvider.From<Pg3Marker>()
                .Where(x => x.Id > 1)
                .DistinctOn(x => x.Num)
                .OrderBy(x => x.Num)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Num, x.Id })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(2, 3, 5);
            rows.Select(r => r.Num).Should().Equal(10, 20, 30);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A set-returning table function is exposed as the query's <c>FROM</c> source: PostgreSQL renders
    /// <c>regexp_split_to_table(...)</c> wrapped in the one-column subquery that restores the
    /// function-named column (<c>WrapTableFunction</c>), and the fragments are read back from the live
    /// server.
    /// </summary>
    [Fact]
    public void TableFunction_RegexpSplitToTable_ShouldReturnFragments()
    {
        CreateMarker();
        try
        {
            var fragments = _sut.DataProvider
                .From(() => SqlFunctions.Postgres.regexp_split_to_table("a,b,c", ","))
                .Select(r => r.Value)
                .ToList();

            fragments.Should().Equal("a", "b", "c");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A raw-SQL <c>FROM</c> source (enabled by <c>PostgresDialect.SupportsRawSqlSource</c>) explicitly
    /// bound with <c>BindEntity</c> is joined to the typed marker source; the aliased raw derived table
    /// stitches one row per marker id.
    /// </summary>
    [Fact]
    public void RawSqlSource_JoinedToTypedSource_ShouldStitchMatchingRows()
    {
        CreateMarker();
        try
        {
            var raw = _sut.DataProvider
                .FromSql("select id, num from pg3_marker")
                .BindEntity<Pg3Marker>(["id", "num"]);

            var rows = _sut.DataProvider
                .From<Pg3Marker>()
                .Join(raw, (l, r) => l.Id == r.Id)
                .Select(p => new { p.Item1.Id, p.Item2.Num })
                .ToList();

            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
            rows.Select(r => r.Num).Should().BeEquivalentTo(new int?[] { 10, 10, 20, 20, 30 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>The paged select renders the PostgreSQL limit/offset form and returns the window.</summary>
    [Fact]
    public void PagedSelect_WithOrderBy_ShouldReturnTheRequestedPage()
    {
        CreateMarker();
        try
        {
            var page = _sut.DataProvider.From<Pg3Marker>()
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

    /// <summary>
    /// <c>MakePage</c> arms: <c>LIMIT n WITH TIES</c> with an offset renders <c>offset N fetch first n
    /// rows with ties</c> and keeps the tied boundary rows, while an offset-only page renders
    /// <c>offset N</c> without a limit. Both windows are read back from the live server.
    /// </summary>
    [Fact]
    public void Paging_WithTiesAndOffsetOnly_ShouldReturnTheRequestedWindow()
    {
        CreateMarker();
        try
        {
            var ties = _sut.DataProvider.From<Pg3Marker>()
                .OrderBy(x => x.Num)
                .Offset(1)
                .Limit(2)
                .WithTies()
                .Select(x => x.Id)
                .ToList();

            ties.Should().Equal(2, 3, 4);

            var offsetOnly = _sut.DataProvider.From<Pg3Marker>()
                .OrderBy(x => x.Id)
                .Offset(2)
                .Select(x => x.Id)
                .ToList();

            offsetOnly.Should().Equal(3, 4, 5);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// <c>MakeCreateTableAsHead</c>/<c>MakeCreateTableAsTail</c> arms: every <c>ON COMMIT</c> option of a
    /// temporary materialisation is accepted by the server, and <c>WITH NO DATA</c> leaves the created
    /// table empty while the default form carries the rows.
    /// </summary>
    [Theory]
    [InlineData("preserve_rows", true, 5)]
    [InlineData("delete_rows", false, 0)]
    [InlineData("drop", false, 0)]
    public void CreateTemporaryTableAsSelect_ShouldHonourOnCommitAndDataFlags(string variant, bool withData, int expected)
    {
        CreateMarker();
        var name = "pg3_ctas_" + variant;
        try
        {
            var onCommit = variant switch
            {
                "preserve_rows" => TempTableOnCommit.PreserveRows,
                "delete_rows" => TempTableOnCommit.DeleteRows,
                _ => TempTableOnCommit.Drop
            };

            _sut.DataProvider.From<Pg3Marker>()
                .Select(x => new { x.Id, x.Num })
                .ToTempTable(name, o => o.OnCommit(onCommit).WithData(withData));

            var read = () => _sut.DataProvider.From(name)
                .Select(t => t.GetInt64("id"))
                .ToList();

            if (variant == "drop")
            {
                // ON COMMIT DROP removes the table at the end of the creating (auto-commit)
                // transaction, so the created-then-dropped relation is verifiably absent afterwards.
                FluentActions.Invoking(read)
                    .Should().Throw<DbException>()
                    .WithMessage("*does not exist*");
            }
            else
            {
                read().Should().HaveCount(expected);
            }
        }
        finally
        {
            Execute($"drop table if exists {name}");
            DropMarker();
        }
    }

    /// <summary>
    /// The public versioned constructor and the type-name/query-hint renderers: a pre-9.4 version gates
    /// the ANSI aggregate <c>FILTER</c> off, ranges and range arrays name their native PostgreSQL types,
    /// and the top-level-<c>select</c> scan inserts the hint comment while skipping quoted text and
    /// parenthesised subqueries. A live marker read keeps the fact on the database.
    /// </summary>
    [Fact]
    public void DialectSurface_ShouldGateTheVersionAndNameTheNativeTypes()
    {
        var context = (DataContext)_sut.DataProvider;

        new PostgresDialect(new Version(9, 3)).AggregateFilterStyle.Should().Be(AggregateFilterStyle.None);
        new PostgresDialect(new Version(9, 4)).AggregateFilterStyle.Should().Be(AggregateFilterStyle.AnsiFilter);
        new PostgresDialect(null).AggregateFilterStyle.Should().Be(AggregateFilterStyle.AnsiFilter);

        var dialect = context.Dialect;
        dialect.MakeTypeName(typeof(Range<int>)).Should().Be("int4range");
        dialect.MakeTypeName(typeof(Range<DateTime>[])).Should().Be("tsmultirange");
        dialect.MakeTypeName(typeof(string)).Should().Be("text");
        dialect.MakeTypeName(typeof(TimeSpan)).Should().Be("interval");
        dialect.MakeTypeName(typeof(uint)).Should().Be("xid");

        dialect.RenderQueryHints("select 'select' as x from pg3_marker", new[] { "SeqScan(t)" }, null)
            .Should().Be("select /*+ SeqScan(t) */ 'select' as x from pg3_marker");
        dialect.RenderQueryHints("with c as (select 1) select * from c", new[] { "SeqScan(t)" }, null)
            .Should().Be("with c as (select 1) select /*+ SeqScan(t) */ * from c");
        dialect.RenderQueryHints("update pg3_marker set num = 1", new[] { "SeqScan(t)" }, null)
            .Should().Be("update pg3_marker set num = 1");

        CreateMarker();
        try
        {
            _sut.DataProvider.From<Pg3Marker>().Where(x => x.Id == 1)
                .Select(x => x.Label).Single().Should().Be("one");
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
        Execute("drop table if exists pg3_marker");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropMarker() => Execute("drop table if exists pg3_marker");

    /// <summary>The class-owned marker table: a nullable key, text payload and grouping column.</summary>
    [SqlTable("pg3_marker")]
    public sealed class Pg3Marker
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
