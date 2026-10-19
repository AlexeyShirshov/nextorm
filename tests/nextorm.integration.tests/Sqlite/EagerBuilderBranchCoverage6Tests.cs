using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Eager-load builder coverage (cycle 6, cluster D4) executed against a live SQLite database. Every fact
/// runs a real <c>LoadWith</c>/<c>JoinInto</c>/<c>BindEntity</c> query over class-owned marker rows of
/// <c>eb6_parent</c>, <c>eb6_child</c>, <c>eb6_grand</c>, <c>eb6_note</c>, <c>eb6_link</c> and the
/// binding-shape tables (dropped, created and seeded by this class's own DDL in the constructor). The
/// graph is asymmetric: parent 9603 owns no child at all and child 9612 owns no grandchild. Every guard
/// fact is paired with a live WORKING companion of the same graph shape.
///
/// <para>
/// Targeted arms: <c>Builders/EntityBuilder.cs</c> - <c>UnsupportedChildShapes</c> (:1207) through child
/// factories that carry a <c>JoinInto</c> declaration (<c>Join</c>), a <c>SelectWhereMax</c> terminal
/// (<c>SelectWhereExtreme</c>), a derived <c>As</c> source (<c>derived (As) source</c>), a
/// <c>Distinct</c>+<c>GroupBy</c> combination and a named <c>Window</c>;
/// <c>CreatePairCommand</c> (:1527) over a many-to-many multi-key declaration, two collections on one
/// parent through the async terminal, a filtered child source and an empty child source;
/// <c>get_HasCompositionState</c> (:2432) through <c>BindEntity</c> composition states that the earlier
/// raw-source guard suite never named (<c>IgnoreFilters</c>, <c>SelectWhereMax</c>, <c>TableHints</c>,
/// <c>ForSystemTime</c>, <c>WithIndex</c>, source <c>ForShare</c> and <c>TableSample</c>) next to live
/// bindings of a flat scalar, a reference-navigation, a collection-navigation, an inherited-member and a
/// nullable-composite entity.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>EagerBuilderBranchCoverage5Tests</c> owns the
/// three-level split/default/async graph, the explicit SplitQuery child ordering, the descendant-filter
/// fold with <c>EagerLoadNestedJoinIntoSpec</c>, per-parent top-N with a nested graph, the nested
/// <c>:348</c>/<c>:366</c> guards, the flat <c>Distinct</c> guard, a live <c>AliasRoot</c> re-root, the
/// pair command over two collections and over a paged parent, and the descendant <c>GroupBy</c>/child
/// <c>OrderBy</c> rejections. <c>EagerGraphBranchCoverage4Tests</c> owns the filtered-child mode matrix,
/// the childless middle in single-query mode, per-parent paging over unique keys, SingleQuery+JoinInto
/// and the flat/nested <c>GroupBy</c>/<c>TablesInScopeHint</c>/<c>OrderBy,Distinct</c> guards.
/// <c>RawSourceBindingGuardTests</c> owns the predicate/distinct/paging/order/group/having/window/row-lock
/// and table/schema/database/server/in-scope-hint composition states of <c>BindEntity</c>. This class
/// instead drives the five child shapes and the composition states those suites leave uncovered.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class EagerBuilderBranchCoverage6Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists eb6_link;
        drop table if exists eb6_note;
        drop table if exists eb6_grand;
        drop table if exists eb6_child;
        drop table if exists eb6_parent;
        drop table if exists eb6_ref;
        drop table if exists eb6_ref_parent;
        drop table if exists eb6_coll_item;
        drop table if exists eb6_coll_parent;
        drop table if exists eb6_inherited;
        drop table if exists eb6_nullable;
        create table eb6_parent
        (
            id integer primary key,
            name text not null
        );
        create table eb6_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null,
            kind text not null
        );
        create table eb6_grand
        (
            id integer primary key,
            child_id int not null,
            sort_key int not null
        );
        create table eb6_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        create table eb6_link
        (
            id integer primary key,
            parent_id int not null,
            note_id int not null
        );
        create table eb6_ref_parent
        (
            id integer primary key,
            ref_id int not null
        );
        create table eb6_ref
        (
            id integer primary key,
            label text not null
        );
        create table eb6_coll_parent
        (
            id integer primary key,
            name text not null
        );
        create table eb6_coll_item
        (
            id integer primary key,
            parent_id int not null
        );
        create table eb6_inherited
        (
            id integer primary key,
            name text not null
        );
        create table eb6_nullable
        (
            tenant int null,
            id integer not null,
            name text not null
        );
        insert into eb6_parent (id, name) values
            (9601, 'p-many'),
            (9602, 'p-one'),
            (9603, 'p-none');
        -- 9601: two kept children plus one skipped child; 9602: one kept child; 9603: no child at all.
        insert into eb6_child (id, parent_id, sort_key, kind) values
            (9611, 9601, 1, 'keep'),
            (9612, 9601, 2, 'keep'),
            (9613, 9601, 3, 'skip'),
            (9621, 9602, 1, 'keep');
        -- Child 9611 owns two grandchildren; child 9612 owns none (a childless middle level).
        insert into eb6_grand (id, child_id, sort_key) values
            (9631, 9611, 2),
            (9632, 9611, 1),
            (9641, 9621, 1);
        insert into eb6_note (id, parent_id, body) values
            (9651, 9601, 'n1'),
            (9652, 9601, 'n2'),
            (9653, 9602, 'n3');
        -- 9601 owns three link rows over two notes (a repeated pair); 9602 owns one; 9603 owns none.
        insert into eb6_link (id, parent_id, note_id) values
            (9661, 9601, 9651),
            (9662, 9601, 9652),
            (9663, 9601, 9651),
            (9664, 9602, 9653);
        insert into eb6_ref (id, label) values
            (9681, 'r1'),
            (9682, 'r2');
        insert into eb6_ref_parent (id, ref_id) values
            (9671, 9681),
            (9672, 9682),
            (9673, 9681);
        insert into eb6_coll_parent (id, name) values
            (9691, 'c-many'),
            (9692, 'c-one');
        insert into eb6_coll_item (id, parent_id) values
            (9695, 9691),
            (9696, 9691),
            (9697, 9692);
        insert into eb6_inherited (id, name) values
            (9701, 'alpha'),
            (9702, 'beta');
        insert into eb6_nullable (tenant, id, name) values
            (1, 9711, 'n1'),
            (null, 9712, 'n2'),
            (2, 9713, 'n3');
        create index eb6_parent_name_idx on eb6_parent (name);
        """;

    public EagerBuilderBranchCoverage6Tests()
    {
        ExecuteRaw(Ddl);

        // The graph relationships must be registered before any mapping of the parent types, so the
        // collection members are recognized as navigations and not as physical columns.
        Context.From<Eb6Grand>();
        Context.From<Eb6Ref>();
        Context.From<Eb6CollItem>();
        Context.From<Eb6Note>();
        Context.From<Eb6Link>();
        Context.From<Eb6Child>(b => b.HasMany(c => c.Grands!, g => g.ChildId));
        Context.From<Eb6Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId)
            .HasManyThrough<Eb6Note, Eb6Link, int, int>(
                p => p.LinkedNotes, p => p.Id, l => l.ParentId, n => n.Id, l => l.NoteId));
        Context.From<Eb6RefParent>(b => b.HasOneToOne(p => p.Ref, p => p.RefId, r => r.Id));
        Context.From<Eb6CollParent>(b => b.HasMany(p => p.Items, i => i.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>The parent/child/grand binding rendered as <c>parent:child[grands]|child[grands]</c>.</summary>
    private static List<string> Bindings(List<Eb6Parent> parents) => parents
        .Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id + "[" + GrandIds(c) + "]")))
        .ToList();

    /// <summary>The class-owned grandchildren of one child, in the seed's sort-key order.</summary>
    private static string GrandIds(Eb6Child child)
        => child.Grands is null
            ? string.Empty
            : string.Join(",", child.Grands.OrderBy(g => g.SortKey).Select(g => g.Id));

    // ---------------------------------------------------------------------------------------------
    // 1. UnsupportedChildShapes: a child factory carrying a JoinInto declaration
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory that carries its own <c>JoinInto</c> declaration reports the
    /// <c>Join</c> shape of <c>UnsupportedChildShapes</c> (:1234) to the single-query fold, so the fold
    /// fails closed before any SQL; the split-mode companion of the same child source is live and keeps
    /// the childless parent and the grandchildless middle child.
    /// </summary>
    [Fact]
    public void LoadWith_ChildCarryingJoinIntoInSingleQuery_ShouldRejectJoinWithLiveSplitCompanion()
    {
        var live = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(
            new[] { "9601:9611[]|9612[]", "9602:9621[]", "9603:" },
            "the split companion of the rejected child source is live");

        var rejected = () => Context.From<Eb6Parent>()
            .Where(p => p.Id == 9601)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().JoinInto(
                    c.From<Eb6Grand>(),
                    (ch, g) => ch.Id == g.ChildId,
                    ch => ch.Grands!,
                    ch => ch.Id,
                    g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's Join*");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. UnsupportedChildShapes: a child factory carrying an aggregate/Select terminal
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory whose terminal is <c>SelectWhereMax</c> carries the
    /// <c>SelectWhereExtreme</c> shape (:1229) - an aggregate/Select terminal the single-query fold
    /// cannot render. The split-mode companion of the same three-level source returns exact rows.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithAggregateTerminalInSingleQuery_ShouldRejectExtremeRowWithLiveCompanion()
    {
        var live = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().Where(x => x.Kind == "keep").OrderBy(x => x.SortKey),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(new[] { "9601:9611[]|9612[]", "9602:9621[]", "9603:" },
            "the split companion runs the same child source without the aggregate terminal");

        var rejected = () => Context.From<Eb6Parent>()
            .Where(p => p.Id == 9601)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().SelectWhereMax(x => x.SortKey),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's SelectWhereExtreme*");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. UnsupportedChildShapes: a child factory whose source is a derived (As) source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory derived through <c>As</c> carries its condition inside the
    /// subquery, so <c>UnsupportedChildShapes</c> (:1280) reports <c>derived (As) source</c>; the live
    /// split companion resolves the same graph.
    /// </summary>
    [Fact]
    public void LoadWith_DerivedChildSourceInSingleQuery_ShouldRejectDerivedWithLiveCompanion()
    {
        var live = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(new[] { "9601:9611[]|9612[]", "9602:9621[]", "9603:" },
            "the split companion binds the same child rows");

        var rejected = () => Context.From<Eb6Parent>()
            .Where(p => p.Id == 9601)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().As(x => new Eb6Child
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    SortKey = x.SortKey,
                    Kind = x.Kind,
                }),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's derived (As) source*");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. UnsupportedChildShapes: a distinct+grouping combination on a NESTED child
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the nested branch of the single-query fold rejects a child that carries both
    /// <c>Distinct</c> (:1216) and <c>GroupBy</c> (:1219), reported as the two-name chain
    /// <c>Distinct, GroupBy</c>; the live nested split companion stitches both levels and keeps the
    /// grandchildless middle child empty.
    /// </summary>
    [Fact]
    public void LoadWith_NestedChildWithDistinctAndGroupingInSingleQuery_ShouldRejectCombinationWithLiveCompanion()
    {
        var live = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>()
                    .Where(x => x.Kind == "keep")
                    .OrderBy(x => x.SortKey)
                    .LoadWith(x => x.Grands!, g => g.From<Eb6Grand>().OrderBy(x => x.SortKey), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(
            new[] { "9601:9611[9632,9631]|9612[]", "9602:9621[9641]", "9603:" },
            "the nested split companion loads both levels of the rejected graph");

        var rejected = () => Context.From<Eb6Parent>()
            .Where(p => p.Id == 9601)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>()
                    .Where(x => x.Kind == "keep")
                    .Distinct()
                    .GroupBy(x => x.ParentId)
                    .LoadWith(x => x.Grands!, g => g.From<Eb6Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's Distinct, GroupBy*");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. UnsupportedChildShapes: a named window on the child
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory carrying a named window reports the <c>Window</c> shape
    /// (:1262) to the single-query fold; the live split companion of the same child source is exact.
    /// </summary>
    [Fact]
    public void LoadWith_ChildCarryingWindowInSingleQuery_ShouldRejectWindowWithLiveCompanion()
    {
        var live = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(new[] { "9601:9611[]|9612[]", "9602:9621[]", "9603:" },
            "the split companion of the windowed child source is live");

        var rejected = () => Context.From<Eb6Parent>()
            .Where(p => p.Id == 9601)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb6Child>().Window("eb6_w", new Expression<Func<Eb6Child, object?>>[] { x => x.Kind }),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's Window*");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. CreatePairCommand: a many-to-many multi-key declaration
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>CreatePairCommand</c> (:1527) takes the many-to-many branch
    /// (:1537-1549): the junction link item type and its re-published mapping drive one denormalized
    /// command whose repeated link pair stays repeated and whose parent without any link row keeps an
    /// empty collection. The earlier pair facts only used direct one-to-many declarations.
    /// </summary>
    [Fact]
    public void JoinInto_ManyToMany_ShouldStitchMultiKeyPairsAndKeepParentWithoutLinks()
    {
        var rows = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .JoinInto(Context.From<Eb6Note>(), (p, n) => n.Id >= 9651, p => p.LinkedNotes)
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(new[] { 9601, 9602, 9603 },
            "the pair command deduplicates the repeated parent rows");
        rows[0].LinkedNotes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 9651, 9652, 9651 },
            "the junction keeps one link occurrence per junction row");
        rows[0].LinkedNotes.Should().OnlyContain(n => n.Id == 9651 || n.Id == 9652);
        rows[1].LinkedNotes.Select(n => n.Id).Should().Equal(9653);
        rows[2].LinkedNotes.Should().BeEmpty("a parent without a junction row keeps an empty collection");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. CreatePairCommand: two collections on one parent through the async terminal
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: two <c>JoinInto</c> collections on one parent are stitched through the
    /// ASYNC pair-command terminal (<c>MaterializeListAsync</c> :570-580), not the synchronous one the
    /// earlier pair fact used; the cartesian product is deduplicated per collection and the childless
    /// parent keeps both collections empty.
    /// </summary>
    [Fact]
    public async Task JoinInto_TwoCollectionsAsync_ShouldStitchAndDeduplicate()
    {
        var rows = await Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .JoinInto(Context.From<Eb6Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(Context.From<Eb6Note>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .OrderBy(p => p.Id)
            .ToListAsync();

        rows.Select(p => p.Id).Should().Equal(new[] { 9601, 9602, 9603 });
        rows[0].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 9611, 9612, 9613 });
        rows[0].Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 9651, 9652 });
        rows[0].Children.Should().OnlyContain(c => c.ParentId == 9601);
        rows[0].Notes.Should().OnlyContain(n => n.ParentId == 9601);
        rows[1].Children.Select(c => c.Id).Should().Equal(9621);
        rows[1].Notes.Select(n => n.Id).Should().Equal(9653);
        rows[2].Children.Should().BeEmpty("the childless parent stays empty under the cartesian product");
        rows[2].Notes.Should().BeEmpty("the note collection is independent of the child collection");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. CreatePairCommand: a filtered child source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the joined child source is a DERIVED query carrying its own <c>Where</c>, so
    /// <c>CreatePairCommand</c> renders the subquery as the join source and stitches only the matching
    /// children; the skipped child and the parent with no matching child are asserted by value.
    /// </summary>
    [Fact]
    public void JoinInto_FilteredChildSource_ShouldStitchOnlyMatchingRows()
    {
        var filteredChild = Context.From(Context.From<Eb6Child>()
            .Where(x => x.Kind == "skip")
            .ToCommand());

        var rows = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .JoinInto(filteredChild, (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(new[] { 9601, 9602, 9603 });
        rows[0].Children.Select(c => c.Id).Should().Equal(new[] { 9613 },
            "only the child surviving the derived source's own Where is stitched");
        rows[1].Children.Should().BeEmpty("9602 owns only a kept child, filtered out by the source");
        rows[2].Children.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // 9. CreatePairCommand: an empty child source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a derived child source whose <c>Where</c> matches no row still produces one
    /// row per parent: <c>CreatePairCommand</c> keeps the outer join's NULL child and the stitcher leaves
    /// every collection empty instead of dropping or repeating parents.
    /// </summary>
    [Fact]
    public void JoinInto_EmptyChildSource_ShouldKeepEveryParentWithEmptyCollection()
    {
        var emptyChild = Context.From(Context.From<Eb6Child>()
            .Where(x => x.Kind == "absent")
            .ToCommand());

        var rows = Context.From<Eb6Parent>()
            .Where(p => p.Id >= 9601 && p.Id <= 9603)
            .JoinInto(emptyChild, (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(new[] { 9601, 9602, 9603 },
            "the pair command keeps every parent of an empty child source");
        rows.Should().OnlyContain(p => p.Children.Count == 0,
            "an empty child source stitches no child onto any parent");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. HasCompositionState: flat scalar binding, plus filter-scope/extreme/table-hint states
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the flat scalar entity binds a direct named source and executes, proving the
    /// fully false short-circuit of <c>HasCompositionState</c>; then the <c>IgnoreFilters</c> filter
    /// scope (:2454), the <c>SelectWhereMax</c> extreme row (:2452) and a from-option table hint
    /// (:2459) each reject a later <c>BindEntity</c> through a different arm of the same chain.
    /// </summary>
    [Fact]
    public void BindEntity_FlatScalarSource_ShouldExecuteAndRejectFilterScopeExtremeAndHintCompositions()
    {
        var ids = Context.From("eb6_parent")
            .BindEntity<Eb6Flat>(new[] { "id", "name" })
            .Where(x => x.Id >= 9601 && x.Id <= 9603)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(new[] { 9601, 9602, 9603 },
            "a plain named source binds and executes through the fully false composition chain");

        FluentActions
            .Invoking(() => Context.From("eb6_parent")
                .IgnoreFilters()
                .BindEntity<Eb6Flat>(new[] { "id", "name" }))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*BindEntity must be called immediately after FromSql/From(string)*");

        FluentActions
            .Invoking(() => Context.From("eb6_parent")
                .SelectWhereMax(t => t["id"].AsInt)
                .BindEntity<Eb6Flat>(new[] { "id", "name" }))
            .Should().Throw<InvalidOperationException>();

        FluentActions
            .Invoking(() => Context.From("eb6_parent", o => o.WithTableHint("NOLOCK"))
                .BindEntity<Eb6Flat>(new[] { "id", "name" }))
            .Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------------------------------
    // 11. HasCompositionState: a reference-navigation entity, plus the temporal state
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: an entity whose reference navigation is a declared one-to-one binds the same
    /// way and returns exact rows; the temporal <c>ForSystemTime</c> state (:2456) rejects the binding
    /// through its own arm of <c>HasCompositionState</c>.
    /// </summary>
    [Fact]
    public void BindEntity_ReferenceNavigationEntity_ShouldExecuteAndRejectTemporalComposition()
    {
        var ids = Context.From("eb6_ref_parent")
            .BindEntity<Eb6RefParent>(new[] { "id", "ref_id" })
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(new[] { 9671, 9672, 9673 },
            "the reference-navigation entity binds and executes over its raw source");

        FluentActions
            .Invoking(() => Context.From("eb6_ref_parent")
                .ForSystemTime(TemporalClause.AsOf(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
                .BindEntity<Eb6RefParent>(new[] { "id", "ref_id" }))
            .Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------------------------------
    // 12. HasCompositionState: a collection-navigation entity, plus the index-hint state
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: an entity whose collection navigation is a declared one-to-many binds and
    /// returns exact rows; a from-option index hint (:2460) rejects the binding through its own arm of
    /// <c>HasCompositionState</c>.
    /// </summary>
    [Fact]
    public void BindEntity_CollectionNavigationEntity_ShouldExecuteAndRejectIndexHintComposition()
    {
        var ids = Context.From("eb6_coll_parent")
            .BindEntity<Eb6CollParent>(new[] { "id", "name" })
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(new[] { 9691, 9692 },
            "the collection-navigation entity binds and executes over its raw source");

        FluentActions
            .Invoking(() => Context.From("eb6_coll_parent", o => o.WithIndex("eb6_parent_name_idx"))
                .BindEntity<Eb6CollParent>(new[] { "id", "name" }))
            .Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------------------------------
    // 13. HasCompositionState: an inherited/base-member entity, plus the source-lock state
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: an entity inheriting its mapped members from a base class binds and returns
    /// exact rows; a from-option source <c>ForShare</c> lock (:2458) rejects the binding through its own
    /// arm of <c>HasCompositionState</c>.
    /// </summary>
    [Fact]
    public void BindEntity_InheritedMemberEntity_ShouldExecuteAndRejectSourceLockComposition()
    {
        var names = Context.From("eb6_inherited")
            .BindEntity<Eb6Inherited>(new[] { "id", "name" })
            .OrderBy(x => x.Id)
            .Select(x => x.Name)
            .ToList();

        names.Should().Equal(new[] { "alpha", "beta" },
            "the inherited base member resolves through the bound raw source");

        FluentActions
            .Invoking(() => Context.From("eb6_inherited", o => o.ForShare())
                .BindEntity<Eb6Inherited>(new[] { "id", "name" }))
            .Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------------------------------
    // 14. HasCompositionState: a nullable composite entity, plus the table-sample state
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: an entity whose composite key carries a nullable component binds and returns
    /// exact rows including the NULL key component; a from-option <c>TABLESAMPLE</c> clause (:2455)
    /// rejects the binding through its own arm of <c>HasCompositionState</c>.
    /// </summary>
    [Fact]
    public void BindEntity_NullableCompositeEntity_ShouldExecuteAndRejectTableSampleComposition()
    {
        var keys = Context.From("eb6_nullable")
            .BindEntity<Eb6Nullable>(new[] { "tenant", "id", "name" })
            .OrderBy(x => x.Id)
            .Select(x => new { x.TenantId, x.Id })
            .ToList();

        keys.Select(k => k.Id).Should().Equal(new[] { 9711, 9712, 9713 },
            "the composite-key entity binds and returns every row, including the NULL key component");
        keys[1].TenantId.Should().BeNull("the nullable composite component is materialized as NULL");

        FluentActions
            .Invoking(() => Context.From("eb6_nullable", o => o.TableSample(10, TableSampleMethod.System, null))
                .BindEntity<Eb6Nullable>(new[] { "tenant", "id", "name" }))
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>Parent of the class-owned eager graph.</summary>
    [SqlTable("eb6_parent")]
    public sealed class Eb6Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Eb6Child> Children { get; } = new List<Eb6Child>();

        public ICollection<Eb6Note> Notes { get; } = new List<Eb6Note>();

        public ICollection<Eb6Note> LinkedNotes { get; } = new List<Eb6Note>();
    }

    /// <summary>Child of the class-owned eager graph, with a per-parent sort key and a kind filter.</summary>
    [SqlTable("eb6_child")]
    public sealed class Eb6Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }

        [Column("kind")]
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Settable and null-initialized on purpose: the split loader must assign a fresh list through the
        /// member setter (the setterless path is covered by the parent's read-only <c>Children</c> member).
        /// </summary>
        public ICollection<Eb6Grand>? Grands { get; set; }
    }

    /// <summary>Grandchild of the class-owned eager graph (a third distinct CLR type).</summary>
    [SqlTable("eb6_grand")]
    public sealed class Eb6Grand
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("child_id")]
        public int ChildId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }

    /// <summary>Note collection of the parent, filled through a direct and a many-to-many declaration.</summary>
    [SqlTable("eb6_note")]
    public sealed class Eb6Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>Junction linking a parent to a note for the many-to-many declaration.</summary>
    [SqlTable("eb6_link")]
    public sealed class Eb6Link
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("note_id")]
        public int NoteId { get; set; }
    }

    /// <summary>Flat scalar entity bound to the class-owned parent table.</summary>
    [SqlTable("eb6_parent")]
    public sealed class Eb6Flat
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Entity with a reference navigation, resolved as a declared one-to-one.</summary>
    [SqlTable("eb6_ref_parent")]
    public sealed class Eb6RefParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ref_id")]
        public int RefId { get; set; }

        public Eb6Ref? Ref { get; set; }
    }

    /// <summary>Referenced side of the one-to-one navigation.</summary>
    [SqlTable("eb6_ref")]
    public sealed class Eb6Ref
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Entity with a collection navigation, resolved as a declared one-to-many.</summary>
    [SqlTable("eb6_coll_parent")]
    public sealed class Eb6CollParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Eb6CollItem> Items { get; } = new List<Eb6CollItem>();
    }

    /// <summary>Collection item of the one-to-many navigation.</summary>
    [SqlTable("eb6_coll_item")]
    public sealed class Eb6CollItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    /// <summary>Base class carrying the mapped members of the inherited binding entity.</summary>
    public abstract class Eb6Base
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Binding entity whose mapped members are all inherited from <see cref="Eb6Base"/>.</summary>
    [SqlTable("eb6_inherited")]
    public sealed class Eb6Inherited : Eb6Base
    {
    }

    /// <summary>Binding entity with a composite key whose first component is nullable.</summary>
    [SqlTable("eb6_nullable")]
    public sealed class Eb6Nullable
    {
        [Key]
        [Column("tenant")]
        public int? TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
