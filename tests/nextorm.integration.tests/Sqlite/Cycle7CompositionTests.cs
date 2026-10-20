using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Generated.nextorm_integration_tests;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// EntityBuilder composition coverage (cycle 7, unit D4) executed against a live SQLite database. Every
/// fact drives a real query over class-owned marker rows of <c>cp7_parent</c>, <c>cp7_child</c>,
/// <c>cp7_grand</c> and <c>cp7_note</c> (dropped, created and seeded by this class's own DDL in the
/// constructor) and asserts returned values. Each rejection fact is paired with a live working control
/// query over the same graph.
///
/// <para>
/// Targeted arms: <c>Builders/EntityBuilder.cs</c> - the remaining <c>UnsupportedChildShapes</c> (:1208)
/// child-shape arms <c>Having</c>, <c>DistinctOn</c>, <c>LimitBy</c>, <c>Final</c>, <c>PreWhere</c>,
/// <c>ArrayJoin</c>, <c>TableSample</c>, <c>Sample</c>, <c>ForSystemTime</c>, <c>ForUpdate/ForShare</c>,
/// <c>ForUpdate/ForShare OF</c>, <c>Settings</c>, <c>TableHint</c> and <c>IndexHint</c> (the
/// OrderBy/Extreme/Derived/Distinct+GroupBy/Window/Join arms are owned by cycles 5-6);
/// <c>CreatePairCommand</c> (:1528) through the suppressed cartesian warning and a CTE parent source;
/// <c>get_HasCompositionState</c> (:2433) through the provider-specific modifier states that the raw
/// binding guard suite never applied to a bound source; <c>IsPlainScalarScope</c> (:1300) through plain,
/// composed, provider-specific, lock/temporal, grouping and raw/derived scalar scopes; and
/// <c>AliasRoot</c> (:3278) through a derived CREATE root, the missing-Item1 rejection, and the
/// condition/having/group/preWhere re-binds.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>EagerBuilderBranchCoverage6Tests</c> owns the
/// Join/SelectWhereExtreme/derived-As/Distinct+GroupBy/Window child shapes, the m:n and two-collection
/// pair commands at the sync and async terminals, the filtered and empty derived pair sources, the
/// IgnoreFilters/extreme/table-hint/temporal/index/source-lock/table-sample binding states and the live
/// reference/collection/inherited/nullable bindings. <c>EagerBuilderBranchCoverage5Tests</c> owns the
/// OrderBy and flat Distinct child guards, the nested GroupBy guard, the paged parent pair command and a
/// live plain-root <c>AliasRoot</c> re-root. This class instead drives the still-uncovered shape arms,
/// the suppression/CTE pair branches, the provider-specific binding and scalar scopes and the alias
/// re-bind arms. The <c>CorrelatedQueryExpressionVisitor</c> reference-navigation fallbacks
/// (<c>TryResolveReferenceAccess</c>, <c>TryLowerReferenceScalar</c>) are gated by
/// <c>IsInMemory</c> and are therefore unreachable from a live SQL provider; only the SQL-reachable
/// <c>Coalesce</c> arm of <c>VisitBinary</c> is exercised here.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class Cycle7CompositionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists cp7_note;
        drop table if exists cp7_grand;
        drop table if exists cp7_child;
        drop table if exists cp7_parent;
        create table cp7_parent
        (
            id integer primary key,
            name text not null,
            score int not null,
            bucket int null
        );
        create table cp7_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null,
            kind text not null,
            amount int null
        );
        create table cp7_grand
        (
            id integer primary key,
            child_id int not null,
            sort_key int not null
        );
        create table cp7_note
        (
            id integer primary key,
            parent_id int not null,
            body text not null
        );
        -- 7001: two kept children (one childless at the grand level) plus one skipped child and two notes;
        -- 7002: one kept child with one grandchild and one note; 7003: no child and no note at all.
        insert into cp7_parent (id, name, score, bucket) values
            (7001, 'alpha', 10, 1),
            (7002, 'beta', 20, null),
            (7003, 'gamma', 30, 2);
        insert into cp7_child (id, parent_id, sort_key, kind, amount) values
            (7101, 7001, 1, 'keep', 5),
            (7102, 7001, 2, 'keep', null),
            (7103, 7001, 3, 'skip', 15),
            (7104, 7002, 1, 'keep', 25);
        insert into cp7_grand (id, child_id, sort_key) values
            (7201, 7101, 2),
            (7202, 7101, 1),
            (7203, 7104, 1);
        insert into cp7_note (id, parent_id, body) values
            (7301, 7001, 'n1'),
            (7302, 7001, 'n2'),
            (7303, 7002, 'n3');
        create index cp7_parent_score_idx on cp7_parent (score);
        """;

    public Cycle7CompositionTests()
    {
        ExecuteRaw(Ddl);

        // The graph relationships must be registered before any mapping of the parent types, so the
        // collection members are recognized as navigations and not as physical columns.
        Context.From<C7Grand>();
        Context.From<C7Note>();
        Context.From<C7Child>(b => b.HasMany(c => c.Grands, g => g.ChildId));
        Context.From<C7Parent>(b => b
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
    private static List<string> Bindings(List<C7Parent> parents) => parents
        .Select(p => p.Id + ":" + string.Join("|", p.Children.Select(c => c.Id + "[" + GrandIds(c) + "]")))
        .ToList();

    /// <summary>The class-owned grandchildren of one child, in the seed's sort-key order.</summary>
    private static string GrandIds(C7Child child)
        => child.Grands is null
            ? string.Empty
            : string.Join(",", child.Grands.OrderBy(g => g.SortKey).Select(g => g.Id));

    /// <summary>
    /// The live split-mode control of the eager graph: the kept children per parent, the childless middle
    /// child kept empty, and the parent without any child at all.
    /// </summary>
    private void AssertLiveSplitGraph()
    {
        var live = Context.From<C7Parent>()
            .Where(p => p.Id >= 7001 && p.Id <= 7003)
            .LoadWith(
                p => p.Children,
                c => c.From<C7Child>().Where(x => x.Kind == "keep"),
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();

        Bindings(live).Should().Equal(
            new[] { "7001:7101[]|7102[]", "7002:7104[]", "7003:" },
            "the live split companion of the rejected single-query child source is exact");
    }

    /// <summary>Asserts that the single-query fold rejects the composed child factory with the given shapes.</summary>
    private void AssertSingleQueryChildRejected(
        Func<IDataContext, EntityBuilder<C7Child>> childQuery,
        string shapes)
    {
        var rejected = () => Context.From<C7Parent>()
            .Where(p => p.Id == 7001)
            .LoadWith(
                p => p.Children,
                childQuery,
                p => p.Id,
                c => c.ParentId,
                EagerLoadMode.SingleQuery)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage($"*cannot fold the child query's {shapes}*");
    }

    /// <summary>Asserts that a composed raw source rejects the entity binding immediately.</summary>
    private void AssertBindingRejected(Func<EntityBuilder<TableAlias>, EntityBuilder<TableAlias>> compose)
    {
        FluentActions
            .Invoking(() => compose(Context.From("cp7_parent")).BindEntity<C7Flat>(new[] { "id", "name", "score" }))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*BindEntity must be called immediately after FromSql/From(string)*");
    }

    /// <summary>Asserts that a composed parent builder makes the scalar terminal take the ordinary path and fail on SQLite.</summary>
    private void AssertScalarRejected(Func<EntityBuilder<C7Parent>, EntityBuilder<C7Parent>> compose, string message)
    {
        FluentActions
            .Invoking(() => compose(Context.From<C7Parent>()).LongCount())
            .Should().Throw<NotSupportedException>()
            .WithMessage(message);
    }

    // ---------------------------------------------------------------------------------------------
    // 1. UnsupportedChildShapes: the Having and DistinctOn child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: a flat child factory carrying <c>Having</c> (:1222) or <c>DistinctOn</c>
    /// (:1225) reports the still-uncovered shape names to the flat single-query fold (:574) before any
    /// SQL; the split companion of the same child source is live and exact.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithHavingOrDistinctOn_ShouldRejectShapeWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>().Having(x => x.Id > 0), "Having");
        AssertSingleQueryChildRejected(c => c.From<C7Child>().DistinctOn(x => x.Id), "DistinctOn");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. UnsupportedChildShapes: the LimitBy, Final and PreWhere child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: the ClickHouse <c>LimitBy</c> (:1231), <c>Final</c> (:1237) and
    /// <c>PreWhere</c> (:1240) builder states on the child factory are reported by the flat single-query
    /// fold, which runs before the SQLite dialect would reject the rendering; the live split companion
    /// returns the exact kept rows.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithLimitByFinalOrPreWhere_ShouldRejectShapesWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>().LimitBy(1, x => x.Id), "LimitBy");
        AssertSingleQueryChildRejected(c => c.From<C7Child>().Final(), "Final");
        AssertSingleQueryChildRejected(c => c.From<C7Child>().PreWhere(x => x.Kind == "keep"), "PreWhere");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. UnsupportedChildShapes: the ArrayJoin and TableSample child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: an <c>ArrayJoin</c> over a projected array (:1243) and a from-option
    /// <c>TableSample</c> (:1246) are reported by the flat single-query fold; the split companion of the
    /// same child source is live and keeps the childless parent.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithArrayJoinOrTableSample_ShouldRejectShapesWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>().ArrayJoin(x => new[] { x.Id }), "ArrayJoin");
        AssertSingleQueryChildRejected(c => c.From<C7Child>(o => o.TableSample(50)), "TableSample");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. UnsupportedChildShapes: the Sample and ForSystemTime child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: a from-option <c>Sample</c> ratio (:1249) and a <c>ForSystemTime</c>
    /// temporal clause (:1252) on the child factory are reported by the flat single-query fold; the live
    /// split companion binds the exact kept children.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithSampleOrTemporal_ShouldRejectShapesWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>(o => o.Sample(0.5)), "Sample");
        AssertSingleQueryChildRejected(
            c => c.From<C7Child>().ForSystemTime(TemporalClause.AsOf(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))),
            "ForSystemTime");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. UnsupportedChildShapes: the row-lock and Settings child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: a child-level <c>ForShare</c> row lock (:1255) and a ClickHouse
    /// <c>Settings</c> declaration (:1265) are reported by the flat single-query fold; the split
    /// companion of the same child source is live and exact.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithRowLockOrSettings_ShouldRejectShapesWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>().ForShare(), "ForUpdate/ForShare");
        AssertSingleQueryChildRejected(c => c.From<C7Child>().Settings(("max_threads", "1")), "Settings");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. UnsupportedChildShapes: the TableHint and IndexHint child arms
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: a from-option <c>WithTableHint</c> (:1271) and <c>WithIndex</c> (:1274) on
    /// the child factory are reported by the flat single-query fold; the live split companion returns the
    /// exact kept rows, so the rejection is about the fold and not about the source itself.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithTableHintOrIndexHint_ShouldRejectShapesWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>(o => o.WithTableHint("NOLOCK")), "TableHint");
        AssertSingleQueryChildRejected(c => c.From<C7Child>(o => o.WithIndex("cp7_parent_score_idx")), "IndexHint");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. UnsupportedChildShapes: the source-level lock OF arm of the child source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a child factory whose physical source carries a from-option <c>ForShare</c>
    /// lock reports <c>ForUpdate/ForShare OF</c> (:1258). The sibling condition of that disjunction (a
    /// source lock on one of the child's joins) is not expressible here: a <c>LoadWith</c> child factory
    /// must return a plain <c>EntityBuilder&lt;TChild&gt;</c>, while <c>Join</c> returns a
    /// <c>JoinedEntityBuilder</c>, so that arm is recorded as unreachable instead of asserted. The live
    /// split companion of the same child source is exact.
    /// </summary>
    [Fact]
    public void LoadWith_ChildWithSourceLock_ShouldRejectLockOfShapeWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        AssertSingleQueryChildRejected(c => c.From<C7Child>(o => o.ForShare()), "ForUpdate/ForShare OF");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. UnsupportedChildShapes: the Cte child arm
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the child factory reads a typed CTE descriptor (<c>AsCte</c>), so its builder
    /// carries <c>Ctes</c>; the flat single-query fold reports <c>Cte</c> (:1268) before any SQL. The live
    /// split companion is the same child source expressed as a plain filtered entity query.
    /// </summary>
    [Fact]
    public void LoadWith_ChildFromCte_ShouldRejectCteShapeWithLiveSplitControl()
    {
        AssertLiveSplitGraph();

        var childCte = Context.From<C7Child>()
            .Where(x => x.Kind == "keep")
            .ToCommand()
            .AsCte("cp7_keep_children");

        AssertSingleQueryChildRejected(c => c.From(childCte), "Cte");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. CreatePairCommand: the suppressed cartesian-warning branch
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: two collection declarations on one parent whose joins each suppress the
    /// cartesian warning set <c>SuppressCartesianWarning</c> (:1661), so <c>CreatePairCommand</c> takes
    /// the suppressed arm; both collections are stitched independently and the childless parent keeps
    /// both empty without any cross-parent leakage.
    /// </summary>
    [Fact]
    public void JoinInto_TwoCollectionsWithSuppressedWarning_ShouldStitchExactPerParentRows()
    {
        var rows = Context.From<C7Parent>()
            .Where(p => p.Id >= 7001 && p.Id <= 7003)
            .JoinInto(
                Context.From<C7Child>(),
                (p, c) => p.Id == c.ParentId,
                p => p.Children,
                o => o.SuppressCartesianWarning())
            .JoinInto(
                Context.From<C7Note>(),
                (p, n) => p.Id == n.ParentId,
                p => p.Notes,
                o => o.SuppressCartesianWarning())
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(7001, 7002, 7003);
        rows[0].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 7101, 7102, 7103 });
        rows[0].Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 7301, 7302 });
        rows[0].Children.Should().OnlyContain(c => c.ParentId == 7001);
        rows[0].Notes.Should().OnlyContain(n => n.ParentId == 7001);
        rows[1].Children.Select(c => c.Id).Should().Equal(7104);
        rows[1].Notes.Select(n => n.Id).Should().Equal(7303);
        rows[2].Children.Should().BeEmpty("the childless parent keeps an empty child collection");
        rows[2].Notes.Should().BeEmpty("the note collection is independent of the child collection");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. CreatePairCommand: a CTE parent source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the parent source is a CTE descriptor (<c>AsCte</c>), so the pair command
    /// carries <c>Ctes</c> (:1641) while it joins the child collection; the exact rows prove the CTE is
    /// declared on the denormalized pair statement and every parent keeps only its own children.
    /// </summary>
    [Fact]
    public void JoinInto_CteParentSource_ShouldDeclareCteAndStitchPerParentRows()
    {
        var cte = Context.From<C7Parent>()
            .Where(p => p.Id >= 7001 && p.Id <= 7003)
            .ToCommand()
            .AsCte("cp7_parents");

        var rows = Context.From(cte)
            .JoinInto(Context.From<C7Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        rows.Select(p => p.Id).Should().Equal(7001, 7002, 7003);
        rows[0].Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 7101, 7102, 7103 });
        rows[1].Children.Select(c => c.Id).Should().Equal(7104);
        rows[2].Children.Should().BeEmpty("the CTE parent without a child keeps an empty collection");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. HasCompositionState: the provider-specific modifier states
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arms: after a single provider-specific modifier the bound raw source is composed,
    /// so <c>HasCompositionState</c> (:2433) rejects the binding through each still-uncovered operand -
    /// <c>LimitBy</c> (:2441), <c>DistinctOn</c> (:2443), <c>PreWhere</c> (:2449), <c>ArrayJoin</c>
    /// (:2453), <c>SettingsList</c> (:2463), <c>GroupingType</c> (:2464), <c>GroupingSets</c> (:2465),
    /// <c>GroupByWithTotals</c> (:2466) and <c>IsFinal</c> (:2468). The plain binding is executed as the
    /// live control.
    /// </summary>
    [Fact]
    public void BindEntity_AfterProviderSpecificModifier_ShouldRejectEachWithLiveControl()
    {
        var ids = Context.From("cp7_parent")
            .BindEntity<C7Flat>(new[] { "id", "name", "score" })
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(new[] { 7001, 7002, 7003 }, "the plain named source binds and executes as the live control");

        AssertBindingRejected(b => b.LimitBy(1, t => t["id"].AsInt));
        AssertBindingRejected(b => b.DistinctOn(t => t["id"].AsInt));
        AssertBindingRejected(b => b.PreWhere(t => t["id"].AsInt > 0));
        AssertBindingRejected(b => b.ArrayJoin(t => new[] { t["id"].AsInt }));
        AssertBindingRejected(b => b.Settings(("max_threads", "1")));
        AssertBindingRejected(b => b.GroupByRollup(t => t["bucket"].AsInt));
        AssertBindingRejected(b => b.GroupByGroupingSets(t => t["bucket"].AsInt, new[] { new[] { 0 } }));
        AssertBindingRejected(b => b.GroupBy(t => t["bucket"].AsInt).WithTotals());
        AssertBindingRejected(b => b.Final());
    }

    // ---------------------------------------------------------------------------------------------
    // 11. AliasRoot: a caller-created derived root carries its query as an explicit derived table
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the <c>create</c> callback returns a DERIVED builder (<c>From(builder)</c>),
    /// so <c>AliasRoot</c> takes the <c>rooted.Query is not null</c> arm (:3321) and materializes the
    /// query as an explicit derived-table source instead of leaving the projection as the root
    /// (<c>ResolveJoinBase</c> would refuse to join it). The live rows prove the derived source executes.
    /// </summary>
    [Fact]
    public void AliasRoot_OnDerivedCreatedRoot_ShouldMaterializeDerivedSourceAndReturnRows()
    {
        var ids = Context.From<C7Parent>()
            .AliasRoot<EntityBuilder<C7Parent>, C7Parent>(c => c.From(c.From<C7Parent>().Where(p => p.Id <= 7003)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        ids.Should().Equal(new[] { 7001, 7002, 7003 }, "the re-rooted derived source keeps the caller's derived query");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. AliasRoot: a projection without Item1 cannot receive entity-typed query state
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a root carrying a predicate is re-rooted onto a projection type that derives
    /// from no <c>Projection</c>, so <c>ProjectionAliasCache.GetItem1Property</c> misses and the seam
    /// fails closed with the Item1 diagnostic (:3335-3336) instead of silently dropping the predicate.
    /// The live root-alias control proves the same predicate is preserved when the projection does
    /// expose <c>Item1</c>.
    /// </summary>
    [Fact]
    public void AliasRoot_OnProjectionWithoutItem1_ShouldRejectAndKeepLiveControl()
    {
        FluentActions
            .Invoking(() => Context.From<C7Parent>()
                .Where(p => p.Id > 0)
                .AliasRoot<EntityBuilder<C7Flat>, C7Flat>(c => c.From<C7Flat>()))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires the root projection*");

        var ids = Context.From<C7Parent>()
            .Where(p => p.Id > 7001)
            .WithAlias(Alias.Root)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        ids.Should().Equal(new[] { 7002, 7003 }, "the alias-root control preserves the pre-alias predicate");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. AliasRoot: the predicate re-bind
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a predicate written before <c>.WithAlias</c> is re-bound onto the root
    /// slot's <c>Item1</c> (:3346-3349), so the re-rooted builder filters on the alias source and keeps
    /// the exact rows while dropping the filtered-out parent.
    /// </summary>
    [Fact]
    public void WithAlias_WithPredicateBeforeAlias_ShouldRebindConditionAndReturnRows()
    {
        var ids = Context.From<C7Parent>()
            .Where(p => p.Name != "beta")
            .WithAlias(Alias.Root)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        ids.Should().Equal(new[] { 7001, 7003 }, "the pre-alias predicate survives the re-root");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. AliasRoot: the grouping and having re-binds
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a <c>GroupBy</c> (:3358) and a <c>Having</c> (:3352) written before
    /// <c>.WithAlias</c> are both re-bound onto the root slot, so the grouped query keeps the having
    /// filter by value and returns exactly the surviving parents.
    /// </summary>
    [Fact]
    public void WithAlias_WithGroupingAndHavingBeforeAlias_ShouldRebindBothAndReturnRows()
    {
        var ids = Context.From<C7Parent>()
            .GroupBy(p => p.Id)
            .Having(p => p.Id >= 7002)
            .WithAlias(Alias.Root)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        ids.Should().Equal(new[] { 7002, 7003 }, "the pre-alias grouping and having survive the re-root");
    }

    // ---------------------------------------------------------------------------------------------
    // 15. AliasRoot: the preWhere re-bind
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a ClickHouse <c>PreWhere</c> written before <c>.WithAlias</c> is re-bound
    /// onto the root slot (:3364-3367); SQLite then rejects the PREWHERE rendering, which proves the
    /// re-bind ran, and the alias-root control without PREWHERE returns every row.
    /// </summary>
    [Fact]
    public void WithAlias_WithPreWhereBeforeAlias_ShouldRebindAndKeepLiveControl()
    {
        FluentActions
            .Invoking(() => Context.From<C7Parent>()
                .PreWhere(p => p.Id > 0)
                .WithAlias(Alias.Root)
                .Select(p => p.Item1.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*PREWHERE*");

        var ids = Context.From<C7Parent>()
            .Where(p => p.Id > 0)
            .WithAlias(Alias.Root)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        ids.Should().Equal(new[] { 7001, 7002, 7003 }, "the alias-root control without PREWHERE is live");
    }

    // ---------------------------------------------------------------------------------------------
    // 16. IsPlainScalarScope: the plain fast path
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a builder with no SQL-affecting state keeps every <c>IsPlainScalarScope</c>
    /// (:1300) conjunct true, so <c>Count</c>, <c>LongCount</c> and the normalized member <c>Sum</c>
    /// execute through the cached scalar artifact and return the exact values twice in a row.
    /// </summary>
    [Fact]
    public void ScalarTerminals_OnPlainScope_ShouldReturnExactValues()
    {
        Context.From<C7Parent>().Count().Should().Be(3);
        Context.From<C7Parent>().Count().Should().Be(3, "the repeated plain terminal reuses the scalar artifact");
        Context.From<C7Parent>().LongCount().Should().Be(3);
        Context.From<C7Parent>().Sum(p => p.Score).Should().Be(60);
    }

    // ---------------------------------------------------------------------------------------------
    // 17. IsPlainScalarScope: supported composition falls back to the ordinary path
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a predicate, an ordering, a join and a grouping each flip one
    /// <c>IsPlainScalarScope</c> conjunct to false, so every scalar terminal below takes the ordinary
    /// path and still returns the exact aggregated value of the composed query.
    /// </summary>
    [Fact]
    public void ScalarTerminals_WithSupportedComposition_ShouldReturnExactValues()
    {
        Context.From<C7Parent>().Where(p => p.Id != 7002).Count().Should().Be(2);
        Context.From<C7Parent>().OrderBy(p => p.Id).LongCount().Should().Be(3);
        Context.From<C7Parent>()
            .Join(Context.From<C7Child>(), (p, c) => p.Id == c.ParentId)
            .LongCount()
            .Should().Be(4, "the join contributes the four child rows");
        Context.From<C7Parent>().GroupBy(p => p.Id).LongCount().Should().Be(1, "every id forms a one-row group");
        Context.From<C7Parent>().Limit(2).LongCount().Should().Be(3, "the limit applies after the aggregate row");
    }

    // ---------------------------------------------------------------------------------------------
    // 18. IsPlainScalarScope: the provider-specific modifier scopes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: each ClickHouse-only modifier flips its own <c>IsPlainScalarScope</c>
    /// conjunct to false, so the scalar terminal takes the ordinary path and the SQLite dialect rejects
    /// the rendering with its exact diagnostic (<c>FINAL</c>, <c>SETTINGS</c>, <c>PREWHERE</c>,
    /// <c>LIMIT BY</c>, <c>ARRAY JOIN</c>, <c>SAMPLE</c>). The plain control count is live.
    /// </summary>
    [Fact]
    public void ScalarTerminals_WithProviderSpecificModifier_ShouldRejectRenderingWithLiveControl()
    {
        Context.From<C7Parent>().LongCount().Should().Be(3, "the plain control is live");

        AssertScalarRejected(b => b.Final(), "*FINAL*");
        AssertScalarRejected(b => b.Settings(("max_threads", "1")), "*SETTINGS*");
        AssertScalarRejected(b => b.PreWhere(p => p.Id > 0), "*PREWHERE*");
        AssertScalarRejected(b => b.LimitBy(1, p => p.Id), "*LIMIT BY*");
        AssertScalarRejected(b => b.ArrayJoin(p => new[] { p.Id }), "*ARRAY JOIN*");
        FluentActions
            .Invoking(() => Context.From<C7Parent>(o => o.Sample(0.5)).LongCount())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*SAMPLE*");
    }

    // ---------------------------------------------------------------------------------------------
    // 19. IsPlainScalarScope: the lock, temporal and hint scopes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a row lock, a temporal clause, a table hint and a tables-in-scope hint each
    /// flip their own <c>IsPlainScalarScope</c> conjunct, so the scalar terminal falls back and SQLite
    /// rejects the rendering; an index hint is one of the few hints SQLite renders, so it executes and
    /// returns the exact count.
    /// </summary>
    [Fact]
    public void ScalarTerminals_WithLockTemporalOrHint_ShouldRejectOrExecuteExactly()
    {
        AssertScalarRejected(b => b.ForUpdate(), "*FOR UPDATE/FOR SHARE*");
        AssertScalarRejected(
            b => b.ForSystemTime(TemporalClause.AsOf(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))),
            "*FOR SYSTEM_TIME*");
        AssertScalarRejected(b => b.WithTablesInScopeHint("cp7_parent"), "*Tables-in-scope hints*");
        FluentActions
            .Invoking(() => Context.From<C7Parent>(o => o.TableSample(50)).LongCount())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*TABLESAMPLE*");
        FluentActions
            .Invoking(() => Context.From<C7Parent>(o => o.WithTableHint("NOLOCK")).LongCount())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Table hints*");

        Context.From<C7Parent>(o => o.WithIndex("cp7_parent_score_idx")).LongCount().Should().Be(3,
            "SQLite renders the index hint and the scalar returns the exact count");
    }

    // ---------------------------------------------------------------------------------------------
    // 20. IsPlainScalarScope: the grouping-modifier scopes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>GroupByRollup</c>, <c>GroupByGroupingSets</c> and <c>WithTotals</c> flip
    /// the <c>GroupingType</c>/<c>GroupingSets</c>/<c>GroupByWithTotals</c> conjuncts, so the scalar
    /// terminal takes the ordinary path instead of the cached artifact. SQLite declares the ANSI
    /// super-aggregate forms but its bundled engine has no such function, so the ordinary rendering fails
    /// at the engine (the exact engine diagnostic is asserted), while <c>WITH TOTALS</c> is rejected by
    /// the dialect before execution. The plain grouped control without a modifier is live.
    /// </summary>
    [Fact]
    public void ScalarTerminals_WithGroupingModifier_ShouldLeaveTheFastPathAndKeepLiveControl()
    {
        Context.From<C7Parent>().GroupBy(p => p.Id).LongCount().Should().Be(1, "the plain grouped control is live");

        FluentActions
            .Invoking(() => Context.From<C7Parent>().GroupByRollup(p => p.Id).LongCount())
            .Should().Throw<Microsoft.Data.Sqlite.SqliteException>()
            .WithMessage("*rollup*");
        FluentActions
            .Invoking(() => Context.From<C7Parent>().GroupByGroupingSets(p => p.Id, new[] { new[] { 0 } }).LongCount())
            .Should().Throw<Microsoft.Data.Sqlite.SqliteException>();
        AssertScalarRejected(b => b.GroupBy(p => p.Id).WithTotals(), "*WITH TOTALS*");
    }

    // ---------------------------------------------------------------------------------------------
    // 21. IsPlainScalarScope: raw and derived source scopes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a bound raw source (<c>_from</c>) and a derived source (<c>_query</c>) each
    /// flip their own <c>IsPlainScalarScope</c> conjunct, so the scalar terminal takes the ordinary path
    /// over the source and returns the exact count.
    /// </summary>
    [Fact]
    public void ScalarTerminals_OnRawAndDerivedSources_ShouldReturnExactCounts()
    {
        Context.From("cp7_parent")
            .BindEntity<C7Flat>(new[] { "id", "name", "score" })
            .LongCount()
            .Should().Be(3, "the bound raw source falls back to the ordinary scalar path");

        Context.From(Context.From<C7Parent>().Where(p => p.Id <= 7002))
            .LongCount()
            .Should().Be(2, "the derived source falls back to the ordinary scalar path");
    }

    // ---------------------------------------------------------------------------------------------
    // 22. VisitBinary: the SQL-reachable Coalesce arm inside a correlated predicate
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the correlated subquery reads a nullable column through <c>??</c>, so
    /// <c>VisitBinary</c> (:1683) evaluates the <c>Coalesce</c> node (:1712) with <c>IsInMemory == false</c>
    /// and falls through to the base visit; the exact parents prove the NULL amount is compensated to 0
    /// and never matches the outer score. The reference-navigation arms of the same visitor are gated by
    /// <c>IsInMemory</c> and stay unreachable for a live SQL provider.
    /// </summary>
    [Fact]
    public void CorrelatedPredicate_WithCoalescedNullableColumn_ShouldCompensateNullPerParent()
    {
        var ids = Context.From<C7Parent>()
            .Where(p => p.Id >= 7001 && p.Id <= 7003)
            .Where(p => SqlFunctions.Sql.exists(
                Context.From<C7Child>().Where(c => c.ParentId == p.Id && (c.Amount ?? 0) > p.Score)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        ids.Should().Equal(new[] { 7001, 7002 },
            "15 > 10 and 25 > 20 match; the NULL amount is compensated to 0 and 7003 has no child at all");
    }

    // ---------------------------------------------------------------------------------------------
    // 23. The join-modifier guard of a SEMI join runs before the dialect gate
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: a <c>SemiJoin</c> carrying a ClickHouse strictness modifier is rejected by the
    /// modifier guard of <c>MakeJoin</c> (204-207) before the dialect's <c>SupportsSemiAntiJoin</c> gate
    /// (209-212) is consulted; the plain inner join over the same class-owned rows runs.
    /// </summary>
    [Fact]
    public void Join_SemiJoinWithStrictnessModifier_ShouldBeRejectedBeforeTheDialectGate()
    {
        var live = Context.From<C7Parent>()
            .Join(Context.From<C7Child>(), (p, c) => p.Id == c.ParentId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        live.Should().Equal(new[] { 7001, 7001, 7001, 7002 }, "the live inner-join counterpart");

        var modified = () => Context.From<C7Parent>()
            .SemiJoin(Context.From<C7Child>(), (p, c) => p.Id == c.ParentId, j => j.WithStrictness(JoinStrictness.Any))
            .Select(p => p.Id)
            .ToList();

        modified.Should().Throw<NotSupportedException>()
            .WithMessage("*join modifier cannot be applied to a Semi join*");
    }

    // ---------------------------------------------------------------------------------------------
    // 24. The AsEntityBuilder adapter on a NON-navigation receiver is rejected
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>TryGetAdapterNavigation</c> (502-516) resolves the adapter receiver as a
    /// declared navigation; a captured list is not rooted at an outer parameter, so the adapter throws
    /// (511-515) while the declared collection adapter of the same shape runs.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_AdapterOnCapturedList_ShouldBeRejectedWhileTheDeclaredCollectionRuns()
    {
        var captured = Context.From<C7Child>().Where(c => c.Kind == "keep").ToList();
        captured.Should().HaveCount(3);

        var live = Context.From<C7Parent>()
            .Where(p => p.Children.AsEntityBuilder().Any())
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        live.Should().Equal(new[] { 7001, 7002 }, "only the parents with a child survive the declared-collection adapter");

        var rejected = () => Context.From<C7Parent>()
            .Where(p => captured.AsEntityBuilder().Any())
            .Select(p => p.Id)
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*AsEntityBuilder is only supported on a declared navigation*");
    }

    // ---------------------------------------------------------------------------------------------
    // 25. The adapter's terminal allow-list rejects a non-count terminal
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>LowerCollectionTerminal</c>'s terminal allow-list (544-548) rejects a
    /// <c>First()</c> terminal of the collection adapter ("only parameterless Any/Count/LongCount"),
    /// while <c>LongCount()</c> on the same adapter returns the exact per-parent counts.
    /// </summary>
    [Fact]
    public void CorrelatedSubquery_AdapterWithUnsupportedTerminal_ShouldBeRejectedWhileLongCountRuns()
    {
        var rows = Context.From<C7Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Count = p.Children.AsEntityBuilder().LongCount() })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(7001, 7002, 7003);
        rows.Select(r => r.Count).Should().Equal(new long[] { 3, 1, 0 }, "the declared collection adapter counts per parent");

        var rejected = () => Context.From<C7Parent>()
            .Select(p => new { p.Id, First = p.Children.AsEntityBuilder().First().Id })
            .ToList();

        rejected.Should().Throw<NotSupportedException>()
            .WithMessage("*navigation terminal 'First' is not supported*");
    }

    // ---------------------------------------------------------------------------------------------
    // 26. PredicateTranslator: the binary/unary operator arms and the null-presence projection
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// <c>PredicateTranslator.VisitUnary</c> (:47-61) and the operator switch of <c>VisitBinary</c>
    /// (:458-508) executed over a live parent row (7001: score 10, bucket 1, name alpha): the arithmetic,
    /// bitwise, shift, comparison and unary arms, the logical <c>&amp;&amp;</c>/<c>||</c> condition arms,
    /// the string-concatenation arm (:419), the numeric conversion cast (:32-43) and the null-aware
    /// presence arms (:354-378) from both operand orders.
    /// </summary>
    [Fact]
    public void PredicateOperatorProjections_ShouldExecute()
    {
        var r = Context.From<C7Parent>()
            .Where(p => p.Id == 7001)
            .Select(p => new
            {
                Add = p.Score + 5,
                And = p.Score & 6,
                Divide = p.Score / 4,
                Modulo = p.Score % 3,
                Multiply = p.Score * 2,
                Or = p.Score | 1,
                Subtract = p.Score - 3,
                LeftShift = p.Score << 1,
                RightShift = p.Score >> 1,
                Negate = -p.Score,
                OnesComplement = ~p.Score,
                Greater = p.Score > 5,
                GreaterOrEqual = p.Score >= 10,
                Less = p.Score < 100,
                LessOrEqual = p.Score <= 10,
                Equal = p.Score == 10,
                NotEqual = p.Score != 0,
                AndAlso = p.Score > 0 && p.Score < 100,
                OrElse = p.Score < 0 || p.Score > 5,
                Concat = p.Name + "-x",
                Widened = (long)p.Score,
                Presence = p.Bucket == null,
                PresenceNot = p.Bucket != null,
                PresenceReversed = null == p.Bucket,
                PresenceReversedNot = null != p.Bucket,
                Coalesced = p.Bucket ?? -1
            })
            .First();

        r.Add.Should().Be(15);
        r.And.Should().Be(2);
        r.Divide.Should().Be(2);
        r.Modulo.Should().Be(1);
        r.Multiply.Should().Be(20);
        r.Or.Should().Be(11);
        r.Subtract.Should().Be(7);
        r.LeftShift.Should().Be(20);
        r.RightShift.Should().Be(5);
        r.Negate.Should().Be(-10);
        r.OnesComplement.Should().Be(-11);
        r.Greater.Should().BeTrue();
        r.GreaterOrEqual.Should().BeTrue();
        r.Less.Should().BeTrue();
        r.LessOrEqual.Should().BeTrue();
        r.Equal.Should().BeTrue();
        r.NotEqual.Should().BeTrue();
        r.AndAlso.Should().BeTrue();
        r.OrElse.Should().BeTrue();
        r.Concat.Should().Be("alpha-x");
        r.Widened.Should().Be(10L);
        r.Presence.Should().BeFalse();
        r.PresenceNot.Should().BeTrue();
        r.PresenceReversed.Should().BeFalse();
        r.PresenceReversedNot.Should().BeTrue();
        r.Coalesced.Should().Be(1);
    }

    /// <summary>
    /// The parameter-only arms of the unary translation (<c>VisitOnesComplement</c> :89-93 and
    /// <c>VisitUnaryOperator</c> :107-111): the same shape runs twice on one context with a captured
    /// bound, so the cached plan re-walks <c>-p.Score</c> and <c>~p.Score</c> in parameter mode and the
    /// second run reads its own bound.
    /// </summary>
    [Fact]
    public void PredicateUnaryOperators_ParameterPass_ShouldWalkTheOperands()
    {
        var floor = 5;

        List<string> Read() => Context.From<C7Parent>()
            .Where(p => p.Id == 7001 && p.Score > floor)
            .Select(p => new { Negated = -p.Score, Complement = ~p.Score })
            .ToList()
            .Select(r => r.Negated + "/" + r.Complement)
            .ToList();

        Read().Should().Equal("-10/-11");

        floor = 11;

        Read().Should().BeEmpty("the cached plan re-reads the captured bound in parameter mode");
    }

    // ---------------------------------------------------------------------------------------------
    // 27. PredicateTranslator: switch/CASE and the unsupported binary shapes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The switch-expression arms of <c>PredicateTranslator.VisitSwitch</c> (:237-340) reached through a
    /// hand-built expression tree (C# forbids a switch expression inside a lambda tree): two test values
    /// sharing one body, a default arm and the parameter-only pass (:250-271) when the same shape runs
    /// twice with a captured bound.
    /// </summary>
    [Fact]
    public void PredicateSwitchExpression_ShouldRenderCasesDefaultAndParameterPass()
    {
        var min = 10;
        var exp = SwitchOfScore("high", (new[] { 10, 20 }, "low"), (new[] { 30 }, "high"));

        List<string> Read() => Context.From<C7Parent>()
            .Where(p => p.Score >= min)
            .OrderBy(p => p.Id)
            .Select(exp)
            .ToList();

        Read().Should().Equal("low", "low", "high");
        Read().Should().Equal(new[] { "low", "low", "high" }, "the second run reuses the cached switch plan");

        min = 30;

        Read().Should().Equal(new[] { "high" }, "the cached plan re-walks the switch in parameter mode");
    }

    /// <summary>
    /// <c>VisitSwitch</c> without any case (:274-283): the whole expression is just its default body.
    /// </summary>
    [Fact]
    public void PredicateSwitchExpression_WithoutCases_ShouldRenderTheDefaultBody()
    {
        var parameter = Expression.Parameter(typeof(C7Parent), "p");
        var body = Expression.Switch(
            Expression.Property(parameter, nameof(C7Parent.Score)),
            Expression.Constant("only"));
        var exp = Expression.Lambda<Func<C7Parent, string>>(body, parameter);

        var values = Context.From<C7Parent>()
            .Where(p => p.Id >= 7001)
            .OrderBy(p => p.Id)
            .Select(exp)
            .ToList();

        values.Should().Equal("only", "only", "only");
    }

    /// <summary>
    /// A switch expression built with a custom comparison method is rejected before any SQL (:241).
    /// </summary>
    [Fact]
    public void PredicateSwitchExpression_WithCustomComparison_ShouldBeRejected()
    {
        var parameter = Expression.Parameter(typeof(C7Parent), "p");
        var comparison = typeof(Cycle7CompositionTests)
            .GetMethod(nameof(SwitchEquality), BindingFlags.Static | BindingFlags.NonPublic)!;
        var body = Expression.Switch(
            typeof(int),
            Expression.Property(parameter, nameof(C7Parent.Score)),
            Expression.Constant(0),
            comparison,
            new[] { Expression.SwitchCase(Expression.Constant(1), Expression.Constant(10)) });
        var exp = Expression.Lambda<Func<C7Parent, int>>(body, parameter);

        FluentActions.Invoking(() => Context.From<C7Parent>()
                .Where(p => p.Id == 7001)
                .Select(exp)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("A switch expression with a custom comparison method is not supported");
    }

    private static bool SwitchEquality(int left, int right) => left == right;

    private static Expression<Func<C7Parent, string>> SwitchOfScore(
        string @default,
        params (int[] Tests, string Body)[] cases)
    {
        var parameter = Expression.Parameter(typeof(C7Parent), "p");
        var switchCases = cases
            .Select(c => Expression.SwitchCase(
                Expression.Constant(c.Body),
                c.Tests.Select(t => (Expression)Expression.Constant(t))))
            .ToArray();
        var body = Expression.Switch(
            Expression.Property(parameter, nameof(C7Parent.Score)),
            Expression.Constant(@default),
            switchCases);

        return Expression.Lambda<Func<C7Parent, string>>(body, parameter);
    }

    /// <summary>
    /// The two binary shapes with no portable SQL form: the exclusive-or operator reaches the default arm
    /// of the operator switch (:505-506), and an array index over a server-side array is rejected before
    /// the operator dispatch (:346-348).
    /// </summary>
    [Fact]
    public void PredicateUnsupportedBinaryShapes_ShouldBeRejected()
    {
        FluentActions.Invoking(() => Context.From<C7Parent>()
                .Where(p => p.Id == 7001)
                .Select(p => p.Score ^ 1)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("ExclusiveOr");

        FluentActions.Invoking(() => Context.From<C7Parent>()
                .Where(p => p.Id == 7001)
                .Select(p => new[] { p.Score }[0])
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*An array index is supported only on a captured array*");
    }

    /// <summary>Parent of the class-owned composition graph.</summary>
    [SqlTable("cp7_parent")]
    public sealed class C7Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }

        [Column("bucket")]
        public int? Bucket { get; set; }

        public ICollection<C7Child> Children { get; } = new List<C7Child>();

        public ICollection<C7Note> Notes { get; } = new List<C7Note>();
    }

    /// <summary>Child of the class-owned composition graph, with a per-parent sort key and a kind filter.</summary>
    [SqlTable("cp7_child")]
    public sealed class C7Child
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

        [Column("amount")]
        public int? Amount { get; set; }

        public ICollection<C7Grand> Grands { get; } = new List<C7Grand>();
    }

    /// <summary>Grandchild of the class-owned composition graph.</summary>
    [SqlTable("cp7_grand")]
    public sealed class C7Grand
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("child_id")]
        public int ChildId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }

    /// <summary>Note collection of the parent.</summary>
    [SqlTable("cp7_note")]
    public sealed class C7Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>Flat entity bound to the class-owned parent table for the binding and alias facts.</summary>
    [SqlTable("cp7_parent")]
    public sealed class C7Flat
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }
}
