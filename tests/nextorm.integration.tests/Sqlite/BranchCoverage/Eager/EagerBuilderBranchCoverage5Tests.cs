using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_integration_tests;

namespace NextORM.Integration.Tests;

/// <summary>
/// Eager-load builder coverage (cycle 5, cluster C5) executed against a live SQLite database. Every fact
/// runs a real <c>LoadWith</c>/<c>JoinInto</c> query over class-owned marker rows of <c>eb5_parent</c>,
/// <c>eb5_child</c>, <c>eb5_grand</c> and <c>eb5_note</c> (three distinct CLR entity types plus a second
/// collection), dropped, created and seeded by this class's own DDL in the constructor. Every guard fact is
/// paired with a live successful companion of the same graph shape.
///
/// <para>
/// Targeted arms (each fact names a DIFFERENT one): <c>Builders/EntityBuilderEagerLoading.cs</c> - the
/// three-level split graph through the child factory (<c>Execute</c>/<c>ExecuteAsync</c>, the recursive
/// <c>ExecuteLoadedChildren</c>), the nested single-query fold and its constructed
/// <c>EagerLoadNestedJoinIntoSpec</c> (:401) with the DESCENDANT condition folded into the recursive join,
/// the per-parent top-N guard <c>PlanSingleQuery</c> :348 (<c>PerParentTopNNotSupported</c>) next to the
/// green split counterpart, the nested non-<c>Where</c> shape guard :366, and the flat shape guard of
/// <c>ToJoinIntoSpec</c>; <c>Builders/EntityBuilder.cs</c> - <c>UnsupportedChildShapes</c> (:1208),
/// <c>CreatePairCommand</c> (:1528) through a live <c>JoinInto</c> pair command (two collections and a
/// paged parent query) and the root-alias seam <c>AliasRoot</c> (:3278).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>EagerGraphBranchCoverage4Tests</c> owns the
/// split-versus-single FILTERED child, the two-level childless middle in single-query mode, the per-parent
/// top-N over unique keys with a <c>Page</c> offset, the SingleQuery+JoinInto stitching and the flat/nested
/// GroupBy / TablesInScopeHint / OrderBy,Distinct guard chains; <c>EagerModeBranchCoverage2Tests</c> owns
/// the mode-equivalence matrix and the Correlated/WithTies/without-OrderBy guards;
/// <c>ExtremaEagerBranchCoverage3Tests</c> owns the mode-dependent child order and the all-tied page. This
/// class instead drives the three-level graph in DEFAULT (split) mode and its async twin, the descendant
/// filter fold of the single-query recursion, per-parent paging COMBINED with a nested grandchild graph,
/// the ForUpdate/Distinct shape guards, the pair command over two collections and paged parents, and a live
/// <c>AliasRoot</c> re-root. A child factory with <c>JoinInto</c> is not expressible as a supported nested
/// eager node and is deferred (see <c>rv1/inputs/deferred.json</c>).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class EagerBuilderBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists eb5_grand;
        drop table if exists eb5_note;
        drop table if exists eb5_child;
        drop table if exists eb5_parent;
        create table eb5_parent
        (
            id integer primary key,
            name text not null
        );
        create table eb5_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null,
            kind text not null
        );
        create table eb5_grand
        (
            id integer primary key,
            child_id int not null,
            sort_key int not null
        );
        create table eb5_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        insert into eb5_parent (id, name) values
            (9501, 'p-many'),
            (9502, 'p-one'),
            (9503, 'p-none');
        -- 9501: two kept children plus one skipped child; 9502: one kept child; 9503: no child at all.
        insert into eb5_child (id, parent_id, sort_key, kind) values
            (9511, 9501, 1, 'keep'),
            (9512, 9501, 2, 'keep'),
            (9513, 9501, 3, 'skip'),
            (9521, 9502, 1, 'keep');
        insert into eb5_note (id, parent_id, body) values
            (9551, 9501, 'n1'),
            (9552, 9501, 'n2'),
            (9553, 9503, 'n3');
        -- Child 9511 owns two grandchildren; child 9512 owns none (a childless middle level).
        insert into eb5_grand (id, child_id, sort_key) values
            (9531, 9511, 2),
            (9532, 9511, 1),
            (9541, 9521, 1);
        """;

    public EagerBuilderBranchCoverage5Tests()
    {
        ExecuteRaw(Ddl);

        // The graph relationships must be registered before any mapping of the parent types, so the
        // collection members are recognized as navigations and not as physical columns.
        Context.From<Eb5Grand>();
        Context.From<Eb5Note>();
        Context.From<Eb5Child>(b => b.HasMany(c => c.Grands, g => g.ChildId));
        Context.From<Eb5Parent>(b => b
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

    /// <summary>The parent/child/grand binding rendered as <c>parent:child[grands]|child[grands]</c>.</summary>
    private static List<string> Bindings(List<Eb5Parent> parents) => parents
        .Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c =>
            c.Id + "[" + string.Join(",", c.Grands.Select(g => g.Id)) + "]")))
        .ToList();

    // ---------------------------------------------------------------------------------------------
    // 1. Three-level graph through the child factory in DEFAULT (split) mode
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the DEFAULT mode resolves to split loading, so the child factory's own nested
    /// declaration is executed by the recursive <c>ExecuteLoadedChildren</c> path (nothing is stitched from
    /// one result set); the middle child 9512 owns no grandchild and the top parent 9503 owns no child,
    /// and both are asserted by value.
    /// </summary>
    [Fact]
    public void LoadWith_ThreeLevelGraphInDefaultMode_ShouldGroupPerParentWithChildlessMiddle()
    {
        var rows = Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(rows).Should().Equal(
            new[] { "9501:9511[9531,9532]|9512[]", "9502:9521[9541]", "9503:" },
            "the default mode is the split loader, so the third level loads recursively");

        rows[0].Children.Select(c => c.Id).Should().Equal(new[] { 9511, 9512 }, "the skipped child 9513 is filtered out");
        rows[0].Children.Single(c => c.Id == 9512).Grands.Should().BeEmpty("9512 has no grandchild");
        rows[2].Children.Should().BeEmpty("9503 has no child");
        rows.SelectMany(p => p.Children).Should().OnlyContain(c => c.ParentId == 9501 || c.ParentId == 9502,
            "no child leaks across parents");
        rows[0].Children.SelectMany(c => c.Grands).Should().OnlyContain(g => g.ChildId == 9511,
            "no grandchild leaks across children");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The same graph in explicit SplitQuery mode, synchronously and asynchronously
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the explicit <c>SplitQuery</c> path with a child <c>OrderBy</c> (which the split
    /// loader honors verbatim) and its asynchronous twin, which recurses through
    /// <c>ExecuteLoadedChildrenAsync</c>; the neighbour's async fact covered the single-query stitch only.
    /// </summary>
    [Fact]
    public async Task LoadWith_ThreeLevelGraphInSplitQueryMode_ShouldOrderChildrenAndMatchAsync()
    {
        EntityBuilder<Eb5Parent> Query() => Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().OrderBy(x => x.SortKey), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id);

        var sync = Query().ToList();
        var expected = new[] { "9501:9511[9532,9531]|9512[]|9513[]", "9502:9521[9541]", "9503:" };
        Bindings(sync).Should().Equal(expected, "the split loader keeps the child's own ordering");

        var asyncRows = await Query().ToListAsync();
        Bindings(asyncRows).Should().Equal(expected, "the asynchronous recursion stitches the same graph");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The three-level graph in single-query mode with a DESCENDANT filter
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the nested single-query fold constructs
    /// <c>EagerLoadNestedJoinIntoSpec</c> (:401) and recurses into the grandchild level, folding the
    /// GRANDCHILD's own <c>Where</c> into the recursive join (the neighbours only filtered the child
    /// level). The grandchildless middle child and the childless parent stay empty.
    /// </summary>
    [Fact]
    public void LoadWith_ThreeLevelGraphInSingleQueryMode_ShouldFoldDescendantFilter()
    {
        var rows = Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().Where(y => y.SortKey == 1), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(rows).Should().Equal(
            new[] { "9501:9511[9532]|9512[]", "9502:9521[9541]", "9503:" },
            "the grandchild's own SortKey filter is folded into the recursive join");
        rows[2].Children.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Per-parent top-N combined with a nested grandchild graph, in split mode
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a PAGED child that also declares a nested grandchild graph, executed by the
    /// split loader - the ranked per-parent page bounds are applied inside each parent partition and the
    /// recursion then loads the grandchildren of the paged children only. The neighbour's per-parent page
    /// facts were flat (no third level) and single-query.
    /// </summary>
    [Fact]
    public void LoadWith_PerParentTopNWithNestedGraphInSplitMode_ShouldPageEachParentAndLoadGrands()
    {
        var rows = Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .Limit(2)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().OrderBy(x => x.SortKey), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(rows).Should().Equal(
            new[] { "9501:9511[9532,9531]|9512[]", "9502:9521[9541]", "9503:" },
            "every parent receives its own top-2 page and the childless parent stays empty");

        rows[0].Children.Select(c => c.Id).Should().Equal(new[] { 9511, 9512 }, "the third child 9513 is outside the page");
        rows[0].Children.Should().OnlyContain(c => c.ParentId == 9501);
        rows.SelectMany(p => p.Children).Should().HaveCount(3, "2 + 1 + 0 children across the page");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Per-parent top-N with a nested graph in single-query mode: the documented guard
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the nested <c>PlanSingleQuery</c> fold rejects a per-parent top-N child at
    /// <c>EntityBuilderEagerLoading.cs:348</c> (<c>PerParentTopNNotSupported</c>); the guard is asserted
    /// together with the green split counterpart of the exact same shape.
    /// </summary>
    [Fact]
    public void LoadWith_PerParentTopNWithNestedGraphInSingleQueryMode_ShouldRejectAndSplitShouldStayGreen()
    {
        var rejected = () => Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .Limit(2)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*requires correlated execution*");

        var live = Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .Limit(2)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .ToList();

        live.Should().ContainSingle();
        live[0].Children.Select(c => c.Id).Should().Equal(new[] { 9511, 9512 }, "the split counterpart applies the same page");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. The nested :366 shape guard, with a live companion
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory that declares a nested graph AND a non-<c>Where</c> shape
    /// (<c>ForUpdate</c>) is rejected by the nested fold's shape guard at
    /// <c>EntityBuilderEagerLoading.cs:366</c>; the neighbour named GroupBy / TablesInScopeHint /
    /// OrderBy,Distinct, and this fact pairs the rejection with a live companion of the same graph.
    /// </summary>
    [Fact]
    public void LoadWith_NestedChildWithForUpdateInSingleQueryMode_ShouldRejectShapeWithLiveCompanion()
    {
        var live = Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        live[0].Children.Select(c => c.Id).Should().Equal(new[] { 9511, 9512 },
            "the live companion of the rejected shape runs the same nested graph");

        var rejected = () => Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .ForUpdate()
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's ForUpdate/ForShare*");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. The flat shape guard of ToJoinIntoSpec, with a live companion
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a FLAT child (no nested graph) whose shape is <c>Distinct</c> is rejected when
    /// the single-query fold folds the child source; the neighbour used an OrderBy+Distinct chain and a
    /// GroupBy, so the single-shape arm is new. The green companion uses the same child source shape.
    /// </summary>
    [Fact]
    public void LoadWith_FlatChildWithDistinctInSingleQueryMode_ShouldRejectShapeWithLiveCompanion()
    {
        var live = Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();
        Bindings(live).Should().Equal("9501:9511[]|9512[]", "9502:9521[]", "9503:");

        var rejected = () => Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>().Distinct(),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's Distinct*");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. CreatePairCommand: a live JoinInto pair command over two different collections
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EntityBuilder.CreatePairCommand</c> (:1528) runs one denormalized command
    /// with TWO stitching specifications (a child collection and a note collection) and deduplicates the
    /// repeated parent rows; the neighbour's JoinInto fact combined it with a single-query LoadWith.
    /// </summary>
    [Fact]
    public void JoinInto_TwoCollections_ShouldStitchPairCommandWithoutCrossParentLeakage()
    {
        var rows = Context.From<Eb5Parent>()
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .JoinInto(Context.From<Eb5Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .JoinInto(Context.From<Eb5Note>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(new[] { 9501, 9502, 9503 }, "the pair command deduplicates the parent rows");
        rows[0].Children.Select(c => c.Id).Should().BeEquivalentTo([9511, 9512, 9513]);
        rows[0].Notes.Select(n => n.Id).Should().BeEquivalentTo([9551, 9552]);
        rows[0].Children.Should().OnlyContain(c => c.ParentId == 9501);
        rows[0].Notes.Should().OnlyContain(n => n.ParentId == 9501);
        rows[1].Children.Select(c => c.Id).Should().Equal(9521);
        rows[1].Notes.Should().BeEmpty();
        rows[2].Children.Should().BeEmpty("a childless parent keeps an empty child collection");
        rows[2].Notes.Select(n => n.Id).Should().Equal(new[] { 9553 }, "the note collection is independent of the child one");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. CreatePairCommand: a paged parent query feeds the pair command
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the parent query is ordered and paged, so <c>CreatePairCommand</c> (:1528)
    /// carries the limit/offset in the parent subquery the stitching join wraps around; only the two
    /// selected parents receive children.
    /// </summary>
    [Fact]
    public void JoinInto_PagedParents_ShouldStitchOnlyTheSelectedParents()
    {
        var rows = Context.From<Eb5Parent>()
            .OrderBy(p => p.Id)
            .Limit(2)
            .JoinInto(Context.From<Eb5Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(new[] { 9501, 9502 }, "the parent page bounds the stitched result");
        rows[0].Children.Select(c => c.Id).Should().BeEquivalentTo([9511, 9512, 9513]);
        rows[1].Children.Select(c => c.Id).Should().Equal(9521);
    }

    // ---------------------------------------------------------------------------------------------
    // 10. The root-alias seam on a plain live source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EntityBuilder.AliasRoot</c> (:3278) re-roots a plain source builder into a
    /// caller-created builder, preserving the query state; the guard suite only asserts its rejections.
    /// The live fact proves the re-rooted source still executes and returns the same rows.
    /// </summary>
    [Fact]
    public void AliasRoot_OnPlainRoot_ShouldPreserveSourceAndReturnRows()
    {
        var rows = Context.From<Eb5Parent>()
            .AliasRoot<EntityBuilder<Eb5Parent>, Eb5Parent>(c => c.From<Eb5Parent>())
            .Where(p => p.Id >= 9501 && p.Id <= 9503)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        rows.Should().Equal(new[] { 9501, 9502, 9503 }, "the re-rooted builder keeps the source and the predicate");
    }

    // ---------------------------------------------------------------------------------------------
    // 10b. A pre-alias state carried onto an alias-joined projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EntityBuilder.ApplyPreAliasStateToAliasJoined</c> (:3189-3241) runs only
    /// when an alias projection (dimension &gt; 1) receives an alias join while it already carries
    /// query state. The state written before <c>.WithAlias</c> (an <c>OrderByDescending</c> on the plain
    /// root) is first carried by <c>AliasRoot</c> and then re-based onto the extended projection
    /// (:3223-3239) instead of being silently dropped; the live rows prove the ordering survived the
    /// join (parent 9502 first) and the joined child rows are complete. The second slot uses the
    /// generated <c>Alias.Buyer</c> marker: only the markers of a recognized alias chain exist in this
    /// assembly (<c>Alias.Root</c>, <c>Alias.Buyer</c>, <c>Alias.Approver</c>), so naming the slot is
    /// what makes the generator emit the extended projection with the <c>Root</c>/<c>Buyer</c> slots.
    /// </summary>
    [Fact]
    public void WithAlias_OrderingBeforeAliasJoin_ShouldRebaseStateAndReturnRows()
    {
        var rooted = Context.From<Eb5Parent>()
            .OrderByDescending(p => p.Id)
            .WithAlias(Alias.Root);

        var rows = rooted
            .Join<Eb5Child>(Context.From<Eb5Child>(), (p, c) => p.Root.Id == c.ParentId, Alias.Buyer)
            .Select(p => new { Parent = p.Root.Id, Child = p.Buyer.Id })
            .ToList();

        rows.Should().HaveCount(4, "9501 owns three children and 9502 one");
        rows.Select(r => r.Parent).Should().Equal(new[] { 9502, 9501, 9501, 9501 },
            "the pre-join descending ordering on the root slot is re-based onto the joined projection");
        rows.Select(r => r.Child).Should().BeEquivalentTo(new[] { 9511, 9512, 9513, 9521 });
        rows.Should().OnlyContain(r => r.Child != 0, "every joined child column resolves after the re-base");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. An unsupported shape at the DESCENDANT level of the nested single-query fold
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the nested fold recurses into the grandchild level
    /// (<c>PlanSingleQuery</c> :390) and the DESCENDANT's flat fold rejects its own unsupported shape
    /// (<c>GroupBy</c> on the grandchild), not the child's - the neighbours only placed the unsupported
    /// shape on the child level. Paired with the live descendant-filtered graph.
    /// </summary>
    [Fact]
    public void LoadWith_GrandChildWithGroupByInSingleQueryMode_ShouldRejectShapeWithLiveCompanion()
    {
        var live = Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().Where(y => y.SortKey == 1), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();
        Bindings(live).Should().Equal(new[] { "9501:9511[9532]|9512[]" },
            "the live companion is the same three-level graph with a grandchild filter");

        var rejected = () => Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .Where(x => x.Kind == "keep")
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().GroupBy(z => z.ChildId), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot fold the child query's GroupBy*");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. UnsupportedChildShapes: the child-query OrderBy arm of the single-query fold
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>EntityBuilder.UnsupportedChildShapes</c> (:1207) reports the child's own
    /// <c>OrderBy</c> through <c>UnsupportedSingleQueryChildShapes</c> (ordering is allowed only by the
    /// per-parent paging source), so the nested single-query fold rejects the shape with the exact guard
    /// message before any SQL is built. The live companion is the SAME three-level graph in split mode,
    /// where the child ordering is honored - the factory is legal, only the single-query fold is not.
    /// A paging-only child factory is not reachable on this path: any non-empty page is routed to the
    /// per-parent top-N source first, so the <c>Limit/Offset/Page</c> arm of the same guard is recorded
    /// in <c>rv1/inputs/deferred.json</c> instead.
    /// </summary>
    [Fact]
    public void LoadWith_NestedChildWithOrderByInSingleQueryMode_ShouldRejectShapeWithLiveSplitCompanion()
    {
        var live = Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>().OrderBy(x => x.SortKey), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(new[] { "9501:9511[9532,9531]|9512[]|9513[]" },
            "the split companion honors the child's own ordering and the nested grand ordering");

        var rejected = () => Context.From<Eb5Parent>()
            .Where(p => p.Id == 9501)
            .LoadWith(
                p => p.Children,
                c => c.From<Eb5Child>()
                    .OrderBy(x => x.SortKey)
                    .LoadWith(x => x.Grands, g => g.From<Eb5Grand>(), x => x.Id, g => g.ChildId),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("LoadWith with EagerLoadMode.SingleQuery cannot fold the child query's OrderBy " +
                "into the join predicate: only the child's own Where is supported. Remove the modifier or " +
                "use split-query loading (omit the SingleQuery mode), which honors every child-query shape.");
    }

    /// <summary>Parent of the class-owned eager graph.</summary>
    [SqlTable("eb5_parent")]
    public sealed class Eb5Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Eb5Child> Children { get; } = new List<Eb5Child>();

        public ICollection<Eb5Note> Notes { get; } = new List<Eb5Note>();
    }

    /// <summary>Child of the class-owned eager graph, with a per-parent sort key and a kind filter.</summary>
    [SqlTable("eb5_child")]
    public sealed class Eb5Child
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

        public ICollection<Eb5Grand> Grands { get; } = new List<Eb5Grand>();
    }

    /// <summary>Grandchild of the class-owned eager graph (the third distinct CLR type).</summary>
    [SqlTable("eb5_grand")]
    public sealed class Eb5Grand
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
    [SqlTable("eb5_note")]
    public sealed class Eb5Note
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
