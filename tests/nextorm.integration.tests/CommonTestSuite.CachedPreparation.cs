using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T201 (#201) D201-4 — shared integration coverage for the D201-3 cached-path preparation recipe
/// (<c>QueryPlanner</c> raw candidate index, <c>QueryCommand.RawShape</c>, transactional
/// <c>ApplyPreparationFrom</c>). A fact added to this partial runs once per concrete provider subclass
/// (<c>SqliteIntegrationTests</c>, <c>PostgresIntegrationTests</c>, <c>SqlServerIntegrationTests</c>,
/// <c>MySqlIntegrationTests</c>, <c>MariaDbIntegrationTests</c>, <c>ClickHouseIntegrationTests</c>).
///
/// <para>
/// Every case is a <b>differential against ordinary preparation</b> (the design's oracle) plus a
/// deterministic look at the raw candidate index itself. The process-wide
/// <c>QueryPlanner.Recipe*</c> counters are shared, and integration collections of different providers
/// may run concurrently, so they are only ever asserted as a <i>monotonic lower bound</i>; the primary
/// per-shape evidence is <c>QueryPlanStore.TryGetRaw</c>, which is exactly what the planner's own probe
/// evaluates and is confined to the current thread-local store.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    // -------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Renders the SQL a fresh command is prepared with, without executing it and without opening a
    /// connection, so it is safe on a placeholder-free integration provider.
    /// </summary>
    private static string CachedPreparationSql<TResult>(IDataContext ctx, QueryCommand<TResult> command, bool storeInCache)
    {
        var prepared = (DbPreparedQueryCommand<TResult>)((DataContext)ctx)
            .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: storeInCache, CancellationToken.None);
        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    /// <summary>
    /// Deterministic evidence that the recipe can serve <paramref name="command"/>'s shape: a raw
    /// pre-preparation candidate is published for its descriptor fingerprint and its historical
    /// normalized key is still owned by the same holder that was published (see
    /// <c>QueryPlanStore.TryGetRaw</c>, invoked by the planner at <c>QueryPlanner.cs:623</c>). The
    /// thread-local raw index is untouched by other providers' collections, so — unlike the counters —
    /// this cannot be satisfied by a concurrent test.
    /// </summary>
    private static bool CachedPreparationRawCandidateIsLive(IDataContext ctx, QueryCommand command)
    {
        var shape = QueryCommand.CaptureRawShape(command);
        return shape is not null && QueryPlanStore.TryGetRaw(ctx.GetType(), shape, command, out _);
    }

    /// <summary>The seeded <c>simple_entity</c> ids (1..10) that are greater than <paramref name="threshold"/>.</summary>
    private static int[] CachedPreparationIdsAbove(int threshold)
        => Enumerable.Range(1, 10).Where(id => id > threshold).ToArray();

    // -------------------------------------------------------------------------------------------
    // 1. Recipe reuse vs ordinary preparation: SQL, state and real results
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void CachedPreparation_ReuseMatchesOrdinaryPreparation()
    {
        var ctx = _sut.DataProvider;

        var publishes = QueryPlanner.RecipePublishes;
        var first = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id), storeInCache: true);
        (QueryPlanner.RecipePublishes - publishes).Should().BeGreaterThan(0,
            "the eligible fresh command publishes a raw candidate (or reuses one published by this thread)");

        // The raw candidate is live and reachable exactly as the planner's own probe requires.
        CachedPreparationRawCandidateIsLive(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id))
            .Should().BeTrue("the recipe published a live raw candidate for the eligible shape");

        var reuses = QueryPlanner.RecipeReuses;
        var second = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id), storeInCache: true);
        (QueryPlanner.RecipeReuses - reuses).Should().BeGreaterThan(0, "the equivalent fresh command reuses the published preparation");
        second.Should().Be(first, "the reused preparation renders byte-identical SQL");

        // Ordinary preparation of the same shape is the differential oracle.
        var ordinary = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id), storeInCache: false);
        ordinary.Should().Be(first);

        // ...and the real database read is unchanged.
        ctx.From<ISimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id).ToList()
            .Should().Equal(CachedPreparationIdsAbove(5));
    }

    [Fact]
    public void CachedPreparation_DifferentRawGraphIsNotReused()
    {
        var ctx = _sut.DataProvider;

        var first = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 11).Select(x => x.Id), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 11).Select(x => x.Id))
            .Should().BeTrue();

        // Same projection, different raw predicate: the descriptor must not alias the published candidate.
        CachedPreparationRawCandidateIsLive(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 12).Select(x => x.Id))
            .Should().BeFalse("a different raw graph is a different descriptor and never matches the stored candidate");

        var other = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 12).Select(x => x.Id), storeInCache: true);
        other.Should().NotBe(first);
    }

    /// <summary>
    /// The strongest differential available without the internal counters: the recipe-served read, a
    /// second recipe-served read of the same shape, and an explicit <c>storeInCache:false</c> ordinary
    /// preparation must all return exactly the same rows from the real database.
    /// </summary>
    [Fact]
    public void CachedPreparation_ResultsMatchBetweenCachedAndOrdinaryPaths()
    {
        var ctx = _sut.DataProvider;
        QueryCommand<int> Build() => ctx.From<ISimpleEntity>().Where(x => x.Id > 6).Select(x => x.Id);

        var cachedFirst = ctx.ToList(((DataContext)ctx).GetPreparedQueryCommand(Build(), false, true, CancellationToken.None));
        var cachedSecond = ctx.ToList(((DataContext)ctx).GetPreparedQueryCommand(Build(), false, true, CancellationToken.None));
        var ordinary = ctx.ToList(((DataContext)ctx).GetPreparedQueryCommand(Build(), false, false, CancellationToken.None));

        cachedFirst.Should().Equal(CachedPreparationIdsAbove(6));
        cachedSecond.Should().Equal(cachedFirst);
        ordinary.Should().Equal(cachedFirst);
    }

    // -------------------------------------------------------------------------------------------
    // 2. Streaming / non-streaming, cancellation, partial enumeration and disposal
    // -------------------------------------------------------------------------------------------

    [Fact]
    public async Task CachedPreparation_StreamingAndNonStreamingResultsMatch()
    {
        var ctx = _sut.DataProvider;

        var buffered = ctx.From<ISimpleEntity>().Where(x => x.Id > 7).Select(x => x.Id).ToList();

        var streamed = new List<int>();
        await foreach (var id in ctx.From<ISimpleEntity>().Where(x => x.Id > 7).Select(x => x.Id)
                           .ToAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            streamed.Add(id);
        }

        buffered.Should().Equal(CachedPreparationIdsAbove(7));
        streamed.Should().Equal(buffered, "the enumerator terminal observes the same rows as the buffered terminal");

        // The enumering terminal may prepare the same shape; the recipe is still reachable and equal.
        CachedPreparationRawCandidateIsLive(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 7).Select(x => x.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task CachedPreparation_CancellationBeforeExecutionKeepsThePathUsable()
    {
        var ctx = _sut.DataProvider;

        var act = () => ctx.From<ISimpleEntity>().Where(x => x.Id > 8).Select(x => x.Id)
            .ToListAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();

        // The cancelled attempt must not have poisoned the path: the same shape still reads correctly.
        var rows = await ctx.From<ISimpleEntity>().Where(x => x.Id > 8).Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().Equal(CachedPreparationIdsAbove(8));
    }

    [Fact]
    public async Task CachedPreparation_PartialEnumerationThenFullReadKeepsThePathUsable()
    {
        var ctx = _sut.DataProvider;

        var rows = 0;
        await foreach (var _ in ctx.From<ISimpleEntity>().Where(x => x.Id > 9).Select(x => x.Id)
                           .ToAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            rows++;
            break; // dispose mid-enumeration
        }

        rows.Should().Be(1);

        ctx.From<ISimpleEntity>().Where(x => x.Id > 9).Select(x => x.Id).ToList()
            .Should().Equal(CachedPreparationIdsAbove(9), "a partially enumerated and disposed read leaves the path usable");
    }

    // -------------------------------------------------------------------------------------------
    // 3. Shared terminal route: the actual shared AnyCommand instance and later ordinary cache hits
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void CachedPreparation_LaterCacheHitAfterSharedTerminalRoute()
    {
        var ctx = _sut.DataProvider;

        // The shared terminal route reuses ONE command instance per context (QueryCache.AnyCommand).
        _sut.SimpleEntity.Where(x => x.Id > 900_000).Any().Should().BeFalse();
        _sut.SimpleEntity.Where(x => x.Id > 5).Any().Should().BeTrue();

        // A later cacheable query must keep its cache behaviour (no sticky Cache/_dontCache mutation).
        var first = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 10).Select(x => x.Id), storeInCache: true);

        var reuses = QueryPlanner.RecipeReuses;
        var second = CachedPreparationSql(ctx, ctx.From<ISimpleEntity>().Where(x => x.Id > 10).Select(x => x.Id), storeInCache: true);
        second.Should().Be(first, "a later cacheable query still hits its plan");
        (QueryPlanner.RecipeReuses - reuses).Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------------------------
    // 4. Whole-graph differentials at database level: CTE / recursive CTE / set operations / Distinct
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void CachedPreparation_CteWholeGraphMatchesOrdinaryPreparation()
    {
        var ctx = _sut.DataProvider;

        QueryCommand<int> Build()
        {
            var body = _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            return ctx.With("cached_prep_recent", body)
                .From("cached_prep_recent")
                .Join(_sut.SimpleEntity, (c, s) => c["id"].AsInt == s.Id)
                .Where(p => p.Item2.Id > 0)
                .Select(p => p.Item1["id"].AsInt);
        }

        var first = CachedPreparationSql(ctx, Build(), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, Build()).Should().BeTrue("the whole CTE graph is an eligible raw shape");

        var reuses = QueryPlanner.RecipeReuses;
        CachedPreparationSql(ctx, Build(), storeInCache: true).Should().Be(first);
        (QueryPlanner.RecipeReuses - reuses).Should().BeGreaterThan(0);
        CachedPreparationSql(ctx, Build(), storeInCache: false).Should().Be(first);

        Build().ToList().OrderBy(id => id).Should().Equal(2, 3);
    }

    [Fact]
    public void CachedPreparation_RecursiveCteWholeGraphMatchesOrdinaryPreparation()
    {
        var ctx = _sut.DataProvider;

        QueryCommand<CteNumberRow> Build()
        {
            var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = ctx.From("cached_prep_nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            return ctx.WithRecursive("cached_prep_nums", anchor.UnionAll(step))
                .From("cached_prep_nums")
                .Select(t => new CteNumberRow { n = t["n"].AsInt });
        }

        var first = CachedPreparationSql(ctx, Build(), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, Build()).Should().BeTrue("the recursive CTE graph is an eligible raw shape");

        var reuses = QueryPlanner.RecipeReuses;
        CachedPreparationSql(ctx, Build(), storeInCache: true).Should().Be(first);
        (QueryPlanner.RecipeReuses - reuses).Should().BeGreaterThan(0);
        CachedPreparationSql(ctx, Build(), storeInCache: false).Should().Be(first);

        Build().ToList().Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void CachedPreparation_SetOperationsWholeGraphMatchesOrdinaryPreparation()
    {
        var ctx = _sut.DataProvider;

        QueryCommand<int> BuildExcept()
        {
            var left = _sut.SimpleEntity.Select(it => it.Id);
            var right = _sut.ComplexEntity.Select(it => (int)it.Id);
            return left.Except(right);
        }

        QueryCommand<int> BuildIntersect()
        {
            var left = _sut.SimpleEntity.Select(it => it.Id);
            var right = _sut.ComplexEntity.Select(it => (int)it.Id);
            return left.Intersect(right);
        }

        var exceptSql = CachedPreparationSql(ctx, BuildExcept(), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, BuildExcept()).Should().BeTrue("the set operation is an eligible raw shape");
        CachedPreparationSql(ctx, BuildExcept(), storeInCache: true).Should().Be(exceptSql);
        CachedPreparationSql(ctx, BuildExcept(), storeInCache: false).Should().Be(exceptSql);
        BuildExcept().ToList().OrderBy(id => id).Should().Equal(4, 5, 6, 7, 8, 9, 10);

        var intersectSql = CachedPreparationSql(ctx, BuildIntersect(), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, BuildIntersect()).Should().BeTrue();
        CachedPreparationSql(ctx, BuildIntersect(), storeInCache: true).Should().Be(intersectSql);
        CachedPreparationSql(ctx, BuildIntersect(), storeInCache: false).Should().Be(intersectSql);
        BuildIntersect().ToList().OrderBy(id => id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void CachedPreparation_DistinctWholeGraphMatchesOrdinaryPreparation()
    {
        var ctx = _sut.DataProvider;
        QueryCommand<int> Build() => _sut.SimpleEntity.Select(it => it.Id).Distinct();

        var first = CachedPreparationSql(ctx, Build(), storeInCache: true);
        CachedPreparationRawCandidateIsLive(ctx, Build()).Should().BeTrue("the DISTINCT projection is an eligible raw shape");

        var reuses = QueryPlanner.RecipeReuses;
        CachedPreparationSql(ctx, Build(), storeInCache: true).Should().Be(first);
        (QueryPlanner.RecipeReuses - reuses).Should().BeGreaterThan(0);
        CachedPreparationSql(ctx, Build(), storeInCache: false).Should().Be(first);

        Build().ToList().OrderBy(id => id).Should().Equal(CachedPreparationIdsAbove(0));
        first.Should().Contain("distinct", Exactly.Once(), "DISTINCT must survive the reuse");
    }
}
