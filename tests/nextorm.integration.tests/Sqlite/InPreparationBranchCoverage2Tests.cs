using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Value-list (<c>IN</c>/<c>Contains</c>) preparation coverage executed against a live SQLite database.
/// Every fact builds a real query over the inherited <see cref="ProviderTestSuite"/> repository and
/// asserts the returned rows, so a wrong plan-key shape would surface as a wrong row set (or a stale
/// parameter count) rather than as a silent cache hit. A reject/guard fact always runs a live
/// counterpart of the same shape first.
///
/// <para>
/// Targeted surface: <c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c> —
/// <c>RefreshInValuesShape</c> 195-281 (the <c>!ShapeScanned</c> first scan 200-246, the
/// <c>ShapeValuesStable</c> early return 250, and the per-execution rebuild 254-280 with
/// <c>HasTopLevelInValues</c> 257-265); <c>src/nextorm.core/Query/InValues.cs</c> 64
/// (<c>ComputeShapeHash</c>/<c>Partition</c>/<c>IsStableValueExpression</c> and the tuple-family
/// rejection arms); <c>src/nextorm.core/Visitors/InValuesTranslator.cs</c> 12 (the captured-partition
/// reuse at 52-63, the empty-list fold 87-92, the nullable-aware <c>or ... is null</c> arm 97-113 and
/// the tuple entry point 39-42).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/TupleInValuesSweepTests.cs</c> owns
/// the *tuple* value list placed in the projection, HAVING, join condition, CTE and set operation;
/// the shared <c>CommonTestSuite.In</c> owns the three-value <c>SqlFunctions.Sql.@in</c> literal list,
/// the zero-value and one-value literal list, the captured two-value list/array, the nullable
/// <c>@in</c> list containing null, the string list with null, and the grown/reassigned captured
/// collection between two executions; <c>SqlitePlanCacheShapeCoverageTests.cs</c> owns the plan-key
/// separation on an <c>@in</c> cardinality change and the same-shape reuse, and
/// <c>Sqlite/PlanEqualityBranchCoverage2Tests.cs</c> (D2) owns the expression/plan comparer arms. The
/// shapes here are disjoint: the instance <c>Contains</c> on a captured nullable list, the null flip
/// inside a captured nullable list while the plan stays cached, the captured-variable-versus-inline-
/// literal plan separation, the <c>storeInCache:false</c> first preparation followed by the cached
/// one, the scalar <c>Contains</c> in a join condition and the nullable-tuple-element rejection.
/// </para>
///
/// <para>
/// Unreachable on SQLite (recorded, never fabricated): every <c>PreparedPreWhere</c> arm
/// (227-240 and 267-278) needs a prepared PreWhere, and <c>SqliteDialect</c> leaves
/// <c>SupportsPreWhere</c> at its <c>false</c> default while no public builder setter exists (the plan
/// records this as deferred with a trigger), so those branches are dead here; the
/// <c>EnsureProviderSupportsTupleInValues</c> preflight (209-210) is the SQL Server tuple gate and is
/// unreachable on a dialect that has a row-value renderer (<c>SqliteDialect.Tuple</c> is non-null).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class InPreparationBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    /// <summary>Executes an already prepared command against the live database.</summary>
    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Drops every cached plan on this thread so the fact observes its own store entries only.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    // ---------------------------------------------------------------------------------------------
    // 1-3. Cardinality: one value, many values, an empty collection
    // ---------------------------------------------------------------------------------------------

    /// <summary>A captured single-element array renders one parameter and filters the live rows.</summary>
    [Fact]
    public void Contains_CapturedArraySingleValue_ShouldFilterLiveRows()
    {
        var values = new long[] { 2 };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }

    /// <summary>A captured many-element list renders one parameter per value in source order.</summary>
    [Fact]
    public void Contains_CapturedListManyValues_ShouldFilterLiveRows()
    {
        var values = new List<long> { 1, 2, 3, 10, 42 };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L, 2L, 3L);
    }

    /// <summary>
    /// An empty captured collection folds to an always-false predicate (<c>InValuesTranslator</c>
    /// 87-92) and returns no row; the non-empty counterpart of the same shape still runs.
    /// </summary>
    [Fact]
    public void Contains_CapturedEmptyCollection_ShouldReturnEmptyWhileItsNonEmptyCounterpartRuns()
    {
        var empty = Array.Empty<long>();

        var ids = _sut.ComplexEntity
            .Where(e => empty.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();

        var live = new long[] { 3 };
        _sut.ComplexEntity
            .Where(e => live.Contains(e.Id))
            .Select(e => e.Id)
            .ToList()
            .Should().Equal(3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 4-5. Null layout: a null cell against a non-nullable and against a nullable column
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A null inside the value list keeps the <c>or &lt;column&gt; is null</c> arm only for a nullable
    /// element type; a non-nullable column cannot match the null cell, so only the non-null value
    /// survives (<c>InValuesTranslator</c> 97-113).
    /// </summary>
    [Fact]
    public void Contains_CapturedNullOnNonNullableColumn_ShouldNotMatchNullRows()
    {
        var values = new long?[] { 1, null };

        var ids = _sut.ComplexEntity
            .Where(e => SqlFunctions.Sql.@in(e.Id, values))
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L);
    }

    /// <summary>
    /// The same null cell against a nullable column takes the instance-<c>Contains</c> branch and matches
    /// the null row as well as the non-null value (<c>row 1</c> has a null <c>nullableint</c>).
    /// </summary>
    [Fact]
    public void Contains_CapturedNullOnNullableColumn_ShouldMatchNullRows()
    {
        var values = new List<int?> { 1, null };

        var ids = _sut.ComplexEntity
            .Where(e => values.Contains(e.Int))
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L, 2L, 3L);
    }

    // ---------------------------------------------------------------------------------------------
    // 6-7. Unkeyed clauses: a projected membership test and a join condition
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A membership test in the projection is an unkeyed clause: the partition is evaluated per build
    /// and the rows carry the per-row hit flag.
    /// </summary>
    [Fact]
    public void Contains_InProjectedColumn_ShouldEvaluatePerRow()
    {
        var values = new long[] { 1, 3 };

        var rows = _sut.ComplexEntity
            .OrderBy(e => e.Id)
            .Select(e => new { e.Id, Hit = values.Contains(e.Id) })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1L, 2L, 3L);
        rows.Select(r => r.Hit).Should().Equal(true, false, true);
    }

    /// <summary>A membership test inside a join condition is likewise unkeyed and filters the join.</summary>
    [Fact]
    public void Contains_InJoinedPredicate_ShouldFilterTheJoin()
    {
        var values = new long[] { 2, 3 };

        var rows = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id && values.Contains(c.Id))
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { Id = p.Item1.Id, Text = p.Item2.RequiredString })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(2, 3);
        rows.Select(r => r.Text).Should().Equal("asdfgoi", "34mfs");
    }

    // ---------------------------------------------------------------------------------------------
    // 8-10. Plan-key shape: captured versus literal, growth while cached, and a stable inline list
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A captured collection contributes only its shape to the plan key, while an inline literal list is
    /// part of the expression shape: the two do not share a plan, and both execute correctly.
    /// </summary>
    [Fact]
    public void Contains_CapturedVariableVersusLiteral_ShouldSeparateThePlanShape()
    {
        ResetPlanCache();

        var values = new long[] { 1, 3 };

        var captured = Prepare(_sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .OrderBy(x => x.Id)
            .Select(e => e.Id));
        Rows(captured).Should().Equal(1L, 3L);

        var literal = Prepare(_sut.ComplexEntity
            .Where(e => new long[] { 1, 3 }.Contains(e.Id))
            .OrderBy(x => x.Id)
            .Select(e => e.Id));
        Rows(literal).Should().Equal(1L, 3L);

        literal.Should().NotBeSameAs(captured,
            "an inline literal list is part of the expression shape while a captured list only contributes its shape");
    }

    /// <summary>
    /// A captured nullable list that gains a null between two executions of the cached plan changes the
    /// value-list shape (cardinality *and* null layout), so the refresh path re-evaluates the partition
    /// and the second execution adds the <c>or ... is null</c> arm. (The cardinality-only growth behind
    /// a cached plan is already owned by the shared <c>CommonTestSuite.In</c> fact; the null flip is the
    /// shape change that is not.)
    /// </summary>
    [Fact]
    public void Contains_CapturedListNullFlipWhileCached_ShouldReprepareAndReturnNewRows()
    {
        var values = new List<int?> { 1 };
        var query = _sut.ComplexEntity
            .Where(e => values.Contains(e.Int))
            .OrderBy(x => x.Id)
            .Select(e => e.Id);

        query.ToList().Should().Equal(2L, 3L);

        values.Add(null);

        query.ToList().Should().Equal(1L, 2L, 3L);
    }

    /// <summary>
    /// An inline literal list is shape-stable, so the first scan's hash and partition stay valid and the
    /// identical shape reuses the same plan (the <c>ShapeValuesStable</c> early return).
    /// </summary>
    [Fact]
    public void Contains_StableInlineList_ShouldReuseThePlanAndTheValues()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity
            .Where(e => new long[] { 1, 3 }.Contains(e.Id))
            .OrderBy(x => x.Id)
            .Select(e => e.Id));
        Rows(first).Should().Equal(1L, 3L);

        var again = Prepare(_sut.ComplexEntity
            .Where(e => new long[] { 1, 3 }.Contains(e.Id))
            .OrderBy(x => x.Id)
            .Select(e => e.Id));
        Rows(again).Should().Equal(1L, 3L);

        again.Should().BeSameAs(first, "a stable inline list keeps the plan key and skips the per-execution rebuild");
    }

    // ---------------------------------------------------------------------------------------------
    // 11-12. A command first prepared without hashing, and the tuple-family rejection
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A command first prepared without hashing (<c>storeInCache:false</c>) and then reused through the
    /// cache folds its value-list shape in on the first cached use (the <c>!ShapeScanned</c> arm) and
    /// still returns the live rows.
    /// </summary>
    [Fact]
    public void Contains_PreparedWithoutHashingThenCached_ShouldFoldTheShape()
    {
        ResetPlanCache();

        var values = new long[] { 2, 3 };
        var command = _sut.ComplexEntity
            .Where(e => values.Contains(e.Id))
            .OrderBy(x => x.Id)
            .Select(e => e.Id);

        var uncached = ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None);
        Rows(uncached).Should().Equal(2L, 3L);

        var cached = Prepare(command);
        Rows(cached).Should().Equal(2L, 3L);
    }

    /// <summary>
    /// A nullable tuple element has no null-safe row shape and is rejected with the pinned message
    /// (<c>InValuesTranslator</c> 39-42, <c>InValues.PartitionTuple</c>), while the same flat
    /// non-nullable tuple list runs.
    /// </summary>
    [Fact]
    public void Contains_NullableTupleElement_ShouldBeRejectedWhileFlatTupleRuns()
    {
        var liveTuples = new List<(long, int?)> { (2, 1) };

        var live = _sut.ComplexEntity
            .Where(e => liveTuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        live.Should().Equal(2L);

        var nullableTuples = new List<(long, int?)?> { (2, 1) };

        var act = () => _sut.ComplexEntity
            .Where(e => nullableTuples.Contains(new ValueTuple<long, int?>(e.Id, e.Int)))
            .Select(e => e.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*nullable tuple elements*");
    }
}
