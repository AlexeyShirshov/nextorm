using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Eager-load graph planning coverage (cycle 4, cluster D4) executed against a live SQLite database.
/// Every fact runs a real <c>LoadWith</c> graph over class-owned marker rows (<c>g4_parent</c>,
/// <c>g4_child</c>, <c>g4_grand</c>, <c>g4_note</c>, dropped/created/seeded by this class's own DDL in the
/// constructor) and asserts parent-to-child binding, cardinality, the absence of cross-parent leakage and
/// the child ordering where the specification fixes it. Guard facts are always paired with a live
/// counterpart of the same graph shape.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>IEagerLoadSpec&lt;TEntity&gt;.PlanSingleQuery</c>
/// (<c>src/nextorm.core/Builders/EntityBuilderEagerLoading.cs:154</c>, explicit implementation
/// <c>EagerLoadSpec&lt;TEntity,TChild,TKey&gt;</c> :338) - the level-one fold :351-362 with the child's own
/// <c>Condition</c>/<c>FilterScope</c> folded in (:355, :377), the nested branch :366-402
/// (<c>UnsupportedSingleQueryChildShapes</c> :366, <c>ResolveSource</c> :373, the descendant recursion with
/// the accumulated <c>baseItemNumber</c> :383-393, the item-type cap :395), and the adjacent public graph
/// paths <c>Execute</c> :893, <c>ExecuteLoadedChildren</c> :918, <c>ExecuteAsync</c> :934,
/// <c>ExecuteLoadedChildrenAsync</c> :952, <c>HasPerParentTopN</c> :974,
/// <c>PerParentTopNNotSupported</c> :982, <c>ValidatePerParentPagingChild</c> :1000,
/// <c>ValidateLoadWithForJoinInto</c> :1051, plus <c>BuildSingleQueryJoins</c>
/// (<c>Builders/EntityBuilder.cs:1412</c>) and its regular-join rejection :1414-1416.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>EagerModeBranchCoverage2Tests</c> owns
/// Default/explicit-Single/explicit-Split/Auto equivalence, the per-parent page with strictly increasing
/// child ids, the nested two-level split/single graphs, the Correlated / non-list-terminal / WithTies /
/// without-OrderBy guards and the Distinct / nested-paged / nested-Distinct / mixed-mode / cyclic guards.
/// <c>ExtremaEagerBranchCoverage3Tests</c> owns the mode-dependent child ORDER on an ordered child, the
/// all-tied per-parent page with a childless parent, the LoadWith+JoinInto combination with the per-parent
/// rejection, and the loader plumbing no-op. This class instead drives a FILTERED child (the folded
/// condition and child filter scope), a two-level graph whose middle level has a childless child plus an
/// async twin, a per-parent page over UNIQUE descending sort keys with an offset shape (distinguishable
/// order), the SingleQuery + JoinInto stitching, and the unsupported-child-shape chains of the nested and
/// flat folds with a live counterpart each.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class EagerGraphBranchCoverage4Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists g4_grand;
        drop table if exists g4_note;
        drop table if exists g4_child;
        drop table if exists g4_parent;
        create table g4_parent
        (
            id integer primary key,
            name text not null
        );
        create table g4_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null,
            kind text not null
        );
        create table g4_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        create table g4_grand
        (
            id integer primary key,
            child_id int not null,
            sort_key int not null
        );
        insert into g4_parent (id, name) values
            (7401, 'p-many'),
            (7402, 'p-one'),
            (7403, 'p-none');
        -- 7401: two kept children with unique sort keys plus one skipped child;
        -- 7402: one kept child; 7403: no child at all.
        insert into g4_child (id, parent_id, sort_key, kind) values
            (7411, 7401, 1, 'keep'),
            (7412, 7401, 2, 'keep'),
            (7413, 7401, 3, 'skip'),
            (7421, 7402, 1, 'keep');
        insert into g4_note (id, parent_id, body) values
            (7451, 7401, 'n1'),
            (7452, 7401, 'n2'),
            (7453, 7402, 'n3');
        -- Child 7411 owns two grandchildren; child 7412 owns none (a childless middle level).
        insert into g4_grand (id, child_id, sort_key) values
            (7431, 7411, 2),
            (7432, 7411, 1),
            (7441, 7421, 1);
        """;

    public EagerGraphBranchCoverage4Tests()
    {
        ExecuteRaw(Ddl);

        // The graph relationships must be registered before any mapping of the parent types, so the
        // collection members are recognized as navigations and not as physical columns.
        Context.From<G4Grand>();
        Context.From<G4Note>();
        Context.From<G4Child>(b => b.HasMany(c => c.Grands, g => g.ChildId));
        Context.From<G4Parent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
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

    /// <summary>The parent/child binding rendered as <c>parent:child|child</c>, in parent-id order.</summary>
    private static List<string> Bindings(List<G4Parent> parents)
        => parents.Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id))).ToList();

    // ---------------------------------------------------------------------------------------------
    // 1. Split versus single query over a FILTERED child, with zero / one / many children
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the child query carries its own <c>Where</c>, which the level-one fold must fold
    /// into the join <c>ON</c> predicate (and its filter scope) while keeping childless parents - the
    /// neighbours only fold an unfiltered child. Both modes must bind the same rows with no leakage.
    /// </summary>
    [Fact]
    public void LoadWith_FilteredChild_SplitAndSingle_ShouldBindZeroOneManyWithoutLeakage()
    {
        List<string> Run(EagerLoadMode mode) => Bindings(Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                mode)
            .OrderBy(p => p.Id)
            .ToList());

        var single = Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        single.Select(p => p.Id).Should().Equal(7401, 7402, 7403);
        single[0].Children.Select(c => c.Id).Should().BeEquivalentTo([7411, 7412]);
        single[0].Children.Should().NotContain(c => c.Id == 7413, "the child's own Where folds into the join predicate");
        single[1].Children.Select(c => c.Id).Should().Equal(7421);
        single[2].Children.Should().BeEmpty("a childless parent keeps an empty collection");

        single.SelectMany(p => p.Children).Should().OnlyContain(c => c.ParentId == 7401 || c.ParentId == 7402);
        single[0].Children.Should().OnlyContain(c => c.ParentId == 7401);
        single[1].Children.Should().OnlyContain(c => c.ParentId == 7402);

        var expected = new[] { "7401:7411|7412", "7402:7421", "7403:" };
        Bindings(single).Should().Equal(expected);
        Run(EagerLoadMode.SplitQuery).Should().Equal(expected, "the two modes bind the same filtered child set");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Two-level graph in single-query mode, with a childless middle level and an async twin
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a nested graph whose middle level mixes a child WITH grandchildren and a child
    /// with none, planned through the recursive single-query fold (accumulated item types) and executed
    /// synchronously and asynchronously on the same shape - the neighbours' nested graphs give every child
    /// a grandchild and assert only the synchronous terminal.
    /// </summary>
    [Fact]
    public async Task LoadWith_TwoLevelGraphWithChildlessMiddle_SingleQuery_ShouldStitchSyncAndAsync()
    {
        List<string> Summary(List<G4Parent> parents) => parents
            .Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c =>
                c.Id + "[" + string.Join(",", c.Grands.OrderBy(g => g.SortKey).Select(g => g.Id)) + "]")))
            .ToList();

        var sync = Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<G4Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        var expected = new[] { "7401:7411[7432,7431]|7412[]", "7402:7421[7441]", "7403:" };
        Summary(sync).Should().Equal(expected);

        var asyncRows = await Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<G4Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToListAsync();

        Summary(asyncRows).Should().Equal(expected, "the async terminal stitches the same graph identically");

        var childless = sync.Single(p => p.Id == 7401).Children.Single(c => c.Id == 7412);
        childless.Grands.Should().BeEmpty("a middle-level child with no grandchild keeps an empty collection");
        sync.SelectMany(p => p.Children).SelectMany(c => c.Grands)
            .Should().OnlyContain(g => g.ChildId == 7411 || g.ChildId == 7421, "no grandchild leaks across children");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Per-parent top-N over UNIQUE keys in single-query mode, with an offset shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: unique sort keys make the page order distinguishable, so the ranked per-parent
    /// source is asserted by EXACT ids (the neighbours use increasing ids or an all-tied key set), and a
    /// <c>Page(limit, offset)</c> shape exercises the skip arm. Both modes must agree, and the childless
    /// parent must stay empty.
    /// </summary>
    [Fact]
    public void LoadWith_PerParentTopNUniqueSortKeys_SingleQuery_ShouldPageDeterministically()
    {
        List<string> Page(EagerLoadMode mode) => Bindings(Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().OrderByDescending(x => x.SortKey).Limit(2),
                p => p.Id,
                c => c.ParentId,
                mode)
            .OrderBy(p => p.Id)
            .ToList());

        var single = Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().OrderByDescending(x => x.SortKey).Limit(2),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        single.Select(p => p.Id).Should().Equal(7401, 7402, 7403);
        single[0].Children.Select(c => c.Id).Should().Equal(new[] { 7413, 7412 },
            "sort keys are unique, so the descending per-parent page is deterministic");
        single[1].Children.Select(c => c.Id).Should().Equal(7421);
        single[2].Children.Should().BeEmpty("a childless parent never receives another parent's page");
        single[0].Children.Should().OnlyContain(c => c.ParentId == 7401);

        var expected = new[] { "7401:7413|7412", "7402:7421", "7403:" };
        Bindings(single).Should().Equal(expected);
        Page(EagerLoadMode.SplitQuery).Should().Equal(expected, "split and single modes apply the same per-parent page");

        // The offset shape: skip one row of the child's own ordering inside each partition.
        var offset = Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().OrderBy(x => x.SortKey).Page(1, 1),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(offset).Should().Equal(new[] { "7401:7412", "7402:", "7403:" },
            "the offset shape pages inside each partition independently");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. SingleQuery combined with JoinInto, and the regular-join rejection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the single-query fold stitched together with an explicit <c>JoinInto</c>
    /// collection (the neighbour's combination runs on the split path), plus the regular-join rejection of
    /// the same builder shape, paired with the live JoinInto counterpart.
    /// </summary>
    [Fact]
    public async Task LoadWith_SingleQueryWithJoinInto_ShouldStitchBothCollectionsAndRejectRegularJoins()
    {
        var live = Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .JoinInto(Context.From<G4Note>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .OrderBy(p => p.Id)
            .ToList();

        live.Select(p => p.Id).Should().Equal(7401, 7402, 7403);
        live[0].Children.Select(c => c.Id).Should().BeEquivalentTo([7411, 7412]);
        live[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([7451, 7452]);
        live[1].Children.Select(c => c.Id).Should().Equal(7421);
        live[1].Notes.Select(n => n.Id).Should().Equal(7453);
        live[2].Children.Should().BeEmpty();
        live[2].Notes.Should().BeEmpty("a childless parent also keeps an empty JoinInto collection");

        var asyncLive = await Context.From<G4Parent>()
            .Where(p => p.Id >= 7401 && p.Id <= 7403)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .JoinInto(Context.From<G4Note>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .OrderBy(p => p.Id)
            .ToListAsync();

        asyncLive.Select(p => p.Id).Should().Equal(7401, 7402, 7403);
        asyncLive[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([7451, 7452]);

        // A regular join on the same builder cannot be combined with the single-query fold.
        var regularJoin = () => Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(p => p.Children, c => c.From<G4Child>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .Join(Context.From<G4Note>(), (p, n) => p.Id == n.ParentId)
            .ToList();

        regularJoin.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot be combined with Join/Apply*");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Unsupported child-shape rejection chains, each paired with a live counterpart
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the rejected shapes are the ones the neighbours never name - a child
    /// <c>GroupBy</c>, a child <c>TablesInScopeHint</c> and a two-name chain (<c>OrderBy</c> + <c>Distinct</c>) - both on
    /// the flat fold and, for <c>GroupBy</c>, on the NESTED branch; every rejection runs next to a live
    /// counterpart of the same graph shape.
    /// </summary>
    [Fact]
    public void LoadWith_UnsupportedChildShapes_ShouldRejectEachShapeWithALiveCounterpart()
    {
        var live = Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        live.Should().ContainSingle();
        live[0].Children.Should().HaveCount(2, "the live counterpart of the rejected shapes runs on the same graph");

        var groupByChild = () => Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().GroupBy(x => x.ParentId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        groupByChild.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's GroupBy*");

        var hintChild = () => Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().WithTablesInScopeHint("g4_child"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        hintChild.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's TablesInScopeHint*");

        var twoShapeChild = () => Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().OrderBy(x => x.SortKey).Distinct(),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        twoShapeChild.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's OrderBy, Distinct*");

        // The nested branch reports the same shape names when the child also carries its own graph.
        var nestedLive = Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<G4Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        nestedLive[0].Children.Select(c => c.Id).Should().BeEquivalentTo([7411, 7412]);

        var nestedGroupBy = () => Context.From<G4Parent>()
            .Where(p => p.Id == 7401)
            .LoadWith(
                p => p.Children,
                c => c.From<G4Child>().GroupBy(x => x.ParentId)
                    .LoadWith(x => x.Grands, g => g.From<G4Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        nestedGroupBy.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's GroupBy*");
    }

    /// <summary>Parent of the class-owned eager graph.</summary>
    [SqlTable("g4_parent")]
    public sealed class G4Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<G4Child> Children { get; } = new List<G4Child>();

        public ICollection<G4Note> Notes { get; } = new List<G4Note>();
    }

    /// <summary>Child of the class-owned eager graph, with a per-parent sort key and a kind filter.</summary>
    [SqlTable("g4_child")]
    public sealed class G4Child
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

        public ICollection<G4Grand> Grands { get; } = new List<G4Grand>();
    }

    /// <summary>Grandchild of the class-owned eager graph (third level).</summary>
    [SqlTable("g4_grand")]
    public sealed class G4Grand
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("child_id")]
        public int ChildId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }

    /// <summary>Second parent collection, filled through <c>JoinInto</c>.</summary>
    [SqlTable("g4_note")]
    public sealed class G4Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }
}
