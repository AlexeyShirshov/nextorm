using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T308: the <c>SETTINGS</c> clause must render after paging, not before it. ClickHouse rejects
/// <c>... SETTINGS max_threads = 2 LIMIT 1</c> with server Code 62 (an unparseable query), so a
/// single-row terminal (<c>First</c>/<c>FirstOrDefault</c>/<c>Single</c>) or a page over a query that
/// carries settings used to fail on a live server. Every fact creates, seeds and drops its own
/// <c>t308_settings_scalar</c> MergeTree table and asserts the concrete value or rows the server
/// returns; the SQL string itself is never asserted here.
///
/// <para>
/// The decisive arm is <see cref="AggregateFirst_ReturnsThree"/>: an aggregate projection with
/// <c>.Settings(...)</c> terminated by <c>First()</c>, which drives <c>Paging.Limit = 1</c>. The
/// control arm <see cref="ListSingleControl_ReturnsThree"/> is the same query read with
/// <c>ToList().Single()</c> (no paging) and must pass before the fix.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class T308SettingsScalarTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private object? ExecuteScalar(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        return command.ExecuteScalar();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists t308_settings_scalar");
        Execute("create table t308_settings_scalar (id Int32, grp Int32, num Int32) "
                + "engine = ReplacingMergeTree order by id");
        Execute("insert into t308_settings_scalar (id, grp, num) values (1, 1, 10), (2, 1, 20), (3, 2, 30)");
    }

    private void DropMarker() => Execute("drop table if exists t308_settings_scalar");

    /// <summary>Diagnostic only: the prepared SQL of an equivalent command shape (never asserted).</summary>
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, TestContext.Current.CancellationToken))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private QueryCommand<int> AggregateWithSettings()
        => Context.From<T308Row>()
            .Settings(("max_threads", "2"))
            .Select(x => SqlFunctions.Sql.count());

    /// <summary>
    /// Decisive arm: aggregate + <c>SETTINGS</c> + <c>First()</c>. <c>First()</c> sets
    /// <c>Paging.Limit = 1</c>, so the generated query carries the settings clause before the limit and
    /// the server answers Code 62 instead of the scalar 3.
    /// </summary>
    [Fact]
    public void AggregateFirst_ReturnsThree()
    {
        CreateMarker();
        try
        {
            var query = AggregateWithSettings();
            Console.WriteLine($"T308 AggregateFirst shape (limit 1):{Environment.NewLine}{SqlOf(Context, query.Limit(1))}");

            var count = AggregateWithSettings().First();

            count.Should().Be(3, "the table holds three rows and settings are execution hints, not row filters");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Control arm: the same aggregate + <c>SETTINGS</c> query read with <c>ToList().Single()</c>. No
    /// paging is applied, so the settings clause stays in the valid trailing position and this passes
    /// before the fix.
    /// </summary>
    [Fact]
    public void ListSingleControl_ReturnsThree()
    {
        CreateMarker();
        try
        {
            var count = AggregateWithSettings().ToList().Single();

            count.Should().Be(3, "the control form must already succeed without a trailing limit");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Aggregate + settings + <c>FirstOrDefault()</c> (<c>Paging.Limit = 1</c>) returns 3.</summary>
    [Fact]
    public void FirstOrDefault_ReturnsThree()
    {
        CreateMarker();
        try
        {
            var count = AggregateWithSettings().FirstOrDefault();

            count.Should().Be(3, "the aggregate still yields exactly one row carrying the value 3");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Aggregate + settings + <c>Single()</c> (<c>Paging.Limit = 2</c>) returns 3.</summary>
    [Fact]
    public void Single_ReturnsThree()
    {
        CreateMarker();
        try
        {
            var count = AggregateWithSettings().Single();

            count.Should().Be(3, "the aggregate produces a single row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Aggregate + settings + <c>SingleOrDefault()</c> (<c>Paging.Limit = 2</c>) returns 3.</summary>
    [Fact]
    public void SingleOrDefault_ReturnsThree()
    {
        CreateMarker();
        try
        {
            var count = AggregateWithSettings().SingleOrDefault();

            count.Should().Be(3, "the aggregate produces a single row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>A page over a settings-bearing query returns the requested rows in order.</summary>
    [Fact]
    public void Paging_ReturnsExpectedRows()
    {
        CreateMarker();
        try
        {
            var page = Context.From<T308Row>()
                .Settings(("max_threads", "2"))
                .OrderBy(x => x.Id)
                .Limit(2)
                .Select(x => x.Id)
                .ToList();

            page.Should().Equal(new[] { 1, 2 }, "the paged ordered read keeps the first two rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>LIMIT BY with settings and a single-row terminal keeps the first row of the first group.</summary>
    [Fact]
    public void LimitByFirst_ReturnsExpectedRow()
    {
        CreateMarker();
        try
        {
            var first = Context.From<T308Row>()
                .Settings(("max_threads", "2"))
                .OrderBy(x => x.Id)
                .LimitBy(1, x => x.Grp)
                .Select(x => x.Id)
                .First();

            first.Should().Be(1, "LIMIT 1 BY grp keeps id 1 for group 1, and the trailing limit takes the first row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>A <c>UNION ALL</c> whose first operand carries settings returns the rows of both arms.</summary>
    [Fact]
    public void UnionSettings_ReturnsExpectedRows()
    {
        CreateMarker();
        try
        {
            var low = Context.From<T308Row>()
                .Settings(("max_threads", "2"))
                .Where(x => x.Id <= 2)
                .Select(x => x.Id);
            var high = Context.From<T308Row>()
                .Where(x => x.Id == 3)
                .Select(x => x.Id);

            // ClickHouse rejects a bare `union` with Code 558 unless union_default_mode is set, so the
            // accepted shape is `union all`; the SETTINGS-ordering question is independent of the keyword.
            var union = low.UnionAll(high);
            Console.WriteLine($"T308 union shape:{Environment.NewLine}{SqlOf(Context, union)}");

            var rows = union.ToList();

            rows.Should().BeEquivalentTo(new[] { 1, 2, 3 }, "the union keeps every row of both operands");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Raw grammar probe against the live server: the trailing order <c>LIMIT ... SETTINGS ...</c> is
    /// valid, while <c>SETTINGS ... LIMIT ...</c> is rejected with server Code 62. This pins the
    /// server rule the generator must satisfy without asserting on any nextorm-rendered SQL string.
    /// </summary>
    [Fact]
    public void ClauseBoundaries_AreValid()
    {
        CreateMarker();
        try
        {
            var valid = ExecuteScalar(
                "select count() from t308_settings_scalar limit 1 settings max_threads = 2");
            Convert.ToInt64(valid, System.Globalization.CultureInfo.InvariantCulture)
                .Should().Be(3, "paging before settings is the ClickHouse-valid order");

            var invalid = () => ExecuteScalar(
                "select count() from t308_settings_scalar settings max_threads = 2 limit 1");
            invalid.Should().Throw<ClickHouseServerException>(
                    "ClickHouse rejects a limit that follows the settings clause")
                .Which.ErrorCode.Should().Be(62, "the server reports a syntax error (Code 62) for that order");
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("t308_settings_scalar")]
    public sealed class T308Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("num")]
        public int Num { get; set; }
    }
}
