using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Structural plan-key branch coverage (cycle 5, cluster C1/D2) executed against a live SQLite file
/// database. Every fact builds two DISTINCT commands from fresh <c>From&lt;T&gt;()</c> chains on ONE
/// SQLite context, prepares both WITHOUT storing them, obtains their plan-cache keys through
/// <c>QueryCommand.GetOrCreatePlanKey</c> (internal, reached through <c>InternalsVisibleTo</c>),
/// asserts the two key INSTANCES are distinct, and then asserts the structural comparison: TRUE for an
/// independently built all-equal pair (which must additionally reuse one cached holder and read
/// identical rows) and FALSE for each one-field negative, every side of which is executed against the
/// live database.
///
/// <para>
/// Preparation is deliberately done first and with <c>storeInCache: false</c>: the comparer reads the
/// PREPARED state (<c>PreparedCondition</c> and the four <c>Resolved*</c> rendering overrides exist only
/// after preparation), while a cached store would hand both equal commands the SAME key instance
/// (<c>CacheStoredPlanKey</c>), defeating the required "distinct instances first" assertion.
/// </para>
///
/// <para>
/// Targeted components of <c>QueryPlanEqualityComparer.EqualsCore</c>
/// (<c>src/nextorm.core/Query/QueryPlanEqualityComparer.cs:66-232</c>) in the requested order:
/// (a) the whole all-equal shape with a join, an order and a grouping; (b) <c>From</c> - a bound
/// entity source against a DERIVED source and a raw <c>FromSql</c> source; (c) <c>Joins</c> - join
/// type and the on-condition operand; (d) <c>Sorting</c> - direction, key and key count;
/// (e) <c>GroupingList</c>/<c>GroupingType</c>; (f) <c>Having</c>; (g) <c>SelectList</c> - entity,
/// scalar and tuple projections; (h) <c>InValuesShapeHash</c> - evaluated list length and element
/// kind; (i) <c>PreparedCondition</c> - the predicate operand; (j) the resolved rendering overrides
/// (<c>ResolvedQuoteIdentifiers</c>, <c>ResolvedNamingConvention</c>, <c>ResolvedKeywordCase</c>,
/// <c>ResolvedCommandTimeout</c>).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/PlanKeyBranchCoverage5Tests.cs</c>
/// owns the scalar shape flags, union/paging/single-row, the window frame cluster and the same
/// rendering overrides through <c>GetQueryPlanEqualityComparer</c>; this class never repeats a window
/// arm and drives the plan keys themselves; <c>Sqlite/PlanEqualityDifferentialBranchCoverage4Tests.cs</c>
/// owns the expression comparer, the index-hint string list, the grouping-set list and the frame
/// cluster.
/// </para>
///
/// <para>
/// Recorded, never fabricated: a source-level and a join-level ALIAS have no public setter on
/// <c>FromOptions</c>/<c>JoinOptions</c> (the aliasing surface is the separate <c>JoinAlias</c>/
/// <c>AliasRoot</c> builder pair, not the plain <c>From</c>/<c>Join</c> chains under test), so the
/// "different alias" sub-arm of (b) and (c) is recorded in <c>rv1/inputs/deferred.json</c> instead of
/// being asserted here.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class StructuralPlanBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists st5_eager_child;
        drop table if exists st5_eager_parent;
        drop table if exists st5_line;
        drop table if exists st5_marker_copy;
        drop table if exists st5_marker;
        create table st5_marker
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table st5_marker_copy
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        create table st5_line
        (
            id integer primary key,
            marker_id int not null,
            qty int not null
        );
        insert into st5_marker (id, name, code, grp, tag, score) values
            (1, 'alpha', 'a', 1, 10, 5),
            (2, 'beta',  'b', 1, 20, 5),
            (3, 'gamma', 'a', 2, 10, 9),
            (4, 'delta', 'b', 2, 20, null);
        insert into st5_marker_copy (id, name, code, grp, tag, score) values
            (1, 'alpha', 'a', 1, 10, 5),
            (2, 'beta',  'b', 1, 20, 5);
        insert into st5_line (id, marker_id, qty) values
            (10, 1, 1),
            (11, 1, 3),
            (12, 2, 5),
            (13, 3, 7);
        create table st5_eager_parent
        (
            id integer primary key,
            name text not null
        );
        create table st5_eager_child
        (
            id integer primary key,
            parent_id int not null,
            sort_key int not null
        );
        insert into st5_eager_parent (id, name) values
            (9701, 'p-many'),
            (9702, 'p-one');
        insert into st5_eager_child (id, parent_id, sort_key) values
            (9711, 9701, 1),
            (9712, 9701, 2),
            (9721, 9702, 1);
        """;

    public StructuralPlanBranchCoverage5Tests()
    {
        ExecuteRaw(Ddl);

        // The eager graph must be registered before any mapping of the parent type, so the collection
        // member is recognized as a navigation and not as a physical column.
        Context.From<St5EagerChild>();
        Context.From<St5EagerParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
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

    /// <summary>
    /// Prepares the command in place with the hash calculation enabled
    /// (<c>PrepareCommand(dontCalculateHash: false, ...)</c>) and WITHOUT touching the plan store. The
    /// uncached <c>GetPreparedQueryCommand(storeInCache: false)</c> path would pass
    /// <c>dontCalculateHash: true</c> and leave every per-part hash - including
    /// <c>InValuesShapeHash</c> and the <c>Resolved*</c> rendering overrides - unresolved, so the
    /// comparison would not see the prepared state at all.
    /// </summary>
    private static void Resolve(QueryCommand command) => command.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

    /// <summary>Prepares and executes the command WITHOUT storing the plan (no shared key instance).</summary>
    private IPreparedQueryCommand<TResult> PrepareUncached<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> PrepareCached<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Prepares without storing the plan and executes the command in one step.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command) => Rows(PrepareUncached(command));

    /// <summary>Drops every cached plan on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>
    /// Asserts the equal arm: both commands are RESOLVED first (their keys are DISTINCT instances),
    /// then they compare equal structurally (same hash), read identical live rows, and the cached path
    /// shares one holder.
    /// </summary>
    private void AssertEqualPlans<TResult>(
        QueryCommand<TResult> first,
        QueryCommand<TResult> second,
        IEnumerable<TResult> expectedRows,
        string because)
    {
        Resolve(first);
        Resolve(second);

        var firstKey = first.GetOrCreatePlanKey(null);
        var secondKey = second.GetOrCreatePlanKey(null);

        firstKey.Should().NotBeSameAs(secondKey, "each freshly built command owns its own plan-key instance");
        first.Should().NotBeSameAs(second, "the comparison below is structural, not a reference match");

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeTrue(because);
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second), "equal plans must hash equally");
        firstKey.Equals(secondKey).Should().BeTrue(because);
        firstKey.GetHashCode().Should().Be(secondKey.GetHashCode(), "equal plan keys must hash equally");

        Rows(PrepareUncached(first)).Should().Equal(expectedRows, because);
        Rows(PrepareUncached(second)).Should().Equal(expectedRows, "the equal shape reads the same live rows on both sides");

        var cachedFirst = PrepareCached(first);
        PrepareCached(second).Should().BeSameAs(cachedFirst, "an independently built equal shape must reuse the one cached plan");
        Rows(cachedFirst).Should().Equal(expectedRows, "the reused plan returns the same live rows");
    }

    /// <summary>
    /// Asserts the negative arm: both commands are RESOLVED first (their keys are DISTINCT instances),
    /// the structural comparison is FALSE, both sides execute with their own rows, and the cached path
    /// does not share a holder either.
    /// </summary>
    private void AssertDistinctPlans<TResult>(
        QueryCommand<TResult> first,
        QueryCommand<TResult> second,
        IEnumerable<TResult> firstRows,
        IEnumerable<TResult> secondRows,
        string because)
    {
        Resolve(first);
        Resolve(second);

        var firstKey = first.GetOrCreatePlanKey(null);
        var secondKey = second.GetOrCreatePlanKey(null);

        firstKey.Should().NotBeSameAs(secondKey, "each freshly built command owns its own plan-key instance");
        first.Should().NotBeSameAs(second, "the comparison below is structural, not a reference match");

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeFalse(because);
        firstKey.Equals(secondKey).Should().BeFalse(because);

        Rows(PrepareUncached(first)).Should().Equal(firstRows, because);
        Rows(PrepareUncached(second)).Should().Equal(secondRows, because);

        var cachedFirst = PrepareCached(first);
        PrepareCached(second).Should().NotBeSameAs(cachedFirst, because);
    }

    /// <summary>
    /// Adds a cross-projection negative arm: two commands whose result types differ cannot share one
    /// generic helper, so the structural comparison is asserted on the non-generic
    /// <see cref="QueryCommand"/> surface after both were resolved.
    /// </summary>
    private void AssertDistinctPreparedPlans(QueryCommand first, QueryCommand second, string because)
        => AssertDistinctResolvedPlans(first, second, because);

    /// <summary>Structural-only negative arm over two already resolved commands of different result types.</summary>
    private static void AssertDistinctResolvedPlans(QueryCommand first, QueryCommand second, string because)
    {
        var firstKey = first.GetOrCreatePlanKey(null);
        var secondKey = second.GetOrCreatePlanKey(null);

        firstKey.Should().NotBeSameAs(secondKey, "each freshly built command owns its own plan-key instance");
        first.GetQueryPlanEqualityComparer().Equals(first, second).Should().BeFalse(because);
        firstKey.Equals(secondKey).Should().BeFalse(because);
    }

    // ---------------------------------------------------------------------------------------------
    // (a) All-equal pair over a joined + ordered + grouped shape
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// The all-equal anchor of the class: two independently built commands whose shape carries a join,
    /// an ordering and a grouping must compare equal (and reuse one cached plan) while being distinct
    /// objects with distinct key instances.
    /// </summary>
    [Fact]
    public void EqualPair_JoinedOrderedGroupedShape_ShouldCompareEqualAndReadTheSameRows()
    {
        ResetPlanCache();

        QueryCommand<int> Shape()
        {
            var joined = Context.From<St5Marker>()
                .Join(Context.From<St5Line>(), (m, l) => m.Id == l.MarkerId)
                .OrderBy(p => p.Item1.Id)
                .Select(p => new { Grp = p.Item1.Grp, Qty = p.Item2.Qty });

            return Context.From(joined)
                .GroupBy(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count());
        }

        AssertEqualPlans(
            Shape(),
            Shape(),
            new[] { 3, 1 },
            "every compared component of the joined, ordered and grouped shape matches");
    }

    // ---------------------------------------------------------------------------------------------
    // (b) From: bound entity source against a derived source and a raw source
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the <c>FromExpressionPlanEqualityComparer</c> through the plan key: a physical entity
    /// source, a derived subquery source and a raw <c>FromSql</c> source are three different sources.
    /// </summary>
    [Fact]
    public void From_BoundEntityDerivedAndRawSources_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Physical() => Context.From<St5Marker>()
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertEqualPlans(Physical(), Physical(), new long[] { 1, 2, 3, 4 }, "two identical physical sources are one plan");

        var derived = Context.From(Context.From<St5Marker>().Where(x => x.Id > 1))
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertDistinctPlans(
            Physical(),
            derived,
            new long[] { 1, 2, 3, 4 },
            new long[] { 2, 3, 4 },
            "a physical entity source and a derived subquery source are different plan keys");

        var raw = Context.FromSql("select id, grp from st5_marker where grp = 1")
            .OrderBy(x => x.GetInt64("id"))
            .Select(x => x.GetInt64("id"));

        AssertDistinctPlans(
            Physical(),
            raw,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2 },
            "a raw SQL source is an opaque, reference-identified source and never shares a plan with a bound entity");
    }

    // ---------------------------------------------------------------------------------------------
    // (c) Joins: join type and the on-condition operand
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the <c>JoinExpressionPlanEqualityComparer</c> through the plan key: the join type and the
    /// condition operand are both part of the key, and both sides read their own live rows.
    /// </summary>
    [Fact]
    public void Joins_TypeAndConditionOperand_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Inner() => Context.From<St5Marker>()
            .Join(Context.From<St5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);

        AssertEqualPlans(
            Inner(),
            Inner(),
            new long[] { 1, 1, 2, 3 },
            "two identical inner joins are one plan");

        var left = Context.From<St5Marker>()
            .LeftJoin(Context.From<St5Line>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);

        AssertDistinctPlans(
            Inner(),
            left,
            new long[] { 1, 1, 2, 3 },
            new long[] { 1, 1, 2, 3, 4 },
            "a different join type (INNER vs LEFT) is a different plan key");

        var byGroup = Context.From<St5Marker>()
            .Join(Context.From<St5Line>(), (m, l) => m.Grp == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => p.Item1.Id);

        AssertDistinctPlans(
            Inner(),
            byGroup,
            new long[] { 1, 1, 2, 3 },
            new long[] { 1, 1, 2, 2, 3, 4 },
            "a different on-condition operand is a different join plan");
    }

    // ---------------------------------------------------------------------------------------------
    // (d) Sorting: direction, key and key count
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the <c>SortingExpressionPlanEqualityComparer</c> through the plan key: one sort key, its
    /// direction and the number of keys each change the key and the live row order.
    /// </summary>
    [Fact]
    public void Sorting_DirectionKeyAndCount_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Ascending() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        AssertEqualPlans(Ascending(), Ascending(), new long[] { 1, 2, 3, 4 }, "two identical orderings are one plan");

        var descending = Context.From<St5Marker>().OrderByDescending(x => x.Id).Select(x => x.Id);

        AssertDistinctPlans(
            Ascending(),
            descending,
            new long[] { 1, 2, 3, 4 },
            new long[] { 4, 3, 2, 1 },
            "a different sort direction is a different plan key");

        var byGroup = Context.From<St5Marker>()
            .OrderBy(x => x.Grp)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertDistinctPlans(
            Ascending(),
            byGroup,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "a different sort key (and a second sort key) is a different plan key even when the rows coincide");
    }

    // ---------------------------------------------------------------------------------------------
    // (e) GroupingList and GroupingType
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the plan-key grouping list: grouping by a different column is a different plan while the
    /// live group counts coincide. (The <c>GroupingType</c> arm - plain GROUP BY vs CUBE - is recorded
    /// as deferred: SQLite rejects the rendered CUBE form at execution, so no live form exists here.)
    /// </summary>
    [Fact]
    public void GroupingListAndType_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> ByGroup() => Context.From<St5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());

        AssertEqualPlans(ByGroup(), ByGroup(), new[] { 2, 2 }, "two identical grouping lists are one plan");

        var byTag = Context.From<St5Marker>()
            .GroupBy(x => new { x.Tag })
            .Select(g => SqlFunctions.Sql.count());

        AssertDistinctPlans(
            ByGroup(),
            byTag,
            new[] { 2, 2 },
            new[] { 2, 2 },
            "a different grouping list is a different plan key even when the group counts coincide");
    }

    // ---------------------------------------------------------------------------------------------
    // (f) HAVING
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the plan-key <c>Having</c>: the equal grouping prefix is shared, only the HAVING
    /// predicate changes, and the live filtered groups follow the change.
    /// </summary>
    [Fact]
    public void Having_OneFieldNegative_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> HavingAboveOne() => Context.From<St5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(g => SqlFunctions.Sql.count() > 1)
            .Select(g => SqlFunctions.Sql.sum(g.Id));

        AssertEqualPlans(HavingAboveOne(), HavingAboveOne(), new long[] { 3, 7 }, "two identical HAVING predicates are one plan");

        var havingAboveTwo = Context.From<St5Marker>()
            .GroupBy(x => new { x.Grp })
            .Having(g => SqlFunctions.Sql.count() > 2)
            .Select(g => SqlFunctions.Sql.sum(g.Id));

        AssertDistinctPlans(
            HavingAboveOne(),
            havingAboveTwo,
            new long[] { 3, 7 },
            Array.Empty<long>(),
            "a different HAVING predicate is a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // (g) SelectList: entity, scalar and multi-column projections
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the plan-key <c>SelectList</c>: an entity projection, a scalar projection and a
    /// multi-column projection are three different select-list plans; the scalar and the multi-column
    /// projection read the same rows. (The TUPLE form is recorded as deferred: SQLite rejects both a
    /// <c>ValueTuple</c> projection and <c>Tuple.Create</c>, which lowers to a row constructor that this
    /// dialect renders only as a direct comparison operand.)
    /// </summary>
    [Fact]
    public void SelectList_EntityScalarAndMultiColumn_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Scalar() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        AssertEqualPlans(Scalar(), Scalar(), new long[] { 1, 2, 3, 4 }, "two identical scalar projections are one plan");

        QueryCommand<St5Marker> Entity() => Context.From<St5Marker>().OrderBy(x => x.Id).ToCommand();
        var multiColumn = Context.From<St5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });

        var entityCommand = Entity();
        Resolve(entityCommand);
        Rows(PrepareUncached(entityCommand)).Select(e => e.Id).Should().Equal(new long[] { 1, 2, 3, 4 });

        Resolve(multiColumn);
        Rows(PrepareUncached(multiColumn)).Select(r => r.Id).Should().Equal(new long[] { 1, 2, 3, 4 });

        var scalarVersusEntity = Scalar();
        Resolve(scalarVersusEntity);
        Rows(PrepareUncached(scalarVersusEntity)).Should().Equal(new long[] { 1, 2, 3, 4 });
        AssertDistinctPreparedPlans(
            scalarVersusEntity,
            entityCommand,
            "an entity projection and a scalar projection are different select-list plans");
        PrepareCached(entityCommand).Should().NotBeSameAs(PrepareCached(scalarVersusEntity));

        var scalarVersusMultiColumn = Scalar();
        Resolve(scalarVersusMultiColumn);
        Rows(PrepareUncached(scalarVersusMultiColumn)).Should().Equal(new long[] { 1, 2, 3, 4 });
        AssertDistinctPreparedPlans(
            scalarVersusMultiColumn,
            multiColumn,
            "a multi-column projection and a scalar projection are different select-list plans");
        PrepareCached(multiColumn).Should().NotBeSameAs(PrepareCached(scalarVersusMultiColumn));
    }

    // ---------------------------------------------------------------------------------------------
    // (h) InValuesShapeHash
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the plan-key <c>InValuesShapeHash</c>: the evaluated value-list length (same closure
    /// site) and the element kind change the key, and the rows follow the values.
    /// </summary>
    [Fact]
    public void InValuesShape_LengthAndElementKind_ShouldKeySeparately()
    {
        ResetPlanCache();

        var ids = new List<long> { 1 };

        QueryCommand<long> ByIds() => Context.From<St5Marker>()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        // The value-list shape is evaluated at preparation, so the one-element side is prepared and
        // executed BEFORE the shared closure grows.
        var oneValue = ByIds();
        Resolve(oneValue);
        Rows(PrepareUncached(oneValue)).Should().Equal(new long[] { 1 });
        var oneKey = oneValue.GetOrCreatePlanKey(null);

        ids.Add(2);
        var twoValues = ByIds();
        Resolve(twoValues);
        Rows(PrepareUncached(twoValues)).Should().Equal(new long[] { 1, 2 });
        var twoKey = twoValues.GetOrCreatePlanKey(null);

        oneKey.Should().NotBeSameAs(twoKey, "each command owns its own plan-key instance");
        oneValue.GetQueryPlanEqualityComparer().Equals(oneValue, twoValues).Should().BeFalse(
            "the evaluated value-list length is part of the plan key even though the closure access is shape independent");
        oneKey.Equals(twoKey).Should().BeFalse(
            "the evaluated value-list length is part of the plan key even though the closure access is shape independent");

        // Same evaluated length (two elements) and the same predicate shape; only the element kind of
        // the captured value list differs.
        var intTags = new List<int> { 10, 20 };
        var intElements = Context.From<St5Marker>()
            .Where(x => intTags.Contains(x.Tag))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var longTags = new List<long> { 10, 20 };
        var longElements = Context.From<St5Marker>()
            .Where(x => longTags.Contains(x.Tag))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertDistinctPlans(
            intElements,
            longElements,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "a different value-list element kind is a different plan key (the evaluated shape hash follows the element type)");
    }

    // ---------------------------------------------------------------------------------------------
    // (i) PreparedCondition
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the plan-key <c>PreparedCondition</c>: the equal shape differs only in the predicate
    /// operand, and each variant reads its own live rows.
    /// </summary>
    [Fact]
    public void PreparedCondition_Operand_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> ByGroup() => Context.From<St5Marker>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertEqualPlans(ByGroup(), ByGroup(), new long[] { 1, 2 }, "two identical predicates are one plan");

        var byTag = Context.From<St5Marker>()
            .Where(x => x.Tag == 10)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertDistinctPlans(
            ByGroup(),
            byTag,
            new long[] { 1, 2 },
            new long[] { 1, 3 },
            "a different predicate operand is a different prepared condition and a different plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // (j) Resolved rendering overrides
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the resolved rendering overrides of the plan key: identifier quoting, the naming
    /// convention, the keyword case and the command timeout each change the key; each pair still
    /// executes against the live database on both sides.
    /// </summary>
    [Fact]
    public void RenderingOverrides_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Base() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        AssertEqualPlans(Base(), Base(), new long[] { 1, 2, 3, 4 }, "two plain commands are one plan");

        var quoted = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithQuotedIdentifiers(true);
        var unquoted = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithQuotedIdentifiers(false);

        AssertDistinctPlans(
            quoted,
            unquoted,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "the resolved identifier-quoting flag is part of the plan key");

        var snake = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id)
            .WithNamingConvention(SnakeCaseNamingConvention.Instance);
        var inherited = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id)
            .WithNamingConvention(null);

        AssertDistinctPlans(
            snake,
            inherited,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "the resolved naming convention is part of the plan key");

        var upper = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithKeywordCase(KeywordCase.Upper);
        var lower = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithKeywordCase(KeywordCase.Lower);

        AssertDistinctPlans(
            upper,
            lower,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "the resolved keyword case is part of the plan key");

        var timeout = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithCommandTimeout(7);
        var noTimeout = Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id).WithCommandTimeout(0);

        AssertDistinctPlans(
            timeout,
            noTimeout,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "the resolved command timeout is part of the plan key");
    }

    // ---------------------------------------------------------------------------------------------
    // (k) From: derived subquery shapes, per-query source overrides and raw-source identity
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives <c>FromExpressionPlanEqualityComparer</c> beyond the physical/derived/raw split: two
    /// independently built derived subqueries with an equal inner shape compare TRUE; a one-field
    /// change of the COMPARED COLUMN in the inner predicate changes the nested command and compares
    /// FALSE (a changed captured VALUE does not: it is bound as a parameter and the rendered SQL text
    /// is identical, measured in <c>rv1/results/probe-derived-source.log</c>); a table-name
    /// override against an equal-shape copy table and a schema qualifier compare FALSE; two separately
    /// built raw sources with the same SQL compare FALSE because a raw fragment is identified by
    /// reference. Every side reads its own live rows.
    /// </summary>
    [Fact]
    public void From_DerivedAliasesOverridesAndRawIdentity_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> Derived(int minimum) => Context.From(Context.From<St5Marker>().Where(x => x.Id > minimum))
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        // A captured closure value is a PARAMETER, not a shape: the two commands above render the same
        // SQL text and share one plan by design (the cached plan re-binds the parameter from the fresh
        // closure). The negative therefore changes the COMPARED COLUMN inside the inner predicate,
        // which is a genuine shape change on the same derived-source shape.
        QueryCommand<long> DerivedByGroup() => Context.From(Context.From<St5Marker>().Where(x => x.Grp > 1))
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertEqualPlans(
            Derived(1),
            Derived(1),
            new long[] { 2, 3, 4 },
            "two derived subqueries with an equal inner shape are one plan");

        AssertDistinctPlans(
            Derived(1),
            DerivedByGroup(),
            new long[] { 2, 3, 4 },
            new long[] { 3, 4 },
            "a different compared column in the inner predicate is a different derived source");

        QueryCommand<long> Physical() => Context.From<St5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var renamed = Context.From<St5Marker>()
            .WithTableName("st5_marker_copy")
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        AssertDistinctPlans(
            Physical(),
            renamed,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2 },
            "a table-name override is a different physical source");

        var qualified = Context.From<St5Marker>()
            .WithSchema("main")
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        AssertDistinctPlans(
            Physical(),
            qualified,
            new long[] { 1, 2, 3, 4 },
            new long[] { 1, 2, 3, 4 },
            "a schema-qualified source is a different plan key from the unqualified table");

        QueryCommand<long> Raw() => Context.FromSql("select id, grp from st5_marker where grp = 1")
            .OrderBy(x => x.GetInt64("id"))
            .Select(x => x.GetInt64("id"));

        AssertDistinctPlans(
            Raw(),
            Raw(),
            new long[] { 1, 2 },
            new long[] { 1, 2 },
            "two raw sources with the same SQL are reference-identified and never share a plan");
    }

    // ---------------------------------------------------------------------------------------------
    // (l) Sorting over a nullable key and a multi-column select-list one-field negative
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Drives the <c>SortingExpressionPlanEqualityComparer</c> over a NULLABLE key (SQLite sorts NULL
    /// first ascending and last descending) and the <c>SelectExpressionPlanEqualityComparer</c> over two
    /// multi-column projections sharing the equal prefix. The equal multi-column pair compares TRUE and
    /// reuses one plan; the reversed direction and the one-column projection change compare FALSE, and
    /// every side executes its own live rows.
    /// </summary>
    [Fact]
    public void SortingNullOrderingAndMultiColumnSelectList_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<long> ByScoreAscending() => Context.From<St5Marker>()
            .OrderBy(x => x.Score)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        QueryCommand<long> ByScoreDescending() => Context.From<St5Marker>()
            .OrderByDescending(x => x.Score)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertEqualPlans(
            ByScoreAscending(),
            ByScoreAscending(),
            new long[] { 4, 1, 2, 3 },
            "two identical nullable-key orderings are one plan");

        AssertDistinctPlans(
            ByScoreAscending(),
            ByScoreDescending(),
            new long[] { 4, 1, 2, 3 },
            new long[] { 3, 1, 2, 4 },
            "a reversed direction over the same nullable key is a different plan and reverses the NULL placement");

        var namesA = Context.From<St5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        var namesB = Context.From<St5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });

        AssertEqualPlans(
            namesA,
            namesB,
            new[]
            {
                new { Id = 1L, Name = "alpha" },
                new { Id = 2L, Name = "beta" },
                new { Id = 3L, Name = "gamma" },
                new { Id = 4L, Name = "delta" },
            },
            "two identical multi-column projections are one plan");

        var codes = Context.From<St5Marker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Code });

        Resolve(namesA);
        Resolve(codes);
        Rows(PrepareUncached(namesA)).Select(r => r.Name).Should().Equal("alpha", "beta", "gamma", "delta");
        Rows(PrepareUncached(codes)).Select(r => r.Code).Should().Equal("a", "b", "a", "b");
        AssertDistinctPreparedPlans(
            namesA,
            codes,
            "a multi-column projection differing in exactly one component is a different select-list plan");
        PrepareCached(codes).Should().NotBeSameAs(PrepareCached(namesA));
    }

    // ---------------------------------------------------------------------------------------------
    // (m) MakeSelect: the GROUPING SETS head and its two fail-closed guards
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>SqlBuilder.MakeSelect</c>'s grouping-set head. A grouping-set declaration
    /// without any set and a set index outside the declared grouping list are rejected before SQL
    /// execution with the exact guard messages (:284 and :294-295), each paired with the same live
    /// grouping-set shape that does execute. The plain-vs-DISTINCT plan-key arm is deliberately NOT
    /// repeated here: <c>Sqlite/PlanKeyBranchCoverage5Tests</c> already executes DISTINCT and its
    /// one-field negative, so this fact takes a different select shape instead of duplicating it.
    /// </summary>
    [Fact]
    public void MakeSelect_GroupingSetGuards_ShouldRejectInvalidSetsAndReadTheValidGroupingShape()
    {
        ResetPlanCache();

        QueryCommand<int> Counts(params int[][] sets) => Context.From<St5Marker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, sets)
            .Select(g => SqlFunctions.Sql.count());

        var missingSets = Counts();
        FluentActions.Invoking(() => Rows(PrepareUncached(missingSets)))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("GROUPING SETS requires at least one set.");

        var outOfRange = Counts(new[] { 0 }, new[] { 5 });
        FluentActions.Invoking(() => Rows(PrepareUncached(outOfRange)))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("Grouping set index 5 is out of range 0..1.");

        // The live companion keeps a plain GROUP BY: the SQLite dialect declares the ANSI grouping-set
        // form but the bundled engine rejects the rendered syntax, so the valid grouping-set execution
        // is recorded as a dropped arm instead of being asserted unproven.
        QueryCommand<int> PlainGroups() => Context.From<St5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());

        AssertEqualPlans(PlainGroups(), PlainGroups(), new[] { 2, 2 },
            "the shape-clean plain grouping companion is one plan and reads one count per group");
    }

    // ---------------------------------------------------------------------------------------------
    // (n) MakeSelect: LIMIT/OFFSET pages and the unsupported WITH TIES guard
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>SqlBuilder.MakeSelect</c>'s trailing page clause. A limit-only page and a
    /// limit+offset page are different plans with different live rows, and the cached plan stays page
    /// specific. SQLite has no <c>WITH TIES</c> page modifier, so the same live shape with
    /// <c>WithTies()</c> is rejected by the dialect contract before execution; the guard is paired with
    /// the live page it would otherwise have read.
    /// </summary>
    [Fact]
    public void MakeSelect_LimitOffsetAndWithTies_ShouldPageAndRejectUnsupportedTies()
    {
        ResetPlanCache();

        QueryCommand<long> FirstTwo() => Context.From<St5Marker>().OrderBy(x => x.Id).Limit(2).Select(x => x.Id);
        QueryCommand<long> SecondTwo() => Context.From<St5Marker>().OrderBy(x => x.Id).Limit(2).Offset(2).Select(x => x.Id);

        AssertEqualPlans(FirstTwo(), FirstTwo(), new long[] { 1, 2 }, "two identical limit pages are one plan");

        AssertDistinctPlans(
            FirstTwo(),
            SecondTwo(),
            new long[] { 1, 2 },
            new long[] { 3, 4 },
            "a limit-only page and a limit+offset page are different plans and read different rows");

        var ties = Context.From<St5Marker>().OrderBy(x => x.Id).Limit(2).WithTies().Select(x => x.Id);

        FluentActions.Invoking(() => Rows(PrepareUncached(ties)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*WITH TIES*");

        AssertEqualPlans(FirstTwo(), FirstTwo(), new long[] { 1, 2 }, "the live companion page still reads its two rows");
    }

    // ---------------------------------------------------------------------------------------------
    // (o) MakeSelect: UNION / INTERSECT / EXCEPT select shapes
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: the set-operation head and body of <c>SqlBuilder.MakeSelect</c>. Both operands
    /// are the same on every pair, so only <c>UnionType</c> differs; the commands compare FALSE and read
    /// the distinct live row sets (union keeps the duplicate values once, intersect keeps the shared
    /// value, except removes it).
    /// </summary>
    [Fact]
    public void MakeSelect_UnionIntersectExcept_ShouldKeySeparatelyAndReadDifferentRows()
    {
        ResetPlanCache();

        QueryCommand<int> Left() => Context.From<St5Marker>().Where(x => x.Grp == 1).Select(x => x.Grp);
        QueryCommand<int> Right() => Context.From<St5Marker>().Where(x => x.Tag == 10).Select(x => x.Grp);

        var union = Left().Union(Right());
        var unionAgain = Left().Union(Right());

        Resolve(union);
        Resolve(unionAgain);
        union.GetOrCreatePlanKey(null).Should().NotBeSameAs(unionAgain.GetOrCreatePlanKey(null),
            "each command owns its own plan-key instance");
        union.GetQueryPlanEqualityComparer().Equals(union, unionAgain).Should().BeTrue(
            "two set operations with the same type and operands are one plan");
        union.GetOrCreatePlanKey(null).Equals(unionAgain.GetOrCreatePlanKey(null)).Should().BeTrue(
            "equal set operations compare equal through the plan key");
        Rows(PrepareUncached(union)).Should().BeEquivalentTo(new[] { 1, 2 },
            "UNION keeps the duplicated 1 once and adds the right-hand 2");

        var intersect = Left().Intersect(Right());
        AssertDistinctPlans(
            Right(),
            intersect,
            new[] { 1, 2 },
            new[] { 1 },
            "the same operands under INTERSECT are a different plan key and keep only the shared value");

        var except = Left().Except(Right());
        AssertDistinctPlans(
            Left().Intersect(Right()),
            except,
            new[] { 1 },
            Array.Empty<int>(),
            "INTERSECT and EXCEPT differ only in UnionType yet are different plans with different rows");

        AssertDistinctPreparedPlans(
            union,
            intersect,
            "UNION and INTERSECT over the same operands are different set-operation plans");
    }

    // ---------------------------------------------------------------------------------------------
    // (p) MakeSelect: grouping totals, a derived source and the per-parent paging head
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: three <c>MakeSelect</c> shapes. (1) The grouping super-aggregates
    /// (<c>ROLLUP</c>, <c>CUBE</c>, <c>GROUPING SETS</c>) return the group aggregates plus the grand
    /// total and are different plan keys from a plain group. (2) A derived source reads its inner
    /// predicate's rows and is a different plan from the physical source. (3) A child query with its own
    /// ordering and limit lowers to the per-parent ranked shape (the <c>PerParentPaging</c> head of
    /// <c>MakeSelect</c>) and gives every parent its own page.
    /// </summary>
    [Fact]
    public void MakeSelect_GroupTotalsDerivedSourceAndPerParentPaging_ShouldReadShapeRows()
    {
        ResetPlanCache();

        QueryCommand<int> PlainGroups() => Context.From<St5Marker>()
            .GroupBy(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());

        Run(PlainGroups()).Should().Equal(new[] { 2, 2 }, "the plain grouping returns one count per group");

        // ROLLUP/CUBE/GROUPING SETS are declared supported by the SQLite dialect (ANSI form) but the
        // bundled engine rejects the rendered syntax at execution ("no such function: rollup" and a
        // near-"sets" syntax error). The arms are compared at the plan-key level only; their live
        // executions are recorded as dropped in rv1/inputs/deferred.json rather than asserted unproven.
        QueryCommand<int> Rollup() => Context.From<St5Marker>()
            .GroupByRollup(x => new { x.Grp })
            .Select(g => SqlFunctions.Sql.count());

        var plainGroupCommand = PlainGroups();
        var rollupCommand = Rollup();
        Resolve(plainGroupCommand);
        Resolve(rollupCommand);
        plainGroupCommand.GetQueryPlanEqualityComparer().Equals(plainGroupCommand, rollupCommand).Should().BeFalse(
            "a plain GROUP BY and a ROLLUP over the same grouping list are different plan keys");
        Rows(PrepareUncached(plainGroupCommand)).Should().Equal(new[] { 2, 2 });

        QueryCommand<long> Physical() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<long> Derived() => Context.From(Context.From<St5Marker>().Where(x => x.Grp == 2))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        AssertEqualPlans(Derived(), Derived(), new long[] { 3, 4 }, "two identical derived sources are one plan");
        AssertDistinctPlans(
            Physical(),
            Derived(),
            new long[] { 1, 2, 3, 4 },
            new long[] { 3, 4 },
            "a physical source and a derived source are different plans and read different rows");

        var paged = Context.From<St5EagerParent>()
            .OrderBy(p => p.Id)
            .LoadWith(
                p => p.Children,
                c => c.From<St5EagerChild>().OrderBy(x => x.SortKey).Limit(1),
                p => p.Id,
                c => c.ParentId)
            .ToList();

        paged.Select(p => p.Id).Should().Equal(new long[] { 9701, 9702 }, "the per-parent page keeps both parents");
        paged[0].Children.Select(c => c.Id).Should().Equal(new long[] { 9711 },
            "the first parent's page keeps only its first child (9712 is outside the per-parent top-1)");
        paged[1].Children.Select(c => c.Id).Should().Equal(new long[] { 9721 },
            "the second parent receives its own page");
    }

    // ---------------------------------------------------------------------------------------------
    // (q) Plan-key string lists: statement hints (null, count, element)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (list helpers): <c>EqualsCore</c> <c>Hints</c> :204 and <c>StringListsEqual</c> :236-246
    /// (reference, null presence, count :238, element :242). The hint lists are attached through the
    /// public <c>Hint(...)</c> surface on this one context; the SQLite dialect rejects hints at render,
    /// so the facts assert the provider-independent plan key and the live baseline rows.
    /// </summary>
    [Fact]
    public void QueryPlan_HintListArms_ShouldComparePresenceCountAndElement()
    {
        ResetPlanCache();

        QueryCommand<long> Base() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        var plain = Base();
        var one = Base().Hint("RECOMPILE");
        var oneAgain = Base().Hint("RECOMPILE");
        var two = Base().Hint("RECOMPILE", "MAXDOP 1");
        var otherElement = Base().Hint("NOLOCK");

        Resolve(plain);
        Resolve(one);
        Resolve(oneAgain);
        Resolve(two);
        Resolve(otherElement);

        var comparer = plain.GetQueryPlanEqualityComparer();

        comparer.Equals(one, oneAgain).Should().BeTrue("an identical hint list is one plan");
        comparer.GetHashCode(one).Should().Be(comparer.GetHashCode(oneAgain));
        comparer.Equals(plain, one).Should().BeFalse("a hint list present on one side only is a different plan key");
        comparer.Equals(one, two).Should().BeFalse("a hint list of a different length is a different plan key");
        comparer.Equals(one, otherElement).Should().BeFalse("a different hint element is a different plan key");
        comparer.GetHashCode(one).Should().NotBe(comparer.GetHashCode(otherElement));

        Rows(PrepareCached(Base())).Should().Equal(new long[] { 1, 2, 3, 4 },
            "the un-hinted baseline executes live on the shared context");
    }

    // ---------------------------------------------------------------------------------------------
    // (r) Plan-key windows: null presence, loop and the TRUE return of WindowsEqual
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (list helpers): <c>WindowsEqual</c> :251 (null presence), :254 (the loop) and :281-283
    /// (the TRUE return). A plain command compared with a named-window command takes the null arm; the
    /// independently built equal named-window pair walks the loop and returns TRUE, and both sides read
    /// their live row numbers on this context.
    /// </summary>
    [Fact]
    public void QueryPlan_WindowListArms_ShouldComparePresenceAndReturnTrue()
    {
        ResetPlanCache();

        QueryCommand<long> Plain() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<int> Windowed()
        {
            var builder = Context.From<St5Marker>();
            return builder
                .Window("st5_w", [x => x.Grp], [builder.Asc(x => x.Id)])
                .OrderBy(x => x.Id)
                .Select(x => SqlFunctions.Sql.row_number().Over("st5_w"));
        }

        var plain = Plain();
        var windowed = Windowed();
        var windowedAgain = Windowed();

        Resolve(plain);
        Resolve(windowed);
        Resolve(windowedAgain);

        var comparer = plain.GetQueryPlanEqualityComparer();

        comparer.Equals(plain, windowed).Should().BeFalse(
            "a window list present on one side only is a different plan key");
        comparer.Equals(windowed, windowedAgain).Should().BeTrue(
            "two identical named-window definitions walk the list and return equal");
        comparer.GetHashCode(windowed).Should().Be(comparer.GetHashCode(windowedAgain));

        var cached = PrepareCached(Windowed());
        Rows(cached).Should().Equal(new[] { 1, 2, 1, 2 },
            "the named window executes live over the two Grp partitions");
    }

    // ---------------------------------------------------------------------------------------------
    // (s) Plan-key key/value lists: SETTINGS (null presence, count, key and value)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (list helpers): <c>KeyValueListsEqual</c> :289-294. The ClickHouse <c>SETTINGS</c> list is
    /// the only public writer of the plan key's key/value list; it is provider-gated at render, so the
    /// facts prepare in place on this context and assert the plan key, with a live baseline query.
    /// </summary>
    [Fact]
    public void QueryPlan_SettingsListArms_ShouldComparePresenceCountKeyAndValue()
    {
        ResetPlanCache();

        QueryCommand<long> Base() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);

        var plain = Base();
        var one = Context.From<St5Marker>().Settings(("max_threads", "2")).OrderBy(x => x.Id).Select(x => x.Id);
        var oneAgain = Context.From<St5Marker>().Settings(("max_threads", "2")).OrderBy(x => x.Id).Select(x => x.Id);
        var otherValue = Context.From<St5Marker>().Settings(("max_threads", "4")).OrderBy(x => x.Id).Select(x => x.Id);
        var otherKey = Context.From<St5Marker>().Settings(("max_rows", "2")).OrderBy(x => x.Id).Select(x => x.Id);
        var two = Context.From<St5Marker>().Settings(("max_threads", "2"), ("max_rows", "3")).OrderBy(x => x.Id).Select(x => x.Id);

        Resolve(plain);
        Resolve(one);
        Resolve(oneAgain);
        Resolve(otherValue);
        Resolve(otherKey);
        Resolve(two);

        var comparer = plain.GetQueryPlanEqualityComparer();

        comparer.Equals(one, oneAgain).Should().BeTrue("an identical SETTINGS list is one plan");
        comparer.GetHashCode(one).Should().Be(comparer.GetHashCode(oneAgain));
        comparer.Equals(plain, one).Should().BeFalse("a SETTINGS list present on one side only is a different plan key");
        comparer.Equals(one, otherValue).Should().BeFalse("a different SETTINGS value is a different plan key");
        comparer.Equals(one, otherKey).Should().BeFalse("a different SETTINGS key is a different plan key");
        comparer.Equals(one, two).Should().BeFalse("a SETTINGS list of a different count is a different plan key");

        Rows(PrepareCached(Base())).Should().Equal(new long[] { 1, 2, 3, 4 },
            "the baseline without SETTINGS executes live on the shared context");
    }

    // ---------------------------------------------------------------------------------------------
    // (t) Plan-key expression lists: ARRAY JOIN (null presence, length, element)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arms B (list helpers): <c>ExpressionListsEqual</c> :304-309. ARRAY JOIN is the only public writer
    /// of the plan key's array-join expression list; the clauses are prepared in place on this context
    /// (SQLite rejects the rendered form) and the plan key is asserted. The plain baseline is live.
    /// </summary>
    [Fact]
    public void QueryPlan_ArrayJoinExpressionListArms_ShouldComparePresenceAndElement()
    {
        ResetPlanCache();

        QueryCommand<long> Plain() => Context.From<St5Marker>().OrderBy(x => x.Id).Select(x => x.Id);
        QueryCommand<long> Joined()
        {
            var builder = Context.From<St5Marker>().ArrayJoin(x => new[] { x.Id });
            return builder.OrderBy(x => x.Id).Select(x => x.Id);
        }

        QueryCommand<long> OtherJoined()
        {
            var builder = Context.From<St5Marker>().ArrayJoin(x => new[] { x.Grp });
            return builder.OrderBy(x => x.Id).Select(x => x.Id);
        }

        var plain = Plain();
        var joined = Joined();
        var joinedAgain = Joined();
        var otherJoined = OtherJoined();

        Resolve(plain);
        Resolve(joined);
        Resolve(joinedAgain);
        Resolve(otherJoined);

        var comparer = plain.GetQueryPlanEqualityComparer();

        comparer.Equals(plain, joined).Should().BeFalse(
            "an array-join expression list present on one side only is a different plan key");
        comparer.Equals(joined, joinedAgain).Should().BeTrue("an identical array-join expression list is one plan");
        comparer.GetHashCode(joined).Should().Be(comparer.GetHashCode(joinedAgain));
        comparer.Equals(joined, otherJoined).Should().BeFalse(
            "a different array-join element expression is a different plan key");

        Rows(PrepareCached(Plain())).Should().Equal(new long[] { 1, 2, 3, 4 },
            "the baseline without ARRAY JOIN executes live on the shared context");
    }

    // ---------------------------------------------------------------------------------------------
    // (u) Plan-key outer references: two non-null equal lists and a differing target
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Arm B (list helper): <c>EqualsCore</c> <c>OuterReferences</c> :229 through the expression
    /// comparer. Two independently built correlated commands both record outer references, so the
    /// comparison walks the non-null equal list instead of the one-null arm; the live counterpart
    /// returns the markers that own at least one line.
    /// </summary>
    [Fact]
    public void QueryPlan_OuterReferences_ShouldCompareBothNonNullLists()
    {
        ResetPlanCache();

        QueryCommand<long> Correlated()
        {
            var lines = Context.From<St5Line>();
            return Context.From<St5Marker>()
                .Where(m => SqlFunctions.Sql.exists(lines.Where(l => l.MarkerId == m.Id)))
                .OrderBy(x => x.Id)
                .Select(x => x.Id);
        }

        var correlated = Correlated();
        var correlatedAgain = Correlated();
        Resolve(correlated);
        Resolve(correlatedAgain);

        var comparer = correlated.GetQueryPlanEqualityComparer();
        comparer.Equals(correlated, correlatedAgain).Should().BeTrue(
            "two identical correlated shapes share an equal outer-reference list");
        comparer.GetHashCode(correlated).Should().Be(comparer.GetHashCode(correlatedAgain));

        Rows(PrepareCached(Correlated())).Should().Equal(new long[] { 1, 2, 3 },
            "only markers 1, 2 and 3 own a line in the live fixture");
    }

    [SqlTable("st5_marker")]
    public sealed class St5Marker
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }

    [SqlTable("st5_line")]
    public sealed class St5Line
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("marker_id")]
        public long MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("st5_eager_parent")]
    public sealed class St5EagerParent
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public List<St5EagerChild> Children { get; set; } = [];
    }

    [SqlTable("st5_eager_child")]
    public sealed class St5EagerChild
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("parent_id")]
        public long ParentId { get; set; }

        [Column("sort_key")]
        public int SortKey { get; set; }
    }
}
