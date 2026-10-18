using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Source-rendering differentials (cycle 4, cluster D3) executed against a live SQLite database. Every
/// fact reads class-owned marker rows of <c>sr4_marker</c>/<c>sr4_line</c> (dropped, created and seeded
/// by this class's own DDL in the constructor) through the inherited repository; the emitted SQL is
/// never the subject - the rows the database actually returns are asserted on every step, and a
/// rejection is always paired with a live counterpart of the same shape.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>SqlSourceRenderer.MakeFrom</c> (424/432) - the
/// derived-subquery branch 561-593, driven here as a NESTED derived source (a derived source over a
/// derived source), so the recursive <c>MakeSelect</c> with <c>ExactProjectionAliases = false</c>
/// (:575) runs a second time and the outermost projection still resolves; <c>MakeRawSqlSource</c>
/// (656) - the parameter loop 663-671 combined with the <c>hasJoins &amp;&amp; IProjection</c>
/// registration arm 686-691 on a JOINED raw source, re-bound to a different parameter value on the
/// same context (A-&gt;B-&gt;A); <c>MakeJoin</c> (186) - the RIGHT/FULL/CROSS spellings plus the
/// per-join table-hint source validation <c>ValidateJoinTableHintSource</c> (:354-355, 0-hit in the
/// baseline) and the physical-table hint gate (:478-479) on the JOINED source; the SQLite
/// <c>INDEXED BY</c> form of <c>MakeFrom</c> (481-487, the <c>Use</c>/<c>Force</c> kind, as opposed
/// to the <c>NOT INDEXED</c> ignore kind) over a real class-owned index; and the
/// <c>SqlBuilder.MakeSelect</c> temporal dispatch gate (:151-157) with a live counterpart.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/SourceRenderingBranchCoverage2Tests.cs</c>
/// owns the CTE read/chain, the database qualifier, the live table expression, the named-parameter raw
/// source (unjoined), the joined raw source WITHOUT parameters, the subquery-hint and table-hint gates
/// on the PRIMARY source, the <c>NOT INDEXED</c> ignore kind, the APPLY dialect gate and the CTE
/// primary-source join; <c>CommonTestSuite.Join</c> owns the plain INNER/LEFT/RIGHT/FULL/CROSS joins
/// and the single-level derived source. This class instead drives the nested derived source, the
/// parameterised JOINED raw source with a changed binding, the per-join table-hint validation (both
/// rejections, each with the live join), the <c>INDEXED BY</c> kind and the temporal gate.
/// </para>
///
/// <para>
/// Recorded, never fabricated: <c>MakeApplyJoin</c> (397) has no reachable arm left on SQLite -
/// <c>SupportsApply</c> is <c>false</c>, so only its gate at :399-400 runs, and that gate is already
/// executed by the cycle-2 class (fact 11); <c>MakeTableExpression</c>'s combined temporal/table-hint
/// rejections and <c>MakeTableFunction</c> are unreachable through the portable public surface used
/// here (no <c>WithTableExpression</c> + hint combination left that the cycle-2 class does not own).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SourceRenderingDifferentialBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists sr4_line;
        drop table if exists sr4_marker;
        create table sr4_marker
        (
            id integer primary key,
            name text not null,
            qty int not null,
            grp int not null,
            tag text null
        );
        create table sr4_line
        (
            id integer primary key,
            marker_id int not null,
            amount int null
        );
        insert into sr4_marker (id, name, qty, grp, tag) values
            (1, 'alpha', 5, 1, 'a'),
            (2, 'beta',  2, 1, 'b'),
            (3, 'gamma', 9, 2, 'a'),
            (4, 'delta', 1, 2, null);
        insert into sr4_line (id, marker_id, amount) values
            (11, 1, 10),
            (12, 1, 20),
            (13, 2, 30),
            (14, 3, 40),
            (15, 99, 50);
        create index ix_sr4_grp on sr4_marker (grp);
        """;

    public SourceRenderingDifferentialBranchCoverage4Tests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------------------------
    // 1. A derived source over a derived source: the nested MakeFrom recursion
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: the derived-subquery branch of <c>MakeFrom</c> (561-593) entered a SECOND
    /// time by a derived source whose own source is derived (the recursive
    /// <c>MakeSelect</c> with <c>ExactProjectionAliases = false</c> at :575), with the outermost
    /// projection resolved through two levels of derived aliases. The neighbours only read one level.
    /// </summary>
    [Fact]
    public void From_NestedDerivedSource_ShouldResolveTwoLevelsAndReadTheOutermostProjection()
    {
        var inner = Context.From<Sr4Marker>()
            .Where(x => x.Grp == 1)
            .Select(x => new { x.Id, x.Qty });

        var middle = Context.From(inner)
            .Where(d => d.Qty > 1)
            .Select(d => new { d.Id, Amount = d.Qty });

        var rows = Context.From(middle)
            .Where(m => m.Amount >= 2)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Amount })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2 }, "only the group-1 markers with qty > 1 survive two derived levels");
        rows.Select(r => r.Amount).Should().Equal(5, 2);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. A parameterised raw source used as a JOINed source, re-bound on the same context
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: <c>MakeRawSqlSource</c>'s parameter loop (663-671) combined with the
    /// <c>hasJoins &amp;&amp; IProjection</c> registration arm (686-691) - the cycle-2 neighbour binds
    /// parameters on an UNJOINED raw source and joins a raw source WITHOUT parameters, so neither
    /// executes both arms together. The same command shape is executed three times on one context with
    /// a changed binding (A-&gt;B-&gt;A) and the live rows must follow the value.
    /// </summary>
    [Fact]
    public void FromSql_ParameterisedJoinedSource_ShouldRebindAndChangeTheRowsOnTheSameContext()
    {
        List<int> Run(int min) => Context
            .FromSql("select id, qty from sr4_marker where qty >= $min", new { min })
            .Join(Context.From<Sr4Line>(), (m, l) => m["id"].AsInt == l.MarkerId)
            .OrderBy(p => p.Item1["id"].AsInt)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1["id"].AsInt)
            .ToList();

        Run(1).Should().Equal(new[] { 1, 1, 2, 3 }, "every marker with lines joins, marker 4 and line 15 have no partner");
        Run(5).Should().Equal(new[] { 1, 1, 3 }, "the rebound value keeps only the markers with qty >= 5");
        Run(1).Should().Equal(new[] { 1, 1, 2, 3 }, "the first binding is rebuilt correctly after the changed one");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Join-type spellings and the per-join table-hint validation
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: <c>MakeJoin</c>'s RIGHT/FULL/CROSS spellings over class-owned rows with
    /// the unmatched side asserted, followed by <c>ValidateJoinTableHintSource</c> (:354-355, 0-hit in
    /// the baseline) rejecting a per-join table hint on a DERIVED join source and the physical-table
    /// gate (:478-479) rejecting it on a physical join source - each rejection paired with the same
    /// join executed live without the hint.
    /// </summary>
    [Fact]
    public void Join_TypeVariationsAndJoinTableHintGuards_ShouldRowCountAndReject()
    {
        var left = Context.From<Sr4Marker>()
            .LeftJoin(Context.From<Sr4Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Id = p.Item1.Id, Line = (int?)p.Item2.Id })
            .ToList();

        left.Should().HaveCount(5, "four markers plus the unmatched line 15");
        left.Count(r => r.Line == null).Should().Be(1, "marker 4 has no line");

        var right = Context.From<Sr4Marker>()
            .RightJoin(Context.From<Sr4Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Marker = (int?)p.Item1.Id, Line = p.Item2.Id })
            .ToList();

        right.Should().HaveCount(5, "four lines plus the unmatched marker 4");
        right.Count(r => r.Marker == null).Should().Be(1, "line 15 has no marker");

        var full = Context.From<Sr4Marker>()
            .FullJoin(Context.From<Sr4Line>(), (m, l) => m.Id == l.MarkerId)
            .Select(p => new { Marker = (int?)p.Item1.Id, Line = (int?)p.Item2.Id })
            .ToList();

        full.Should().HaveCount(6, "the matched pairs plus both unmatched sides");

        var cross = Context.From<Sr4Marker>().CrossJoin(Context.From<Sr4Line>()).Count();
        cross.Should().Be(20, "4 markers x 5 lines");

        // A per-join table hint over a DERIVED join source is rejected before any SQL is assembled.
        var derived = Context.From<Sr4Line>().Where(l => l.Amount != null).Select(l => new { l.Id, l.MarkerId });
        var derivedJoin = Context.From<Sr4Marker>()
            .Join(Context.From(derived), (m, l) => m.Id == l.MarkerId)
            .Select(p => p.Item1.Id)
            .ToList();
        derivedJoin.Should().Equal(new[] { 1, 1, 2, 3 }, "the live counterpart of the rejected shaped join");

        var derivedHint = () => Context.From<Sr4Marker>()
            .Join(Context.From(derived), (m, l) => m.Id == l.MarkerId, j => j.WithJoinTableHint("nolock"))
            .Select(p => p.Item1.Id)
            .ToList();

        derivedHint.Should().Throw<InvalidOperationException>()
            .WithMessage("*only supported on a physical table source*");

        // The same hint on a PHYSICAL join source reaches the dialect gate instead.
        var physical = Context.From<Sr4Marker>()
            .Join(Context.From<Sr4Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Id = p.Item1.Id, Line = p.Item2.Id })
            .ToList();
        physical.Should().HaveCount(4);

        var physicalHint = () => Context.From<Sr4Marker>()
            .Join(Context.From<Sr4Line>(), (m, l) => m.Id == l.MarkerId, j => j.WithJoinTableHint("nolock"))
            .Select(p => p.Item1.Id)
            .ToList();

        physicalHint.Should().Throw<NotSupportedException>()
            .WithMessage("*Table hints are not supported*");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. The INDEXED BY kind of the index-hint renderer (not the NOT INDEXED ignore kind)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: <c>MakeFrom</c>'s index-hint renderer (481-487) with the <c>Use</c> and
    /// <c>Force</c> kinds, which emit <c>indexed by &lt;name&gt;</c> over a real class-owned index;
    /// the cycle-2 neighbour only drives the <c>Ignore</c> kind (<c>NOT INDEXED</c>). Both hinted
    /// reads must return exactly the rows of the unhinted read.
    /// </summary>
    [Fact]
    public void From_IndexHintUseAndForce_ShouldReadExactlyThePlainRows()
    {
        var plain = Context.From<Sr4Marker>()
            .Where(x => x.Grp == 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        plain.Should().Equal(3, 4);

        var used = Context.From<Sr4Marker>(o => o.WithIndex("ix_sr4_grp"))
            .Where(x => x.Grp == 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        used.Should().Equal(new[] { 3, 4 }, "an index hint changes the access path, never the rows");

        var forced = Context.From<Sr4Marker>(o => o.WithIndex(IndexHintKind.Force, "ix_sr4_grp"))
            .Where(x => x.Grp == 2)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        forced.Should().Equal(new[] { 3, 4 }, "the Force kind renders the same SQLite spelling");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. The temporal dispatch gate of MakeSelect, with a live counterpart
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Distinguishing arm: the <c>SqlBuilder.MakeSelect</c> temporal dispatch gate (:151-157) - a
    /// <c>FOR SYSTEM_TIME</c> clause over a plain physical source is rejected by SQLite before any
    /// source is rendered, while the same command shape without the clause reads the marker rows.
    /// </summary>
    [Fact]
    public void ForSystemTime_OnSqlite_ShouldBeRejectedWhileThePlainSourceRuns()
    {
        var plain = Context.From<Sr4Marker>().OrderBy(x => x.Id).Select(x => x.Id).ToList();
        plain.Should().Equal(1, 2, 3, 4);

        var all = () => Context.From<Sr4Marker>()
            .ForSystemTime(TemporalClause.All())
            .Select(x => x.Id)
            .ToList();

        all.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");

        var asOf = () => Context.From<Sr4Marker>()
            .ForSystemTime(TemporalClause.AsOf(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
            .Select(x => x.Id)
            .ToList();

        asOf.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");
    }

    /// <summary>Marker row of the class-owned <c>sr4_marker</c> table.</summary>
    [SqlTable("sr4_marker")]
    public sealed class Sr4Marker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("qty")]
        public int Qty { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public string? Tag { get; set; }
    }

    /// <summary>Line row of the class-owned <c>sr4_line</c> table.</summary>
    [SqlTable("sr4_line")]
    public sealed class Sr4Line
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
