using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Stage A (iteration 15, task #183) characterization of the fresh-fluent cached path that the Stage B
/// equality-scope (B1) and guarded parameter-refresh (B2) changes must preserve: closure-site identity,
/// repeated-capture naming/dedup, null/converter parameters, IN/lookup shape changes, Any/Count shared
/// command interleaving, and the unsupported-shape fallback. These tests build/prepare SQL through the
/// placeholder context (they never open a connection) unless execution is explicitly required.
/// </summary>
/// <remarks>
/// Pinned to the same collection as <c>DataContextCacheClearTests</c>, which clears the process-wide
/// <see cref="DataContextCache"/>: the identity-based "same instance on cache hit" assertions here must
/// never observe a concurrent clear. The assembly already sets <c>ParallelMode.None</c>; this makes the
/// serialization explicit/local as well (W4, checkpoint 1/3).
/// </remarks>
[Collection("DataContextCache clear")]
public class CachedPathCharacterizationTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd, bool cache = false)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, cache, CancellationToken.None);

    // ---------------------------------------------------------------------------------------------
    // Row 1: two closures of the same shape with different captured values.
    // ---------------------------------------------------------------------------------------------

    private static QueryCommand<long> WhereWithCaptured(EntityBuilder<IComplexEntity> e, int captured)
        => e.Where(x => x.Id > captured).Select(x => x.Id);

    // Two textual call sites: each compiles into its own display-class type, which participates in the
    // plan key via ExpressionPlanEqualityComparer.CompareMember (the captured value does not).
    private static QueryCommand<long> WhereAtSiteA(EntityBuilder<IComplexEntity> e)
    {
        var captured = 1;
        return e.Where(x => x.Id > captured).Select(x => x.Id);
    }

    private static QueryCommand<long> WhereAtSiteB(EntityBuilder<IComplexEntity> e)
    {
        var captured = 2;
        return e.Where(x => x.Id > captured).Select(x => x.Id);
    }

    [Fact]
    public void SameClosureSite_DifferentValues_ShouldReusePlanAndRefreshParam()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = Prepare(ctx, WhereWithCaptured(e, 1), cache: true);
        first.DbCommandParams[0].Value.Should().Be(1);
        first.NeedsParamRefresh.Should().BeTrue("a captured local is not fixed by the expression shape");

        var second = Prepare(ctx, WhereWithCaptured(e, 2), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        second.DbCommandParams[0].Value.Should().Be(2, "the shared cached command re-binds the second closure's value");
    }

    [Fact]
    public void DistinctClosureSites_SameShape_ShouldNotShareThePlanKey()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = Prepare(ctx, WhereAtSiteA(e), cache: true);
        var second = Prepare(ctx, WhereAtSiteB(e), cache: true);

        ReferenceEquals(first, second).Should()
            .BeFalse("each closure site owns a distinct display-class type, which is part of the key");
        first.DbCommandParams[0].Value.Should().Be(1);
        second.DbCommandParams[0].Value.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // Row 2: repeated captures in WHERE / JOIN / projection.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RepeatedCapture_AcrossJoinWhereProjection_ShouldBindOnce()
    {
        using var ctx = SqliteTestContext.Create();
        var v = 5;

        var prepared = Prepare(ctx, ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id && s.Id > v)
            .Where(p => p.Item1.Int == v)
            .Select(p => new { p.Item1.Id, Captured = v }));

        var parameters = prepared.DbCommandParams.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle("one captured local is one parameter no matter how often it repeats");
        parameters[0].ParameterName.Should().Be("v");
        parameters[0].Value.Should().Be(5);
        prepared.DbCommand.CommandText.Should().Contain("$v");
    }

    // ---------------------------------------------------------------------------------------------
    // Row 3: null and converter parameter cases.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NullCaptured_ShouldBindANullValuedParameter()
    {
        using var ctx = SqliteTestContext.Create();
        string? captured = null;

        var prepared = Prepare(ctx, ctx.From<IComplexEntity>().Where(x => x.String == captured).Select(x => x.Id));

        var parameter = prepared.DbCommandParams.Cast<DbParameter>().Should().ContainSingle().Subject;
        (parameter.Value is null || parameter.Value is DBNull).Should()
            .BeTrue("a null capture is bound as a (normalized) null parameter, never inlined");
    }

    [Fact]
    public void ConvertedCapturedEnum_ShouldBindTheProviderValue()
    {
        using var ctx = SqliteTestContext.Create();
        var captured = SqlGenState.Closed;

        var prepared = Prepare(ctx, ctx.From<IConverterEntity>().Where(x => x.State == captured).Select(x => x.Id));

        var parameter = prepared.DbCommandParams.Cast<DbParameter>().Should().ContainSingle().Subject;
        parameter.Value.Should().Be("Closed", "the value converter maps the enum to its provider representation");
    }

    // ---------------------------------------------------------------------------------------------
    // Row 4: IN / lookup form and value change, plus filters / settings.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void InCollection_LengthChange_ShouldNotReuseTheStalePlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var values = new long[] { 1 };
        var first = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);
        first.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0");

        values = new long[] { 1, 2 };
        var second = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should()
            .BeFalse("a changed IN cardinality is a different shape and must not reuse the cached plan");
        second.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0", "p1");
    }

    // Same cardinality but a changed value is the same shape: it must hit the cached plan and re-bind the
    // fresh value (the value-list parameter is a runtime pN placeholder, so the recipe declines and the
    // original extraction path refreshes it).
    [Fact]
    public void InCollection_SameLengthValueChange_ShouldReusePlanAndRebind()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var values = new long[] { 1 };
        var first = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);
        first.DbCommandParams[0].Value.Should().Be(1L);

        ExpressionPlanEqualityComparer.ResetCounters();
        values[0] = 2;
        var second = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("an unchanged IN cardinality is the same shape");
        second.DbCommandParams[0].Value.Should().Be(2L, "the shared cached command re-binds the current value");
        ParamRefreshRecipe.FastBindHits.Should().Be(0, "a runtime pN placeholder is outside the recipe subset");
        QueryPlanner.FallbackRefreshes.Should().Be(1, "the value-list parameter refreshes through the extraction path");
    }

    // An empty IN is a constant `1 = 0` with no parameters: a rebuilt empty IN must reuse the same
    // parameter-free plan without inventing a recipe or a fallback refresh.
    [Fact]
    public void InCollection_Empty_ShouldReuseParameterFreePlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var values = Array.Empty<long>();

        var first = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);
        first.DbCommandParams.Cast<DbParameter>().Should().BeEmpty("an empty IN renders a constant and binds no parameter");
        first.DbCommand.CommandText.Should().Contain("1 = 0");

        ExpressionPlanEqualityComparer.ResetCounters();
        var second = Prepare(ctx, e.Where(x => SqlFunctions.Sql.@in(x.Id, values)).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("an empty IN is a stable, parameter-free shape");
        second.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0, "a parameter-free plan never enters the refresh path");
    }

    [Fact]
    public void SettingsAndPreWhere_ShouldDistinguishThePlanKey()
    {
        using var ctx = SqliteTestContext.Create();

        // Settings/PREWHERE are not renderable on SQLite, but they are still part of the structural plan
        // identity; prepare-without-render and compare the keys, exactly like PlanKeyUniquenessTests does.
        var plain = ctx.From<IComplexEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var withSetting = ctx.From<IComplexEntity>().Where(x => x.Id > 0).Settings(("max_threads", "2")).Select(x => x.Id);
        var withPreWhere = ctx.From<IComplexEntity>().Where(x => x.Id > 0).PreWhere(x => x.Id > 5).Select(x => x.Id);

        plain.PrepareCommand(false, TestContext.Current.CancellationToken);
        withSetting.PrepareCommand(false, TestContext.Current.CancellationToken);
        withPreWhere.PrepareCommand(false, TestContext.Current.CancellationToken);

        var comparer = plain.GetQueryPlanEqualityComparer();
        comparer.Equals(plain, withSetting).Should().BeFalse("a setting is part of the plan identity");
        comparer.Equals(plain, withPreWhere).Should().BeFalse("a PREWHERE is part of the plan identity");
    }

    // ---------------------------------------------------------------------------------------------
    // Row 5: Any / Count interleaved with a CTE and a DML source (no sticky cache state).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AnyCountInterleavedWithCteAndDmlSource_ShouldKeepTheSharedCommandCacheable()
    {
        using var ctx = SqliteTestContext.CreateSqlite();
        ctx.PurgeQueryCache();

        // Materialise the context-shared Any command without executing it (the placeholder boundary has
        // no reader). This is the command AGENTS.md warns must never get the sticky Cache=false flag.
        var shared = EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<IComplexEntity>().ToCommand());
        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue();

        var cte = ctx.With("c", ctx.From<IComplexEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("c").Select(t => t["id"].AsInt);
        var cteFirst = Prepare(ctx, cte, cache: true);
        var cteSecond = Prepare(ctx, cte, cache: true);
        ReferenceEquals(cteFirst, cteSecond).Should().BeTrue("a rebuilt read CTE must hit the cached plan");

        // Prepare a DML source in between: it must not poison the process/context plan state.
        var delete = ctx.CreateDeleteBuilder<ConverterEntity>().Where(x => x.Id > 0).BuildKeyCommand(new ConverterEntity { Id = 1 });
        delete.Condition!.PrepareCommand(false, TestContext.Current.CancellationToken);
        delete.Condition.Cache.Should().BeTrue("preparing a DML source must not disable its plan cache");

        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue("the DML interleave must not disable the shared Any command's plan cache");
        ReferenceEquals(Prepare(ctx, cte, cache: true), cteFirst).Should().BeTrue("the read CTE plan survives the DML interleave");
    }

    // ---------------------------------------------------------------------------------------------
    // Row 8: fallback error path and evaluation-once on the refresh path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void UnsupportedLookupOutsideWhere_ShouldThrowAndNotPoisonThePlanCache()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();

        var lookup = new Dictionary<int, DateTime> { [1] = new(2024, 1, 1) };
        var act = () => ctx.GetPreparedQueryCommand(
            ctx.From<IComplexEntity>().Select(x => new { Value = lookup[x.Int!.Value] }), false, true, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*WHERE*");

        // The failed cacheable preparation must not leave a poisoned entry behind.
        var ok = Prepare(ctx, ctx.From<IComplexEntity>().Select(x => x.Id), cache: true);
        ReferenceEquals(Prepare(ctx, ctx.From<IComplexEntity>().Select(x => x.Id), cache: true), ok).Should().BeTrue();
    }

    private sealed class CountingProbe
    {
        public int Reads;
        public int Value = 5;

        public int Threshold
        {
            get
            {
                Reads++;
                return Value;
            }
        }

        public int Other
        {
            get
            {
                Reads++;
                return Value;
            }
        }
    }

    [Fact]
    public void CapturedProperty_OnCacheHit_ShouldBeEvaluatedExactlyOnce()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var probe = new CountingProbe();

        var first = Prepare(ctx, e.Where(x => x.Id > probe.Threshold).Select(x => x.Id), cache: true);

        probe.Reads = 0; // isolate the cache-hit refresh
        var second = Prepare(ctx, e.Where(x => x.Id > probe.Threshold).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should().BeTrue();
        second.NeedsParamRefresh.Should().BeTrue();
        probe.Reads.Should().Be(1, "a captured parameter refresh must evaluate the captured member exactly once");
    }

    // Same source site, two different probe instances: the closure type and the member chain are part of
    // the plan key while the captured value is not, so the second Prepare is a cache hit with a fresh
    // closure.
    private static QueryCommand<long> WhereProbe(EntityBuilder<IComplexEntity> e, CountingProbe probe)
        => e.Where(x => x.Id > probe.Threshold).Select(x => x.Id);

    [Fact]
    public void SameClosureSite_RepeatedExecution_ShouldNotReuseFirstClosure()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var a = new CountingProbe { Value = 1 };
        var b = new CountingProbe { Value = 2 };

        var first = Prepare(ctx, WhereProbe(e, a), cache: true);
        first.DbCommandParams[0].Value.Should().Be(1);
        first.NeedsParamRefresh.Should().BeTrue();
        first.ParamRecipe.Should().NotBeNull("a simple captured scalar is in the supported recipe subset");

        a.Reads = 0;
        b.Reads = 0;
        var second = Prepare(ctx, WhereProbe(e, b), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        b.Reads.Should().Be(1, "the current closure is evaluated exactly once on the refresh");
        a.Reads.Should().Be(0, "the reusable recipe must not reuse the first closure");
        second.DbCommandParams[0].Value.Should().Be(2, "the shared cached command re-binds the second closure's value");
    }

    // A real cache hit with a supported recipe takes the positional fast path: it reads the CURRENT
    // closure, binds exactly once and never touches the original extraction path.
    [Fact]
    public void CachedHit_FastPath_ShouldBindCurrentValueAndCountOnce()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var probe = new CountingProbe { Value = 1 };

        var first = Prepare(ctx, WhereProbe(e, probe), cache: true);
        first.ParamRecipe.Should().NotBeNull("a simple captured scalar is in the supported recipe subset");
        first.DbCommandParams[0].Value.Should().Be(1);

        probe.Reads = 0;
        probe.Value = 2;
        ExpressionPlanEqualityComparer.ResetCounters();
        var second = Prepare(ctx, WhereProbe(e, probe), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        ParamRefreshRecipe.BindRejected.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
        second.DbCommandParams[0].Value.Should().Be(2, "the fast path binds the CURRENT closure value");
        probe.Reads.Should().Be(1, "the fast path reads the captured member exactly once");
    }

    // A null capture is still a reproducible scalar: the fast path rebinds the value once the closure
    // changes from null to a non-null string.
    [Fact]
    public void CachedHit_NullCaptureFastPath_ShouldBindCurrentValue()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        string? captured = null;
        var first = Prepare(ctx, e.Where(x => x.String == captured).Select(x => x.Id), cache: true);
        first.ParamRecipe.Should().NotBeNull("a null captured scalar still has a reproducible recipe");

        ExpressionPlanEqualityComparer.ResetCounters();
        captured = "x";
        var second = Prepare(ctx, e.Where(x => x.String == captured).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should().BeTrue();
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
        second.DbCommandParams[0].Value.Should().Be("x", "the null-to-value change is rebound by the fast path");
    }

    // The inverse transition exercises the writer's coalesce branch: a value capture that becomes null on
    // the hit must bind DBNull through the fast path, never the stale miss-time string.
    [Fact]
    public void CachedHit_ValueToNullFastPath_ShouldBindDbNull()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        string? captured = "first";
        var first = Prepare(ctx, e.Where(x => x.String == captured).Select(x => x.Id), cache: true);
        first.ParamRecipe.Should().NotBeNull("a captured string scalar is a supported recipe");

        ExpressionPlanEqualityComparer.ResetCounters();
        captured = null;
        var second = Prepare(ctx, e.Where(x => x.String == captured).Select(x => x.Id), cache: true);

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        ParamRefreshRecipe.BindRejected.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
        second.DbCommandParams[0].Value.Should()
            .Be(DBNull.Value, "the writer's null-coalesce branch normalizes a null capture to DBNull");
    }

    // Converter and raw-SQL shapes are not in the scalar subset, so the plan carries no recipe and no
    // hit takes the fast path. The converter shape still refreshes through the original extraction
    // path (a counted fallback); a raw-SQL override instead reuses its miss-time bound parameters and
    // never enters the generated extraction path, so it is not a fallback refresh.
    [Fact]
    public void CachedHit_UnsupportedShapes_ShouldFallBackWithoutFastPath()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();

        var converter = ctx.From<IConverterEntity>();
        var captured = SqlGenState.Closed;
        var converterFirst = Prepare(ctx, converter.Where(x => x.State == captured).Select(x => x.Id), cache: true);
        converterFirst.ParamRecipe.Should().BeNull("a converter-normalized value is not in the scalar subset");
        ExpressionPlanEqualityComparer.ResetCounters();
        var converterSecond = Prepare(ctx, converter.Where(x => x.State == captured).Select(x => x.Id), cache: true);
        ReferenceEquals(converterFirst, converterSecond).Should().BeTrue();
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        converterSecond.DbCommandParams[0].Value.Should().Be("Closed");

        var e = ctx.From<IComplexEntity>();
        const string sql = "select id from complex_entity where id = @id";
        var rawFirst = Prepare(ctx, RawSqlCommand(e, sql, new { id = 1 }), cache: true);
        rawFirst.ParamRecipe.Should().BeNull("a raw-SQL override has no generated captured condition");
        ExpressionPlanEqualityComparer.ResetCounters();
        var rawSecond = Prepare(ctx, RawSqlCommand(e, sql, new { id = 2 }), cache: true);
        ReferenceEquals(rawFirst, rawSecond).Should().BeTrue();
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0, "a raw-SQL override bypasses the generated extraction path entirely");
    }

    // The immutable recipe validates parameter count/name/shape before evaluating any closure; an
    // incompatible recipe must fall back to the original ExtractParams path without corrupting the bind.
    [Fact]
    public void CachedPlan_WithIncompatibleRecipe_ShouldFallBackWithoutCorruptingTheBind()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var a = new CountingProbe { Value = 1 };
        var b = new CountingProbe { Value = 2 };

        var target = Prepare(ctx, WhereProbe(e, a), cache: true);
        target.ParamRecipe.Should().NotBeNull();

        // A recipe whose slot count does not match the cached command's parameter count.
        var countMismatch = Prepare(ctx, e.Where(x => x.Id > a.Threshold || x.Id > b.Other).Select(x => x.Id), cache: true);
        countMismatch.ParamRecipe.Should().NotBeNull();
        target.SetParamRecipe(countMismatch.ParamRecipe);

        a.Reads = 0;
        b.Reads = 0;
        ExpressionPlanEqualityComparer.ResetCounters();
        var countHit = Prepare(ctx, WhereProbe(e, b), cache: true);

        ReferenceEquals(target, countHit).Should().BeTrue("the second WhereProbe hits the cached plan");
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        countHit.DbCommandParams[0].Value.Should().Be(2, "the fallback extracts the current closure value");
        b.Reads.Should().Be(1, "the original extraction path evaluates the current closure exactly once");
        a.Reads.Should().Be(0, "the incompatible recipe never evaluates the stale closure");

        // A same-count recipe whose parameter name differs must also be rejected.
        var nameMismatch = Prepare(ctx, e.Where(x => x.Id > a.Other).Select(x => x.Id), cache: true);
        nameMismatch.ParamRecipe.Should().NotBeNull();
        target.SetParamRecipe(nameMismatch.ParamRecipe);

        a.Reads = 0;
        b.Reads = 0;
        ExpressionPlanEqualityComparer.ResetCounters();
        var nameHit = Prepare(ctx, WhereProbe(e, b), cache: true);

        ReferenceEquals(target, nameHit).Should().BeTrue();
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        nameHit.DbCommandParams[0].Value.Should().Be(2);
        b.Reads.Should().Be(1);
        a.Reads.Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Row: converter / raw SQL. The converter half is covered by
    // ConvertedCapturedEnum_ShouldBindTheProviderValue above; this is the raw-SQL fallback baseline.
    // A raw-SQL override (WithSql / PrepareFromSql) bypasses the generated-SQL and ExtractParams path
    // (SqlStmt is null on the prepared command), and its plan identity is the injected SQL text:
    // the generated expression shape and the parameter values are not part of the key. This is the
    // documented fallback the B2 guarded refresh must keep (raw SQL is not a supported fastpath shape).
    // ---------------------------------------------------------------------------------------------

    private static QueryCommand<long> RawSqlCommand(EntityBuilder<IComplexEntity> e, string sql, object? @params)
        => e.Select(x => x.Id).WithSql(sql, @params);

    [Fact]
    public void RawSqlOverride_FormAndValueChange_ShouldKeyThePlanBySqlTextOnly()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        const string sql = "select id from complex_entity where id = @id";

        var first = Prepare(ctx, RawSqlCommand(e, sql, new { id = 1 }), cache: true);
        first.SqlStmt.Should().BeNull("a raw-SQL override bypasses the generated-SQL and ExtractParams path");

        // Same SQL text, different parameter value: the value is not part of the raw-SQL plan key.
        var sameText = Prepare(ctx, RawSqlCommand(e, sql, new { id = 2 }), cache: true);
        ReferenceEquals(first, sameText).Should()
            .BeTrue("the raw SQL text, not the parameter value, is the raw-SQL plan identity");

        // Different SQL text: a different raw-SQL shape must not reuse the cached plan.
        var otherText = Prepare(ctx, RawSqlCommand(e, "select id from complex_entity where id = @id and id > 0", new { id = 1 }), cache: true);
        ReferenceEquals(first, otherText).Should()
            .BeFalse("a changed raw SQL text must not reuse the cached plan");
    }
}
