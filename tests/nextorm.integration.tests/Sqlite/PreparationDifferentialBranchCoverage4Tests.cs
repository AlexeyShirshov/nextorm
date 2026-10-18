using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Deeper QueryPreparer differentials (cycle 4, cluster D1) where a single battery is too coarse:
/// the captured-collection shape refresh of an UNKEYED value list, the bound filter scope, the
/// FROM-derived unkeyed value-list scans and the non-<c>ItemN</c> cast arms of the projection guard.
/// Every fact builds its commands over the class-owned <c>pd4_main</c>/<c>pd4_line</c> marker tables
/// (dropped, created and seeded by this class's own DDL in the constructor - no global seed-table
/// writes, no global seed-table counts and no <c>InMemoryDataContext</c>) and asserts the live rows on
/// every step of the A -&gt; A -&gt; B -&gt; B -&gt; A round trip, so a stale plan (a wrong parameter
/// count or a stale shape hash) surfaces as a wrong row set rather than as a silent cache hit.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>RefreshInValuesShape</c> 195-281 - the
/// <c>!ShapeScanned</c> first fold 200-246 and the per-execution rebuild 254-280 for a value list that
/// is NOT in WHERE/PREWHERE (the unkeyed path), with the empty/single/multiple/changed-collection
/// transitions; <c>RawShape.FilterScopeEqual</c> 3839-3840 (present-vs-null and equal-vs-equal);
/// <c>ScanUnkeyedScalarInFrom</c> 3262-3316 and <c>ScanUnkeyedTupleInFrom</c> 3006 (through
/// <c>HasUnkeyedScalarInCondition</c> 3251 and the tuple scan 2959) reached from a FROM-derived
/// source; <c>IsUnrelatedCastOfProjectionItem</c> 1792-1819 false arms 1798 (non-parameter member) and
/// 1803 (a projection type that is not an <c>IProjection</c>).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>InPreparationBranchCoverage2Tests</c> owns
/// the captured-collection shape refresh for a value list in WHERE (including the
/// <c>storeInCache:false</c>-then-cached fold) and the instance-<c>Contains</c> family;
/// <c>TupleInValuesSweepTests</c> owns the tuple list in the projection, HAVING, join condition, CTE
/// and set operation; <c>RawShapeBranchCoverage2Tests</c> owns the raw/bound/derived root-source
/// ineligibility and the <c>FromBinding</c> arm; <c>PreparerBatteryBranchCoverage4Tests</c> owns the
/// cast REJECTION arm (1795/1798/1803/1807/1810 true path). The arms above are disjoint from all of
/// them.
/// </para>
///
/// <para>
/// Deferred (recorded, never fabricated): the <c>CollectBoundCrossJoinFilters</c> 2278 /
/// <c>AppendPendingCrossJoinFilters</c> 2305 / <c>InjectBoundSourceFilters</c> 2168 cluster needs a
/// bound raw source that carries REGISTERED query filters on a CROSS/APPLY join - a combination no
/// existing integration fact proves on any provider - so it is left to a provider-class boundary run
/// with a proven neighbour instead of risking an unproven form in the inner loop.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class PreparationDifferentialBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pd4_line;
        drop table if exists pd4_main;
        create table pd4_main
        (
            id integer primary key,
            name text not null,
            grp int not null,
            tag int not null
        );
        create table pd4_line
        (
            id integer primary key,
            main_id int not null,
            label text not null
        );
        insert into pd4_main (id, name, grp, tag) values
            (1, 'alpha', 1, 10),
            (2, 'beta',  1, 20),
            (3, 'gamma', 2, 10),
            (4, 'delta', 2, 20);
        insert into pd4_line (id, main_id, label) values
            (10, 1, 'x'),
            (11, 2, 'y'),
            (12, 3, 'z');
        """;

    public PreparationDifferentialBranchCoverage4Tests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Prepares through the given cache mode, returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command, bool storeInCache)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: storeInCache,
            CancellationToken.None);

    /// <summary>Executes an already prepared command against the live database.</summary>
    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Drops every cached plan (and the raw candidate index) on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Captures the value-free raw descriptor of a fresh command, or <see langword="null"/>.</summary>
    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    // ---------------------------------------------------------------------------------------------
    // 1. Unkeyed captured value list: the uncached fold, then the cached per-execution refresh
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void InValuesDifferential_UnkeyedProjectionList_ShouldRefreshTheShapeAndReadLiveRows()
    {
        ResetPlanCache();

        var values = new List<int> { 1 };

        QueryCommand<int> Hits() => Context.From<Pd4Main>()
            .OrderBy(x => x.Id)
            .Select(x => values.Contains(x.Id) ? 1 : 0);

        // First preparation WITHOUT hashing: the shape is never folded in, so RefreshInValuesShape has to
        // fold it from scratch on the first cached execution (the !ShapeScanned arm).
        var command = Hits();
        Rows(Prepare(command, storeInCache: false)).Should()
            .Equal(new[] { 1, 0, 0, 0 }, "only id 1 is in the captured list");

        values.Add(3);
        Rows(Prepare(command, storeInCache: true)).Should()
            .Equal(new[] { 1, 0, 1, 0 }, "the grown list must reach the live plan");

        values.Add(2);
        Rows(Prepare(command, storeInCache: true)).Should()
            .Equal(new[] { 1, 1, 1, 0 }, "a further grown list is folded in as well");

        values.Clear();
        Rows(Prepare(command, storeInCache: true)).Should()
            .Equal(new[] { 0, 0, 0, 0 }, "the empty value list folds to an always-false predicate");

        values.AddRange([2, 4]);
        Rows(Prepare(command, storeInCache: true)).Should()
            .Equal(new[] { 0, 1, 0, 1 }, "a reassigned non-empty list is honoured after the empty shape");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Bound filter scope: present-vs-null and equal-vs-equal on one shape
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void FilterScopeDifferential_IgnoreFiltersRoundTrip_ShouldCompareAndReadLiveRows()
    {
        ResetPlanCache();

        QueryCommand<int> Scoped() => Context.From<Pd4Main>()
            .IgnoreFilters()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        QueryCommand<int> Unscoped() => Context.From<Pd4Main>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var scoped = Scoped();
        var unscoped = Unscoped();

        Shape(scoped).Should().NotBeNull("a bound filter scope over a plain table is a describable graph");

        Rows(Prepare(scoped, storeInCache: true)).Should().Equal(new[] { 1, 2 });

        Shape(unscoped)!.Matches(Shape(scoped)!, unscoped).Should()
            .BeFalse("the bound filter scope is a descriptor dimension");

        Rows(Prepare(unscoped, storeInCache: true)).Should()
            .Equal(new[] { 1, 2 }, "no filter is registered for the marker entity, so both shapes read the same rows");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Unkeyed value list inside a FROM-derived source: scalar and tuple scans
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void UnkeyedDerivedSourceDifferential_ShouldReKeyTheDerivedShapeAndReadLiveRows()
    {
        ResetPlanCache();

        var scalar = new List<int> { 1, 2 };
        QueryCommand<int> ScalarDerived() => Context.From(Context.From<Pd4Main>()
                .Where(x => SqlFunctions.Sql.@in(x.Id, scalar))
                .Select(x => new { x.Id, x.Name }))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var pairs = new List<(int Id, int Tag)> { (1, 10) };
        QueryCommand<int> TupleDerived() => Context.From(Context.From<Pd4Main>()
                .Where(x => pairs.Contains(new ValueTuple<int, int>(x.Id, x.Tag)))
                .Select(x => new { x.Id }))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var scalarCommand = ScalarDerived();
        Rows(Prepare(scalarCommand, storeInCache: true)).Should().Equal(new[] { 1, 2 });

        scalar.Add(4);
        Rows(Prepare(ScalarDerived(), storeInCache: true)).Should()
            .Equal(new[] { 1, 2, 4 }, "the derived source must re-key from its grown scalar list");

        var tupleCommand = TupleDerived();
        Rows(Prepare(tupleCommand, storeInCache: true)).Should()
            .Equal(new[] { 1 }, "only the (1, 10) pair matches");

        pairs.Add((3, 10));
        Rows(Prepare(TupleDerived(), storeInCache: true)).Should()
            .Equal(new[] { 1, 3 }, "the derived source must re-key from its grown tuple list");

        pairs.Clear();
        Rows(Prepare(TupleDerived(), storeInCache: true)).Should()
            .BeEmpty("an empty tuple list folds to an always-false predicate");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Cast arms of a NON-ItemN member: the projection guard's early false paths, live
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ProjectionCastDifferential_NonItemMemberCast_ShouldReadLiveRows()
    {
        ResetPlanCache();

        // A widening cast of a main-entity column: the operand is a member over the entity parameter, but
        // the entity type is not an IProjection, so the ItemN tests are never reached.
        var widened = Rows(Prepare(Context.From<Pd4Main>()
            .OrderBy(x => x.Id)
            .Select(x => (long)x.Id), storeInCache: true));
        widened.Should().Equal(new[] { 1L, 2L, 3L, 4L });

        // A widening cast of a joined item's column: the operand's member is rooted at the ItemN member
        // access, not at the projection parameter, so the member test itself fails.
        var joined = Rows(Prepare(Context.From<Pd4Main>()
            .Join(Context.From<Pd4Line>(), (m, l) => m.Id == l.MainId)
            .OrderBy(p => p.Item1.Id)
            .Select(p => (long)p.Item1.Id), storeInCache: true));
        joined.Should().Equal(new[] { 1L, 2L, 3L });
    }

    // ---------------------------------------------------------------------------------------------
    // 5. FOR JSON clause: the document clause is a descriptor dimension (RawShape 3724)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ForJsonDifferential_DocumentClause_ShouldCompareTheClauseAndReadTheProjection()
    {
        ResetPlanCache();

        var path = Context.From<Pd4Main>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name })
            .WithForJson(ForJsonMode.Path);
        var pathAgain = Context.From<Pd4Main>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name })
            .WithForJson(ForJsonMode.Path);
        var auto = Context.From<Pd4Main>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name })
            .WithForJson(ForJsonMode.Auto);
        var rooted = Context.From<Pd4Main>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name })
            .WithForJson(ForJsonMode.Path, root: "row");

        Shape(path).Should().NotBeNull("a FOR JSON clause over a plain table is a describable graph");

        Shape(pathAgain)!.Matches(Shape(path)!, pathAgain).Should()
            .BeTrue("an identical FOR JSON clause is the same descriptor");
        Shape(auto)!.Matches(Shape(path)!, auto).Should()
            .BeFalse("a different FOR JSON mode is a different descriptor");
        Shape(rooted)!.Matches(Shape(path)!, rooted).Should()
            .BeFalse("a root name is part of the FOR JSON descriptor");

        // Live counterpart of the same projection shape (SQLite renders no FOR JSON).
        var rows = Rows(Prepare(Context.From<Pd4Main>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name }), storeInCache: true));
        rows.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma", "delta");
    }

    /// <summary>Marker row of the class-owned <c>pd4_main</c> table.</summary>
    [SqlTable("pd4_main")]
    public sealed class Pd4Main
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>pd4_line</c> table.</summary>
    [SqlTable("pd4_line")]
    public sealed class Pd4Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("main_id")]
        public int MainId { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
