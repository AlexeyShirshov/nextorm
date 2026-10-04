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

        public int Threshold
        {
            get
            {
                Reads++;
                return 5;
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
