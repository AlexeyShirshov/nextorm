using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server preparation differentials (cycle 4, cluster D1) executed against a live Testcontainers
/// server. Conservatively scoped: every form below already has a passing neighbour fact on this
/// provider, and each fact creates, seeds and drops its own <c>ss4_marker</c> table, so no assertion
/// depends on the shared seed totals or on test order.
///
/// <para>
/// Neighbours that prove every form (read and deliberately not repeated):
/// <c>SqlServerFunctionExecutionTests.ForJson_ShouldExecute</c> (path/auto/root),
/// <c>SqlServerFunctionExecutionTests.PagingWithTies_ShouldExecute</c> (<c>Page</c> + <c>WithTies</c>)
/// and <c>SqlServerFeaturesTests.TableHint_ShouldExecute</c> (<c>From&lt;T&gt;(o =&gt;
/// o.WithTableHint("nolock"))</c>). This class adds the differential round trip over class-owned rows
/// instead: the same command shape executed before and after a one-dimension change, with the live
/// rows asserted on every step.
/// </para>
///
/// <para>
/// Dropped from this dispatch (recorded, never fabricated): the <c>RawShape.ForTableSample/Temporal</c>
/// arms and the temporal-table form - no existing integration fact renders either on SQL Server - and
/// the descriptor comparison of the FOR JSON clause, which is driven from the SQLite differential class
/// instead (the <c>ForJson</c> builder is provider-agnostic).
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerPreparationDifferentialBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists ss4_marker");
        Execute("create table ss4_marker (id int not null primary key, name nvarchar(50) not null, num int null, grp int not null)");
        Execute("insert into ss4_marker (id, name, num, grp) values (1, 'alpha', 1, 1), (2, 'beta', 1, 1), (3, 'gamma', 2, 2)");
    }

    private void DropMarker() => Execute("drop table if exists ss4_marker");

    /// <summary>FOR JSON path/auto/round trip over the class-owned marker rows (V04).</summary>
    [Fact]
    public void ForJsonDifferential_PathAutoAndRoot_ShouldReturnDocuments()
    {
        CreateMarker();
        try
        {
            var path = Context.From<Ss4Marker>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Name })
                .ForJson(ForJsonMode.Path);

            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("\"id\":1");

            var auto = Context.From<Ss4Marker>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Name })
                .ForJson(ForJsonMode.Auto);

            auto.Should().NotBeNullOrEmpty();
            auto.Should().Contain("\"id\":1");

            // The round trip: the path form is correct again after the auto form has been prepared.
            var pathAgain = Context.From<Ss4Marker>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Name })
                .ForJson(ForJsonMode.Path);

            pathAgain.Should().Contain("\"id\":1");

            var rooted = Context.From<Ss4Marker>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Name })
                .ForJson(ForJsonMode.Path, root: "row");

            rooted.Should().Contain("\"row\"");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>WITH TIES keeps the whole tied group of the requested page over class-owned rows (V04).</summary>
    [Fact]
    public void PagingWithTiesDifferential_ShouldKeepTheTiedGroup()
    {
        CreateMarker();
        try
        {
            var page = Context.From<Ss4Marker>()
                .Where(x => x.Num != null)
                .OrderBy(x => x.Num)
                .Page(1, 0)
                .Select(x => x.Id)
                .ToList();

            page.Should().HaveCount(1, "a plain page of one row keeps only the first");

            var tied = Context.From<Ss4Marker>()
                .Where(x => x.Num != null)
                .OrderBy(x => x.Num)
                .Page(1, 0)
                .WithTies()
                .Select(x => x.Id)
                .ToList();

            tied.Should().HaveCount(2, "both rows of the tied group stay on the page");

            var tiedAgain = Context.From<Ss4Marker>()
                .Where(x => x.Num != null)
                .OrderBy(x => x.Num)
                .Page(1, 0)
                .WithTies()
                .Select(x => x.Id)
                .ToList();

            tiedAgain.Should().HaveCount(2, "the tied page is correct again on the reused shape");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>A table hint changes the rendering, never the rows: the hinted read equals the plain one.</summary>
    [Fact]
    public void TableHintDifferential_ShouldReadTheSameRows()
    {
        CreateMarker();
        try
        {
            var plain = Context.From<Ss4Marker>().OrderBy(x => x.Id).Select(x => x.Id).ToList();
            plain.Should().Equal(new[] { 1, 2, 3 });

            var hinted = Context.From<Ss4Marker>(o => o.WithTableHint("nolock"))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            hinted.Should().Equal(new[] { 1, 2, 3 }, "the hint is a rendering dimension, not a row filter");

            var hintedAgain = Context.From<Ss4Marker>(o => o.WithTableHint("nolock"))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            hintedAgain.Should().Equal(new[] { 1, 2, 3 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Marker row of the class-owned <c>ss4_marker</c> table.</summary>
    [SqlTable("ss4_marker")]
    public sealed class Ss4Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("num")]
        public int? Num { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }
}
