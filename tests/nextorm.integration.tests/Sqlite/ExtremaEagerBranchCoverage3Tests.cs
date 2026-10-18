using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Extrema lowering and eager-load orchestration coverage (cycle 3, cluster D6) executed against a live
/// SQLite database. Every fact uses class-owned marker rows (the <c>pb3e_marker</c> extrema table and the
/// <c>pb3e_parent</c>/<c>pb3e_child</c>/<c>pb3e_note</c> eager graph, created and seeded from raw DDL in
/// the constructor) and asserts the returned rows; guards always run next to a live counterpart of the
/// same shape.
///
/// <para>
/// Targeted surface: <c>src/nextorm.core/Builders/EntityBuilder.cs</c>
/// <c>SelectWhereMax</c>/<c>SelectWhereMin</c> and their projection overloads (2044-2099 ->
/// <c>SelectWhereExtreme</c> 2101-2133); <c>src/nextorm.core/Builders/EntityBuilderEagerLoading.cs</c>
/// <c>Execute</c>/<c>ExecuteLoadedChildren</c>/<c>ExecuteLoadedChildrenAsync</c> (893/918/952),
/// <c>HasPerParentTopN</c> (974), <c>ValidatePerParentPagingChild</c> (1000-1025),
/// <c>ValidateLoadWithForJoinInto</c> (1051-1070) and <c>PlanSingleQuery</c> (338-403).
/// </para>
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>CommonTestSuite.SelectWhereExtrema</c> owns
/// the seeded <c>extrema_entity</c> shapes (global/grouped, projection, composite value selector, null
/// semantics, empty source); <c>EagerModeBranchCoverage2Tests</c> owns Default/SingleQuery/SplitQuery/Auto
/// equivalence, the per-parent top-N with strictly increasing child ids, the nested two-level graph, the
/// Correlated / non-list-terminal / WithTies / without-OrderBy guards and the SingleQuery fold guards.
/// This class adds the filtered and strongly-typed extrema shapes and the eager combinations that remain
/// uncovered: the mode-dependent child ORDER, the all-tied per-parent page with a childless parent, the
/// LoadWith+JoinInto combination with its per-parent top-N rejection and the loader plumbing guards.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class ExtremaEagerBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists pb3e_marker
        (
            id integer primary key,
            score int null,
            grade int not null,
            label text not null
        );
        delete from pb3e_marker;
        insert into pb3e_marker (id, score, grade, label) values
            (1, null, 10, 'one'),
            (2, 5,    20, 'two'),
            (3, 9,    30, 'three'),
            (4, 9,    40, 'four'),
            (5, 3,    50, 'five'),
            (6, 3,    60, 'six'),
            (7, 7,    70, 'seven');

        create table if not exists pb3e_parent (id integer primary key, name text);
        delete from pb3e_parent;
        create table if not exists pb3e_child (id integer primary key, parent_id int not null, sort_key int not null, name text);
        delete from pb3e_child;
        create table if not exists pb3e_note (id integer primary key, parent_id int not null, body text);
        delete from pb3e_note;

        insert into pb3e_parent (id, name) values
            (9301, 'p-ordered'),
            (9302, 'p-tied'),
            (9303, 'p-empty');

        -- 9301: two children with distinct sort keys (the two eager modes order them differently);
        -- 9302: three children whose sort keys are ALL tied (a per-parent page must still return two);
        -- 9303: no child at all.
        insert into pb3e_child (id, parent_id, sort_key, name) values
            (9310, 9301, 1, 'a1'),
            (9311, 9301, 2, 'a2'),
            (9320, 9302, 1, 't1'),
            (9321, 9302, 1, 't2'),
            (9322, 9302, 1, 't3');

        insert into pb3e_note (id, parent_id, body) values
            (9400, 9301, 'note-1'),
            (9401, 9301, 'note-2'),
            (9402, 9302, 'note-3');
        """;

    public ExtremaEagerBranchCoverage3Tests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();

        // The eager graph relationships must be registered before any mapping of the parent type, so the
        // collection members are recognized as navigations and not as physical columns.
        Context.From<Pb3eChild>();
        Context.From<Pb3eNote>();
        Context.From<Pb3eParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // 1. Extrema over the nullable value column, without a predicate
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Extrema_NullableColumnWithoutPredicate_ShouldReturnTiedExtremes()
    {
        var marker = Context.From<Pb3eMarker>();

        var maxAll = marker.SelectWhereMax(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ToList();
        maxAll.Select(r => r.Id).Should().Equal(3, 4);
        maxAll.Should().OnlyContain(r => r.Score == 9);

        var minOne = marker.SelectWhereMin(e => e.Score).ToList();
        minOne.Should().ContainSingle();
        minOne[0].Score.Should().Be(3);
        minOne[0].Id.Should().BeOneOf(5, 6);

        var minAll = marker.SelectWhereMin(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ToList();
        minAll.Select(r => r.Id).Should().Equal(5, 6);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Extrema over a filtered subset and a non-nullable value column
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Extrema_WithPredicateAndNonNullableColumn_ShouldSelectInsideTheFilteredSubset()
    {
        var nonNullableMax = Context.From<Pb3eMarker>()
            .Where(e => e.Grade >= 30)
            .SelectWhereMax(e => e.Grade, ExtremeRowTies.All)
            .ToList();
        nonNullableMax.Should().ContainSingle();
        nonNullableMax[0].Id.Should().Be(7);
        nonNullableMax[0].Grade.Should().Be(70);

        var nonNullableMin = Context.From<Pb3eMarker>()
            .Where(e => e.Grade >= 30)
            .SelectWhereMin(e => e.Grade)
            .ToList();
        nonNullableMin.Should().ContainSingle();
        nonNullableMin[0].Id.Should().Be(3);
        nonNullableMin[0].Grade.Should().Be(30);

        var nullableMin = Context.From<Pb3eMarker>()
            .Where(e => e.Grade >= 30)
            .SelectWhereMin(e => e.Score)
            .ToList();
        nullableMin.Should().ContainSingle();
        nullableMin[0].Score.Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The projection overloads with a tie set
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Extrema_ProjectionOverload_ShouldProjectTiedExtremeRows()
    {
        var projectedMax = Context.From<Pb3eMarker>()
            .SelectWhereMax(e => e.Score, e => new { e.Id, e.Grade, e.Label }, ExtremeRowTies.All)
            .ToList();

        projectedMax.Select(r => (r.Id, r.Grade, r.Label)).Should()
            .BeEquivalentTo(new[] { (3, 30, "three"), (4, 40, "four") });

        var projectedMin = Context.From<Pb3eMarker>()
            .SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All)
            .ToList();

        projectedMin.Select(r => r.Id).Should().BeEquivalentTo(new[] { 5, 6 });
        projectedMin.Select(r => r.Label).Should().BeEquivalentTo(new[] { "five", "six" });
    }

    // ---------------------------------------------------------------------------------------------
    // 4. An empty filtered source against a live counterpart of the same shape
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Extrema_EmptyVersusNonEmptyFilteredSource_ShouldYieldNoRowsOnlyWhenNothingMatches()
    {
        Context.From<Pb3eMarker>()
            .Where(e => e.Grade > 1000)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToList()
            .Should().BeEmpty();

        Context.From<Pb3eMarker>()
            .Where(e => e.Grade > 1000)
            .SelectWhereMin(e => e.Score)
            .ToList()
            .Should().BeEmpty();

        var liveMax = Context.From<Pb3eMarker>()
            .Where(e => e.Grade <= 20)
            .SelectWhereMax(e => e.Score)
            .ToList();
        liveMax.Should().ContainSingle();
        liveMax[0].Id.Should().Be(2);
        liveMax[0].Score.Should().Be(5);

        var liveMin = Context.From<Pb3eMarker>()
            .Where(e => e.Grade <= 20)
            .SelectWhereMin(e => e.Score)
            .ToList();
        liveMin.Should().ContainSingle();
        liveMin[0].Id.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. The two eager modes order the same child collection differently
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LoadWith_SplitVersusSingle_ShouldOrderChildCollectionsDifferently()
    {
        // SplitQuery honours the child query's own ORDER BY; the SingleQuery fold accepts only the child's
        // WHERE (an ordered child is rejected there), so the stitched collection keeps the denormalized
        // row order instead. The two modes therefore materialize differently ordered collections for the
        // very same graph, deterministically: parent 9301 owns children (9310 sort_key 1) and
        // (9311 sort_key 2).
        var splitParents = Context.From<Pb3eParent>()
            .Where(p => p.Id >= 9301 && p.Id <= 9303)
            .LoadWith(
                p => p.Children,
                c => c.From<Pb3eChild>().OrderByDescending(x => x.SortKey).OrderByDescending(x => x.Id),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        splitParents.Select(p => p.Id).Should().Equal(9301, 9302, 9303);
        splitParents[0].Children.Select(c => c.Id).Should().Equal(9311, 9310);
        splitParents[1].Children.Select(c => c.Id).Should().Equal(9322, 9321, 9320);
        splitParents[2].Children.Should().BeEmpty();

        var singleParents = Context.From<Pb3eParent>()
            .Where(p => p.Id >= 9301 && p.Id <= 9303)
            .LoadWith(p => p.Children, c => c.From<Pb3eChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        singleParents.Select(p => p.Id).Should().Equal(9301, 9302, 9303);
        singleParents[0].Children.Select(c => c.Id).Should().Equal(9310, 9311);
        singleParents[1].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 9320, 9321, 9322 });
        singleParents[2].Children.Should().BeEmpty();

        singleParents[0].Children.Select(c => c.Id).Should()
            .NotEqual(splitParents[0].Children.Select(c => c.Id), "the two modes order the same child collection differently");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Per-parent top-N with an all-tied child key set and a childless parent, both modes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LoadWith_PerParentTopNWithTiesAndChildlessParent_ShouldPagePerParent()
    {
        var single = Context.From<Pb3eParent>()
            .Where(p => p.Id >= 9301 && p.Id <= 9303)
            .LoadWith(
                p => p.Children,
                c => c.From<Pb3eChild>().OrderBy(x => x.SortKey).Limit(2),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .OrderBy(p => p.Id)
            .ToList();

        single.Select(p => p.Id).Should().Equal(9301, 9302, 9303);
        single[0].Children.Should().HaveCount(2);
        single[1].Children.Should().HaveCount(2, "the three tied children must page to two rows for this parent");
        single[1].Children.Should().OnlyContain(c => c.ParentId == 9302 && c.SortKey == 1);
        single[2].Children.Should().BeEmpty("the childless parent keeps an empty collection");

        var split = Context.From<Pb3eParent>()
            .Where(p => p.Id >= 9301 && p.Id <= 9303)
            .LoadWith(
                p => p.Children,
                c => c.From<Pb3eChild>().OrderBy(x => x.SortKey).Limit(2),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        split.Select(p => p.Id).Should().Equal(9301, 9302, 9303);
        split[0].Children.Should().HaveCount(2);
        split[1].Children.Should().HaveCount(2);
        split[1].Children.Should().OnlyContain(c => c.ParentId == 9302 && c.SortKey == 1);
        split[2].Children.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // 7. LoadWith combined with JoinInto, and the per-parent top-N rejection of that entry point
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void LoadWith_CombinedWithJoinInto_ShouldStitchBothAndRejectPerParentTopN()
    {
        var live = Context.From<Pb3eParent>()
            .Where(p => p.Id >= 9301 && p.Id <= 9303)
            .LoadWith(p => p.Children, c => c.From<Pb3eChild>().OrderBy(x => x.SortKey).OrderBy(x => x.Id), p => p.Id, c => c.ParentId)
            .JoinInto(Context.From<Pb3eNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .OrderBy(p => p.Id)
            .ToList();

        live.Select(p => p.Id).Should().Equal(9301, 9302, 9303);
        live[0].Children.Select(c => c.Id).Should().Equal(9310, 9311);
        live[0].Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 9400, 9401 });
        live[1].Children.Select(c => c.Id).Should().Equal(9320, 9321, 9322);
        live[1].Notes.Select(n => n.Id).Should().Equal(9402);
        live[2].Children.Should().BeEmpty();
        live[2].Notes.Should().BeEmpty();

        var rejected = () => Context.From<Pb3eParent>()
            .Where(p => p.Id == 9301)
            .LoadWith(
                p => p.Children,
                c => c.From<Pb3eChild>().OrderBy(x => x.SortKey).Limit(1),
                p => p.Id,
                c => c.ParentId)
            .JoinInto(Context.From<Pb3eNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*per-parent top-N requires correlated execution*");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. The loader plumbing: the no-spec no-op contract and the unsupported per-parent shapes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task EagerLoaderPlumbing_ShouldNoOpWithoutSpecsAndRejectUnsupportedPerParentShapes()
    {
        var live = Context.From<Pb3eParent>()
            .Where(p => p.Id == 9301)
            .LoadWith(
                p => p.Children,
                c => c.From<Pb3eChild>().OrderBy(x => x.SortKey).Limit(1),
                p => p.Id,
                c => c.ParentId)
            .ToList();
        live.Should().ContainSingle();
        live[0].Children.Should().ContainSingle();

        // A builder carrying no load specification is a no-op at every orchestration entry point.
        var noSpecs = Context.From<Pb3eParent>();
        EntityBuilderEagerLoading.Execute(noSpecs, []);
        await EntityBuilderEagerLoading.ExecuteAsync(noSpecs, [], CancellationToken.None);
        EntityBuilderEagerLoading.ExecuteLoadedChildren(noSpecs, []);
        await EntityBuilderEagerLoading.ExecuteLoadedChildrenAsync(noSpecs, [], CancellationToken.None);

        // A non-paged child is a no-op for the per-parent paging validator; a paged child carrying an
        // unsupported modifier is rejected with the shared message.
        EntityBuilderEagerLoading.ValidatePerParentPagingChild(Context.From<Pb3eChild>());

        var unsupported = () => EntityBuilderEagerLoading.ValidatePerParentPagingChild(
            Context.From<Pb3eChild>().OrderBy(x => x.SortKey).Limit(1).Distinct());
        unsupported.Should().Throw<NotSupportedException>().WithMessage("*also carries*Distinct*");
    }

    /// <summary>Marker row of the class-owned <c>pb3e_marker</c> extrema table.</summary>
    [SqlTable("pb3e_marker")]
    public sealed class Pb3eMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The nullable extremum value: nulls are ignored, ties are kept by <c>All</c>.</summary>
        [Column("score")]
        public int? Score { get; set; }

        /// <summary>A non-nullable extremum value used with a range predicate.</summary>
        [Column("grade")]
        public int Grade { get; set; }

        /// <summary>The projected text payload.</summary>
        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Parent of the class-owned eager graph.</summary>
    [SqlTable("pb3e_parent")]
    public sealed class Pb3eParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<Pb3eChild> Children { get; } = new List<Pb3eChild>();

        public ICollection<Pb3eNote> Notes { get; } = new List<Pb3eNote>();
    }

    /// <summary>Child of the class-owned eager graph with a per-parent sort key.</summary>
    [SqlTable("pb3e_child")]
    public sealed class Pb3eChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    /// <summary>Second collection of the parent, filled through <c>JoinInto</c>.</summary>
    [SqlTable("pb3e_note")]
    public sealed class Pb3eNote
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string? Body { get; set; }
    }
}
