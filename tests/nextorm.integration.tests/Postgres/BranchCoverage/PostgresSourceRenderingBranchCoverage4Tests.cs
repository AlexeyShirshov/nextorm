using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL source-rendering coverage (cycle 4, cluster D3) executed against a live Testcontainers
/// server. Every fact creates, seeds and drops its own <c>pg4r_marker</c> table and asserts the rows
/// the server returns.
///
/// <para>
/// Proving neighbours (read, cited, deliberately not repeated): the correlated APPLY family is proved
/// live by <c>CommonTestSuite/Queries/CommonTestSuite.CorrelatedQuery.cs:341</c>
/// (<c>CorrelatedCrossApply_ShouldEvaluatePerOuterRow</c>) and <c>:379</c>
/// (<c>CrossApply_OnPlainTable_ShouldCrossJoin</c>); the derived-join form by
/// <c>CommonTestSuite/Joins/CommonTestSuite.Join.cs:225</c>.
/// </para>
///
/// <para>
/// Distinguishing arms: <c>MakeJoin</c>'s APPLY dispatch (223-224) into <c>MakeApplyJoin</c> (397) on
/// PostgreSQL's LATERAL spelling over a CLASS-OWNED correlated derived source that filters on the
/// outer value (the neighbours use the shared seed tables), and the per-join table-hint source
/// validation <c>ValidateJoinTableHintSource</c> (:354-355, 0-hit in the baseline) plus
/// <c>IsPhysicalTableSource</c> (:360-367, 0-hit) rejecting a hint on a derived join source, paired
/// with the same join executed live.
/// </para>
///
/// <para>
/// Dropped from this dispatch (recorded, never fabricated): <c>DISTINCT ON</c> over a derived source
/// and the extreme-row renderings are already proved live by
/// <c>PostgresDialectBranchCoverage3Tests.cs:107</c> and <c>PostgresExtremeRowNativeSpecificTests</c>,
/// so a repeat would not close a new arm; the temporal form has no passing PostgreSQL integration
/// neighbour and is deferred.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresSourceRenderingBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

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
        Execute("drop table if exists pg4r_marker");
        Execute("create table pg4r_marker (id int not null primary key, num int null, label text null, grp int null)");
        Execute("insert into pg4r_marker (id, num, label, grp) values (1, 10, 'one', 1), (2, 10, 'two', 1), (3, 20, 'three', 2), (4, 30, 'four', 2)");
    }

    private void DropMarker() => Execute("drop table if exists pg4r_marker");

    /// <summary>
    /// Distinguishing arm: the LATERAL apply over a class-owned correlated derived source (the outer
    /// value filters the inner rows), plus the plain-table fallback spelling on the same context.
    /// Proved forms: <c>CommonTestSuite.CorrelatedQuery.cs:341</c> and <c>:379</c>.
    /// </summary>
    [Fact]
    public void CrossApply_OnClassOwnedRows_ShouldEvaluateTheCorrelatedSourcePerRow()
    {
        CreateMarker();
        try
        {
            var correlated = Context.From<Pg4rMarker>()
                .Where(m => m.Grp == 1)
                .CrossApply(m => Context.From<Pg4rMarker>().Where(x => x.Num == m.Num && x.Id > m.Id))
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            correlated.Should().HaveCount(1, "of the two group-1 rows only id 1 has a later row with the same num");
            correlated[0].Left.Should().Be(1);
            correlated[0].Right.Should().Be(2);

            var plain = Context.From<Pg4rMarker>()
                .Where(m => m.Grp == 2)
                .CrossApply(Context.From<Pg4rMarker>())
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            plain.Should().HaveCount(8, "two group-2 rows times four marker rows (a lateral cross join)");
            plain.Should().OnlyContain(p => p.Left >= 3);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the per-join table-hint source validation on a DERIVED join source (the
    /// <c>IsPhysicalTableSource</c> false arm), with the identical join executed live without a hint.
    /// Proved form: the derived-source join at <c>CommonTestSuite.Join.cs:225</c>.
    /// </summary>
    [Fact]
    public void Join_PerTableHintOnDerivedSource_ShouldBeRejectedWhileThePlainJoinRuns()
    {
        CreateMarker();
        try
        {
            var derived = Context.From<Pg4rMarker>().Where(x => x.Grp == 1).Select(x => new { x.Id, x.Num });

            var live = Context.From<Pg4rMarker>()
                .Join(Context.From(derived), (m, d) => m.Id == d.Id)
                .OrderBy(p => p.Item1.Id)
                .Select(p => p.Item1.Id)
                .ToList();

            live.Should().Equal(new[] { 1, 2 });

            var act = () => Context.From<Pg4rMarker>()
                .Join(Context.From(derived), (m, d) => m.Id == d.Id, j => j.WithJoinTableHint("SeqScan(t1)"))
                .Select(p => p.Item1.Id)
                .ToList();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*only supported on a physical table source*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>Marker row of the class-owned <c>pg4r_marker</c> table.</summary>
    [SqlTable("pg4r_marker")]
    public sealed class Pg4rMarker
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
