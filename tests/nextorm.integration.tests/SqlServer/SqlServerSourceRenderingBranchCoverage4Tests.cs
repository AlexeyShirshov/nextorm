using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server source-rendering coverage (cycle 4, cluster D3) executed against a live Testcontainers
/// server. Conservatively scoped to forms that an EXISTING PASSING integration fact already proves on
/// this provider; every fact creates, seeds and drops its own <c>ss4r_marker</c>/<c>ss4r_line</c>
/// tables, and asserts the rows the server returns.
///
/// <para>
/// Proving neighbours (read, cited, deliberately not repeated as such): the table-hint form
/// <c>SqlServer/SqlServerFeaturesTests.cs:56</c> (<c>From&lt;SimpleEntity&gt;(o =&gt;
/// o.WithTableHint("nolock"))</c>) and the linked-server qualifier
/// <c>SqlServer/SqlServerExtendedFunctionTests.cs:67</c> (<c>WithServer(".")</c>).
/// </para>
///
/// <para>
/// Distinguishing arms: the hint renders on the PRIMARY source of a JOINED statement (the
/// <c>MakeFrom</c> physical branch under <c>needAlias</c>/<c>hasJoins</c>), re-prepared on the same
/// context; the server qualifier goes through <c>MakeFrom</c>'s <c>SupportsLinkedServer</c> arm
/// (496-499) and <c>MakeQualifiedTableName</c>, which neither the SQLite class (rejects it) nor the
/// D1 SQL Server differential (unqualified, unjoined read) executes.
/// </para>
///
/// <para>
/// Dropped from this dispatch (recorded, never fabricated): the <c>TABLESAMPLE</c> and
/// <c>FOR SYSTEM_TIME</c> forms - no existing integration fact renders either on SQL Server (only
/// dialect-unit and in-memory tests do), so they are deferred instead of gambled on a red boundary.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerSourceRenderingBranchCoverage4Tests : ProviderTestSuite
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
        Execute("drop table if exists ss4r_line");
        Execute("drop table if exists ss4r_marker");
        Execute("create table ss4r_marker (id int not null primary key, name nvarchar(50) not null, num int null, grp int not null)");
        Execute("create table ss4r_line (id int not null primary key, marker_id int not null, amount int null)");
        Execute("insert into ss4r_marker (id, name, num, grp) values (1, 'alpha', 10, 1), (2, 'beta', 10, 1), (3, 'gamma', 20, 2)");
        Execute("insert into ss4r_line (id, marker_id, amount) values (11, 1, 5), (12, 1, 7), (13, 2, 9)");
    }

    private void DropMarker()
    {
        Execute("drop table if exists ss4r_line");
        Execute("drop table if exists ss4r_marker");
    }

    /// <summary>
    /// Distinguishing arm: the table hint on the primary source of a JOINED statement, re-prepared on
    /// the same context. Proved form: <c>SqlServerFeaturesTests.cs:56</c> (hint), join form
    /// <c>CommonTestSuite.Join</c>.
    /// </summary>
    [Fact]
    public void TableHint_OnJoinedPrimarySource_ShouldNotChangeTheJoinedRows()
    {
        CreateMarker();
        try
        {
            var plain = Context.From<Ss4rMarker>()
                .Where(x => x.Num != null)
                .Join(Context.From<Ss4rLine>(), (m, l) => m.Id == l.MarkerId)
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id, p.Item2.Amount })
                .ToList();

            plain.Should().HaveCount(3);
            plain.Select(r => r.Line).Should().Equal(11, 12, 13);

            var hinted = Context.From<Ss4rMarker>(o => o.WithTableHint("nolock"))
                .Where(x => x.Num != null)
                .Join(Context.From<Ss4rLine>(), (m, l) => m.Id == l.MarkerId)
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id, p.Item2.Amount })
                .ToList();

            hinted.Should().HaveCount(3, "a table hint is a rendering dimension, not a row filter");
            hinted.Select(r => r.Amount).Should().Equal(new int?[] { 5, 7, 9 });

            var hintedAgain = Context.From<Ss4rMarker>(o => o.WithTableHint("nolock"))
                .Where(x => x.Num != null)
                .Join(Context.From<Ss4rLine>(), (m, l) => m.Id == l.MarkerId)
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => new { p.Item1.Id, Line = p.Item2.Id })
                .ToList();

            hintedAgain.Should().HaveCount(3, "the hinted joined shape is correct again on the reused context");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the linked-server qualifier arm of <c>MakeFrom</c> (496-499) on a
    /// class-owned table. Proved form: <c>SqlServerExtendedFunctionTests.cs:67</c>
    /// (<c>WithServer(".")</c>), which is a read of the shared seed, not of a class-owned marker.
    /// </summary>
    [Fact]
    public void WithServer_OnClassOwnedMarker_ShouldReadTheSameRows()
    {
        CreateMarker();
        try
        {
            var plain = Context.From<Ss4rMarker>().OrderBy(x => x.Id).Select(x => x.Id).ToList();
            plain.Should().Equal(new[] { 1, 2, 3 });

            var qualified = Context.From<Ss4rMarker>()
                .WithServer(".")
                .Where(x => x.Grp == 1)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            qualified.Should().Equal(new[] { 1, 2 }, "the linked-server prefix only qualifies the name");

            var qualifiedAgain = Context.From<Ss4rMarker>()
                .WithServer(".")
                .Where(x => x.Grp == 2)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            qualifiedAgain.Should().Equal(new[] { 3 }, "the same qualified shape reads the other group");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Marker row of the class-owned <c>ss4r_marker</c> table.</summary>
    [SqlTable("ss4r_marker")]
    public sealed class Ss4rMarker
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

    /// <summary>Line row of the class-owned <c>ss4r_line</c> table.</summary>
    [SqlTable("ss4r_line")]
    public sealed class Ss4rLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }
}
