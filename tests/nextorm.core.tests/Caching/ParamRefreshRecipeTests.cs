using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Direct coverage of the immutable guarded parameter-refresh recipe and the planner's fast/mismatch
/// accounting on the cached path. The provider-free <see cref="RecipeTestContext"/> runs the real
/// <c>QueryPlanner</c> (mirroring <c>Iteration14CteLookupTests</c>) so a miss/hit pair exercises the
/// actual <c>TryCreate</c>/<c>TryBind</c>/<c>RefreshParameters</c> path without a database.
/// </summary>
[Collection("Query cache controls")]
public class ParamRefreshRecipeTests
{
    private sealed class RecipeEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class NestedProbeHolder
    {
        public CountingProbe Probe { get; set; } = new();
    }

    private sealed class ValueHolder
    {
        public int value;
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

        public string? Text
        {
            get
            {
                Reads++;
                return "text";
            }
        }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd, bool cache = true)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, cache, CancellationToken.None);

    // Same source site, so the captured value (not its shape) changes between calls and the second
    // Prepare is a cache hit with a fresh closure.
    private static QueryCommand<int> WhereCaptured(EntityBuilder<RecipeEntity> e, int value)
        => e.Where(x => x.Id > value).Select(x => x.Id);

    private static QueryCommand<int> WhereProbe(EntityBuilder<RecipeEntity> e, CountingProbe probe)
        => e.Where(x => x.Id > probe.Threshold).Select(x => x.Id);

    private static QueryCommand<string?> WhereText(EntityBuilder<RecipeEntity> e, CountingProbe probe)
        => e.Where(x => x.Name == probe.Text).Select(x => x.Name);

    private static QueryCommand<int> WhereOther(EntityBuilder<RecipeEntity> e, CountingProbe probe)
        => e.Where(x => x.Id > probe.Other).Select(x => x.Id);

    private static QueryCommand<int> WhereTwo(EntityBuilder<RecipeEntity> e, CountingProbe a, CountingProbe b)
        => e.Where(x => x.Id > a.Threshold || x.Id > b.Other).Select(x => x.Id);

    // A capture reached through a member chain on a captured object (not a direct closure field), so the
    // compiled writer walks closure.Probe.Threshold and BuildPath follows the same nested chain.
    private static QueryCommand<int> WhereNestedProbe(EntityBuilder<RecipeEntity> e, NestedProbeHolder holder)
        => e.Where(x => x.Id > holder.Probe.Threshold).Select(x => x.Id);

    // Builds a prepared provider-free command whose only WHERE capture is `captured`, so the shape guard
    // is the sole difference when a shape clause is added through `configure`.
    private static QueryCommand<int> CreateCapturedCommand(
        RecipeTestContext ctx,
        int captured,
        Func<QueryDefinition, QueryDefinition>? configure = null)
    {
        Expression<Func<RecipeEntity, bool>> condition = x => x.Id > captured;
        var definition = new QueryDefinition
        {
            SrcType = typeof(RecipeEntity),
            Exp = (Expression<Func<RecipeEntity, int>>)(x => x.Id),
            Condition = condition,
        };

        if (configure is not null)
            definition = configure(definition);

        var command = ctx.CreateCommand<int>(definition);
        command.PrepareCommand(false, CancellationToken.None);
        return command;
    }

    private static void AssertRecipeNull(QueryCommand<int> command)
    {
        var key = new ExpressionKey(command.PreparedCondition!, command);
        var parameter = new Parameter("captured", 1) { CapturedKey = key };
        ParamRefreshRecipe.TryCreate(command, [parameter]).Should()
            .BeNull("the unsupported shape must keep the original extraction path");
    }

    // The captured scalar lives ONLY in the projection: the WHERE captures nothing. The recipe must
    // therefore navigate the projection root on a hit, not the prepared condition.
    private static QueryCommand<int> ProjectionOnlyCaptured(EntityBuilder<RecipeEntity> e, CountingProbe probe)
        => e.Where(x => x.Id > 0).Select(x => x.Id + probe.Threshold);

    [Fact]
    public void ProjectionOnlyCapture_OnCacheHit_ShouldBindFreshProjectionValueWithoutThrowing()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var a = new CountingProbe { Value = 1 };
        var b = new CountingProbe { Value = 2 };

        var first = Prepare(ctx, ProjectionOnlyCaptured(e, a));
        first.ParamRecipe.Should().NotBeNull("a projection-only captured scalar is part of the supported recipe subset");
        first.DbCommandParams.Cast<DbParameter>().Should().ContainSingle();
        first.DbCommandParams[0].Value.Should().Be(1);

        ExpressionPlanEqualityComparer.ResetCounters();
        a.Reads = 0;
        b.Reads = 0;

        var second = Prepare(ctx, ProjectionOnlyCaptured(e, b));

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        second.DbCommandParams[0].Value.Should().Be(2, "the recipe navigates the projection root for a projection-only capture");
        b.Reads.Should().Be(1, "the fresh projection closure is read exactly once");
        a.Reads.Should().Be(0, "the stale closure is never read");
        QueryPlanner.FallbackRefreshes.Should().Be(0, "a resolved projection path takes the fast path");
    }

    [Fact]
    public void FastPath_OnCacheHit_ShouldBindCurrentClosureAndCountOnce()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();

        var first = Prepare(ctx, WhereCaptured(e, 1));
        first.ParamRecipe.Should().NotBeNull("a simple captured scalar is in the supported recipe subset");

        ExpressionPlanEqualityComparer.ResetCounters();

        var second = Prepare(ctx, WhereCaptured(e, 2));

        ReferenceEquals(first, second).Should().BeTrue("the captured value is not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        ParamRefreshRecipe.BindRejected.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
        second.DbCommandParams[0].Value.Should().Be(2, "the positional recipe binds the fresh closure's value");
    }

    [Fact]
    public void MismatchedRecipe_ShouldRejectAndFallBackWithoutReadingTheStaleClosure()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var a = new CountingProbe { Value = 1 };
        var b = new CountingProbe { Value = 2 };

        var target = Prepare(ctx, WhereProbe(e, a));
        target.ParamRecipe.Should().NotBeNull();

        // A recipe whose slot count differs from the cached command's parameter count.
        var countMismatch = Prepare(ctx, WhereTwo(e, a, b));
        countMismatch.ParamRecipe.Should().NotBeNull();
        target.SetParamRecipe(countMismatch.ParamRecipe);

        a.Reads = 0;
        b.Reads = 0;
        ExpressionPlanEqualityComparer.ResetCounters();

        var countHit = Prepare(ctx, WhereProbe(e, b));

        ReferenceEquals(target, countHit).Should().BeTrue("the second WhereProbe hits the cached plan");
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        countHit.DbCommandParams[0].Value.Should().Be(2, "the fallback extracts the current closure value");
        b.Reads.Should().Be(1, "the original extraction path evaluates the current closure exactly once");
        a.Reads.Should().Be(0, "the incompatible recipe never evaluates the stale closure");

        // A same-count recipe whose parameter name differs must also be rejected (name + order check).
        var nameMismatch = Prepare(ctx, WhereOther(e, a));
        nameMismatch.ParamRecipe.Should().NotBeNull();
        target.SetParamRecipe(nameMismatch.ParamRecipe);

        a.Reads = 0;
        b.Reads = 0;
        ExpressionPlanEqualityComparer.ResetCounters();

        var nameHit = Prepare(ctx, WhereProbe(e, b));

        ReferenceEquals(target, nameHit).Should().BeTrue();
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        nameHit.DbCommandParams[0].Value.Should().Be(2);
        b.Reads.Should().Be(1);
        a.Reads.Should().Be(0);
    }

    [Fact]
    public void NullRecipe_ShouldFallBackOnEveryHit()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();

        var target = Prepare(ctx, WhereCaptured(e, 1));
        target.ParamRecipe.Should().NotBeNull();

        // Simulate an unsupported (recipe-null) shape: no fast path, the original extraction runs.
        target.SetParamRecipe(null);

        ExpressionPlanEqualityComparer.ResetCounters();
        var hit = Prepare(ctx, WhereCaptured(e, 7));

        ReferenceEquals(target, hit).Should().BeTrue();
        ParamRefreshRecipe.FastBindHits.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        hit.DbCommandParams[0].Value.Should().Be(7);
    }

    [Fact]
    public void Fallback_SameCountNameMismatch_OnRealHit_ShouldBindPositionallyWithoutThrowing()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var probe = new CountingProbe { Value = 3 };

        var first = Prepare(ctx, WhereProbe(e, probe));
        first.ParamRecipe.Should().NotBeNull();

        // Break the cached placeholder name so the recipe rejects (its name check) and the fallback runs
        // on a REAL cache hit with a same-count name mismatch - the state the original Release extractor
        // bound positionally. Reaching the fallback through a real hit (not the SetParamRecipe seam) is
        // the point: it must never throw.
        first.DbCommandParams[0].ParameterName = "renamed";

        ExpressionPlanEqualityComparer.ResetCounters();
        probe.Value = 7;

        Func<DbPreparedQueryCommand<int>> act = () => Prepare(ctx, WhereProbe(e, probe));
        var hit = act.Should().NotThrow().Subject;

        ReferenceEquals(first, hit).Should().BeTrue("the second WhereProbe hits the cached plan");
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        hit.DbCommandParams[0].Value.Should().Be(7, "the fallback binds positionally even when the cached name differs");
    }

    [Fact]
    public void Fallback_SameCountOrderMismatch_OnRealHit_ShouldBindPositionallyWithoutThrowing()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var a = new CountingProbe { Value = 1 };
        var b = new CountingProbe { Value = 2 };

        var first = Prepare(ctx, WhereTwo(e, a, b));
        first.ParamRecipe.Should().NotBeNull();
        first.DbCommandParams.Count.Should().Be(2);

        // Swap the two cached placeholder names so the recipe's per-slot name/order check rejects while
        // the count still matches: the original extractor's positional semantics must survive a real hit.
        var name0 = first.DbCommandParams[0].ParameterName;
        first.DbCommandParams[0].ParameterName = first.DbCommandParams[1].ParameterName;
        first.DbCommandParams[1].ParameterName = name0;

        var a2 = new CountingProbe { Value = 11 };
        var b2 = new CountingProbe { Value = 22 };

        ExpressionPlanEqualityComparer.ResetCounters();
        Func<DbPreparedQueryCommand<int>> act = () => Prepare(ctx, WhereTwo(e, a2, b2));
        var hit = act.Should().NotThrow().Subject;

        ReferenceEquals(first, hit).Should().BeTrue("the second WhereTwo hits the cached plan");
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        hit.DbCommandParams[0].Value.Should().Be(11, "the first slot keeps the first captured value");
        hit.DbCommandParams[1].Value.Should().Be(22, "the second slot keeps the second captured value");
    }

    [Fact]
    public void Fallback_CountMismatch_ShouldNotPartiallyBindAndShouldInvalidateTheRecipe()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var probe = new CountingProbe { Value = 4 };

        var first = Prepare(ctx, WhereProbe(e, probe));
        first.ParamRecipe.Should().NotBeNull();
        first.DbCommandParams[0].Value.Should().Be(4);

        // Give the cached statement one more placeholder than the fresh command produces. A matched plan
        // key should not produce this, but the fallback must stay safe: the stale trailing slot must not
        // be bound and the prefix must not be partially written.
        first.DbCommandParams.Add(new FakeParameter("extra") { Value = "stale" });

        ExpressionPlanEqualityComparer.ResetCounters();
        probe.Value = 9;

        var hit = Prepare(ctx, WhereProbe(e, probe));

        ReferenceEquals(first, hit).Should().BeTrue();
        ParamRefreshRecipe.BindRejected.Should().Be(1);
        QueryPlanner.FallbackRefreshes.Should().Be(1);
        hit.ParamRecipe.Should().BeNull("the count-mismatched recipe is invalidated so the next hit re-plans");
        hit.DbCommandParams[0].Value.Should().Be(4, "no partial bind writes the prefix on a count mismatch");
    }

    [Fact]
    public void TryCreate_ShouldRefuseUnsupportedParameterShapes()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();

        var command = WhereCaptured(e, 1);
        command.PrepareCommand(false, CancellationToken.None);
        var key = new ExpressionKey(command.PreparedCondition!, command);

        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1)])
            .Should().BeNull("a parameter without a captured key is not part of the scalar subset");

        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1) { CapturedKey = key, Stable = true }])
            .Should().BeNull("a stable parameter is fixed by the shape");

        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1) { CapturedKey = key, HasConversion = true }])
            .Should().BeNull("a converter-normalized value cannot be reproduced from the raw capture");

        ParamRefreshRecipe.TryCreate(command, [new Parameter("p0", 1) { CapturedKey = key }])
            .Should().BeNull("a runtime placeholder is not a captured closure scalar");
    }

    // ------------------------------------------------------------------------------------------------
    // D4 test-matrix rows: parameter-count edge cases, duplicate/nested captures, shape guards,
    // concurrency and allocation bounds.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void TryCreate_ShouldReturnNullWhenThereAreNoParameters()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();

        var command = WhereCaptured(e, 1);
        command.PrepareCommand(false, CancellationToken.None);

        // No parameters means there is nothing to re-bind: the original extraction path (which also
        // handles cleared/empty lists) must stay in charge, and no empty recipe may be built.
        ParamRefreshRecipe.TryCreate(command, []).Should().BeNull("an empty parameter list has no recipe");

        // The same command with the one captured parameter is a supported shape, proving the null above
        // is the empty-count guard and not the shape guard.
        var key = new ExpressionKey(command.PreparedCondition!, command);
        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1) { CapturedKey = key }])
            .Should().NotBeNull("the same command has a recipe once it has a captured parameter");
    }

    [Fact]
    public void TryCreate_DuplicateCaptureNameFromDistinctClosures_ShouldReturnNull()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var command = CreateCapturedCommand(ctx, 1);
        var field = typeof(ValueHolder).GetField(nameof(ValueHolder.value))!;

        var entity = Expression.Parameter(typeof(RecipeEntity), "x");
        var id = Expression.Property(entity, nameof(RecipeEntity.Id));
        var left = Expression.Field(Expression.Constant(new ValueHolder { value = 1 }), field);
        var right = Expression.Field(Expression.Constant(new ValueHolder { value = 2 }), field);
        var distinct = Expression.Lambda(
            Expression.AndAlso(Expression.GreaterThan(id, left), Expression.GreaterThan(id, right)),
            entity);
        command.PreparedCondition = distinct;

        // The Collector keys captures by member name; the same name reached from two distinct closure
        // instances is ambiguous (ParamRefreshRecipe.cs Collector.VisitMember), so no recipe is built.
        var distinctKey = new ExpressionKey(distinct, command);
        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1) { CapturedKey = distinctKey }])
            .Should().BeNull("the same capture name from two distinct closures is an unambiguous-recipe failure");

        // Control: reading the very same closure instance twice is one capture and still builds a recipe.
        var same = Expression.Field(Expression.Constant(new ValueHolder { value = 1 }), field);
        var repeated = Expression.Lambda(
            Expression.AndAlso(Expression.GreaterThan(id, same), Expression.GreaterThan(id, same)),
            entity);
        command.PreparedCondition = repeated;
        var repeatedKey = new ExpressionKey(repeated, command);
        ParamRefreshRecipe.TryCreate(command, [new Parameter("value", 1) { CapturedKey = repeatedKey }])
            .Should().NotBeNull("a repeated read of the same closure is one capture, not a collision");
    }

    [Fact]
    public void NestedCapturePath_OnCacheHit_ShouldBindFreshValue()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var first = new NestedProbeHolder { Probe = new CountingProbe { Value = 1 } };
        var second = new NestedProbeHolder { Probe = new CountingProbe { Value = 2 } };

        var miss = Prepare(ctx, WhereNestedProbe(e, first));
        miss.ParamRecipe.Should().NotBeNull("a member chain through a captured object is a supported path");

        ExpressionPlanEqualityComparer.ResetCounters();
        var hit = Prepare(ctx, WhereNestedProbe(e, second));

        ReferenceEquals(miss, hit).Should().BeTrue("the wrapper value is not part of the plan key");
        ParamRefreshRecipe.FastBindHits.Should().Be(1);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
        hit.DbCommandParams[0].Value.Should().Be(2, "the nested path resolves and reads the fresh wrapper member");
    }

    [Fact]
    public void HasSupportedShape_ShouldAcceptPlainCapturedWhere()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(ctx, 1);
        var key = new ExpressionKey(command.PreparedCondition!, command);

        ParamRefreshRecipe.TryCreate(command, [new Parameter("captured", 1) { CapturedKey = key }])
            .Should().NotBeNull("a plain captured scalar WHERE is the supported base shape");
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectGrouping()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(
            ctx,
            1,
            d => d with { Group = (Expression<Func<RecipeEntity, int>>)(x => x.Id) });

        AssertRecipeNull(command);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectHaving()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(
            ctx,
            1,
            d => d with
            {
                Group = (Expression<Func<RecipeEntity, int>>)(x => x.Id),
                Having = (Expression<Func<RecipeEntity, bool>>)(x => x.Id > 0),
            });

        AssertRecipeNull(command);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectPreWhere()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(
            ctx,
            1,
            d => d with { PreWhere = (Expression<Func<RecipeEntity, bool>>)(x => x.Id > 0) });

        AssertRecipeNull(command);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectWindows()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(
            ctx,
            1,
            d => d with { Windows = [new WindowDefinition("w", [], [], null)] });

        AssertRecipeNull(command);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectArrayJoin()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(ctx, 1);
        command._preparedArrayJoin = [Expression.Constant(Array.Empty<int>())];

        AssertRecipeNull(command);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectUnion()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(ctx, 1);
        var other = CreateCapturedCommand(ctx, 2);
        var unioned = command.Union(other);
        unioned.PrepareCommand(false, CancellationToken.None);

        AssertRecipeNull(unioned);
    }

    [Fact]
    public void HasSupportedShape_ShouldRejectOuterReferences()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();

        var command = CreateCapturedCommand(ctx, 1);
        // A fresh command has no outer registry, so the reference is recorded on this command itself.
        command.OuterRegistry = null;
        command.AddOuterReference(Expression.Constant(1));

        command.OuterReferences.Should().NotBeNullOrEmpty("the guard clause under test needs a registered outer reference");
        AssertRecipeNull(command);
    }

    [Fact]
    public async Task TryBind_ConcurrentDistinctCommands_ShouldBindCorrectValuesWithoutCorruption()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();

        var seed = new CountingProbe { Value = 50 };
        var recipeCommand = Prepare(ctx, WhereProbe(e, seed));
        recipeCommand.ParamRecipe.Should().NotBeNull();
        var recipe = recipeCommand.ParamRecipe!;

        const int threads = 8;
        var fresh = new QueryCommand<int>[threads];
        var prepared = new DbPreparedQueryCommand<int>[threads];
        for (var i = 0; i < threads; i++)
        {
            fresh[i] = WhereProbe(e, new CountingProbe { Value = i + 1 });
            prepared[i] = Prepare(ctx, fresh[i], cache: false);
        }

        ExpressionPlanEqualityComparer.ResetCounters();

        var barrier = new Barrier(threads);
        var results = new bool[threads];
        var failures = new List<Exception>();
        var tasks = new Task[threads];
        for (var i = 0; i < threads; i++)
        {
            var index = i;
            tasks[index] = Task.Run(
                () =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        results[index] = recipe.TryBind(fresh[index], prepared[index].DbCommandParams);
                    }
                    catch (Exception ex)
                    {
                        lock (failures)
                            failures.Add(ex);
                    }
                },
                TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);

        failures.Should().BeEmpty("the immutable recipe must be safe to bind concurrently");
        results.Should().OnlyContain(bound => bound, "every distinct command must resolve its own fresh closure");
        for (var i = 0; i < threads; i++)
            prepared[i].DbCommandParams[0].Value.Should().Be(i + 1, "each command keeps its own closure's value");

        ParamRefreshRecipe.FastBindHits.Should().Be(threads, "every concurrent bind counted exactly once");
        ParamRefreshRecipe.BindRejected.Should().Be(0);
        QueryPlanner.FallbackRefreshes.Should().Be(0);
    }

    [Fact]
    public void FastPath_ShouldReadTheFreshClosureExactlyOnce_AndNotAtCreate()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var probe = new CountingProbe { Value = 4 };

        var first = Prepare(ctx, WhereProbe(e, probe));
        first.ParamRecipe.Should().NotBeNull();

        probe.Reads = 0; // isolate the cache-hit refresh
        var second = Prepare(ctx, WhereProbe(e, probe));
        ReferenceEquals(first, second).Should().BeTrue();
        probe.Reads.Should().Be(1, "the fast path reads the captured member exactly once");
    }

    // A small fixed slack absorbs runtime noise (GC bookkeeping, tiered-JIT call-site updates) without
    // masking a per-hit allocation, which would scale with the iteration count instead of staying fixed.
    private const int AllocationSlackBytes = 512;

    // A boxed int is one 24-byte allocation on x64: 8-byte object header + 8-byte method-table pointer
    // + 4-byte payload padded to 8 bytes.
    private const int BoxedIntBytes = 24;

    [Fact]
    public void FastPath_ShouldNotAllocateDictionaryOrCollector()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var probe = new CountingProbe();

        var firstCommand = WhereText(e, probe);
        var first = Prepare(ctx, firstCommand);
        first.ParamRecipe.Should().NotBeNull();
        var secondCommand = WhereText(e, probe);
        var second = Prepare(ctx, secondCommand);
        ReferenceEquals(first, second).Should().BeTrue();

        var recipe = first.ParamRecipe!;
        var dbParams = second.DbCommandParams;

        for (var i = 0; i < 100; i++)
            recipe.TryBind(secondCommand, dbParams);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            recipe.TryBind(secondCommand, dbParams);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The fast bind is allocation-free; the tiny fixed slack only absorbs runtime noise (GC
        // bookkeeping, tiered-JIT call-site updates) and is far below one per-hit allocation.
        allocated.Should().BeLessThanOrEqualTo(AllocationSlackBytes,
            "a positional fast bind allocates no dictionary/collector/value array");
    }

    [Fact]
    public void FastPath_ValueTypeParameter_ShouldBeBoxedAtMostOnce()
    {
        using var ctx = new RecipeTestContext();
        ctx.PurgeQueryCache();
        var e = ctx.From<RecipeEntity>();
        var probe = new CountingProbe { Value = 9 };

        var firstCommand = WhereProbe(e, probe);
        var first = Prepare(ctx, firstCommand);
        var secondCommand = WhereProbe(e, probe);
        var second = Prepare(ctx, secondCommand);
        ReferenceEquals(first, second).Should().BeTrue();

        var recipe = first.ParamRecipe!;
        var dbParams = second.DbCommandParams;

        for (var i = 0; i < 100; i++)
            recipe.TryBind(secondCommand, dbParams);

        const int iterations = 10_000;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            recipe.TryBind(secondCommand, dbParams);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // A boxed int is one 24-byte allocation on x64 (8-byte object header + 8-byte method-table
        // pointer + 4-byte payload padded to 8 bytes), so `iterations` fast binds allocate at most
        // iterations*24 plus the fixed noise slack; a dictionary/collector would exceed it by far.
        const int upperBound = iterations * BoxedIntBytes + AllocationSlackBytes;
        allocated.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(upperBound,
            "the typed writer boxes the value-type parameter exactly once per fast bind");
    }

    // ------------------------------------------------------------------------------------------------
    // Provider-free SQL context: runs the real planner without a database.
    // ------------------------------------------------------------------------------------------------

    private sealed class RecipeTestDialect : SqlDialectBase
    {
        internal static readonly RecipeTestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class RecipeTestContext : DataContext
    {
        private readonly FakeConnection _connection = new();

        public RecipeTestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => RecipeTestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new FakeParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString) => _connection;
    }

    private sealed class FakeParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand();
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter() => new FakeParameter(string.Empty);

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value!);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string? value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string? parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string? parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string? parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string? parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                _items[index] = value;
        }
    }
}
