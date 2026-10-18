using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T297 P4 behavioural proof for the compact shared description and its copy-on-write boundaries:
/// a clone/derivation shares raw description blocks but owns its preparation and binding state, so a
/// mutation, reset or concurrent preparation on one node can never be observed by the other.
/// </summary>
public class CteDescriptionCowTests
{
    private const string SqlServerPlaceholder =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private sealed class CowTypedCteRow
    {
        public int Id { get; set; }
    }

    private static SqlServerDataContext CreateSqlServerContext()
        => new(SqlServerPlaceholder, new DataContextBuilder());

    private static string PrepareSql<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache = true)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> PrepareCommand<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache = true)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static void ResetRecipeCache()
    {
        QueryPlanStore.Clear();
        QueryPlanner.ResetCounters();
    }

    /// <summary>Pins that mutating a clone's raw description (DISTINCT) splits it and leaves the original SQL, flags and plan hash untouched.</summary>
    [Fact]
    public void Descriptions_CloneMutation_DoesNotLeakIntoOriginal_AndHashesDiffer()
    {
        using var ctx = CreateSqlServerContext();

        var original = ctx.From<SimpleEntity>().Where(x => x.Id > 4).Select(x => x.Id);
        var originalCondition = original.Condition;
        var originalSql = PrepareSql(ctx, original, storeInCache: false);

        var clone = (QueryCommand<int>)original.Clone();
        ReferenceEquals(original.Definition, clone.Definition).Should().BeTrue(
            "a clone shares the raw description until it writes to it");
        clone.IsDistinct = true;
        clone.ResetPreparation();

        var cloneSql = PrepareSql(ctx, clone, storeInCache: false);

        original.IsDistinct.Should().BeFalse("the clone's copy-on-write flag must not reach the shared description");
        clone.IsDistinct.Should().BeTrue();
        original.Condition.Should().BeSameAs(originalCondition, "the shared predicate block must not be rewritten");
        PrepareSql(ctx, original, storeInCache: false).Should().Be(originalSql,
            "preparing the mutated clone must not change the original's rendered SQL");
        cloneSql.Should().ContainEquivalentOf("distinct");
        cloneSql.Should().NotBe(originalSql);

        var comparer = original.GetQueryPlanEqualityComparer();
        comparer.Equals(original, clone).Should().BeFalse("DISTINCT is part of the plan key");
        comparer.GetHashCode(original).Should().NotBe(comparer.GetHashCode(clone),
            "the two nodes are different plans");
    }

    /// <summary>Pins that a prepared command keeps its own preparation state when a clone (sharing its description) is mutated and reset.</summary>
    [Fact]
    public void Sharing_CloneMutationAndReset_LeavePreparedOriginalWarm()
    {
        using var ctx = CreateSqlServerContext();

        var original = ctx.With("cow_warm", ctx.From<SimpleEntity>().Where(x => x.Id > 3).Select(x => new { x.Id }))
            .From("cow_warm")
            .Select(t => t["id"].AsInt);
        original.PrepareCommand(false, CancellationToken.None);
        original.IsPrepared.Should().BeTrue();

        var selectList = original.SelectList;
        var ctes = original.Ctes;
        var columnsHash = original.ColumnsPlanHash;
        var whereHash = original.WherePlanHash;
        var fromHash = original.FromPlanHash;
        var ctesHash = original.CtesPlanHash;
        var resultHash = original.ResultPlanHash;

        var clone = (QueryCommand<int>)original.Clone();
        ReferenceEquals(original.Definition, clone.Definition).Should().BeTrue(
            "the clone must share the original's description until it writes to it");

        clone.IsDistinct = true;
        clone.ResetPreparation();

        clone.IsPrepared.Should().BeFalse("the clone owns its preparation state");
        original.IsPrepared.Should().BeTrue("resetting the clone must leave the original warm");
        original.SelectList.Should().BeSameAs(selectList);
        original.Ctes.Should().BeSameAs(ctes);
        original.ColumnsPlanHash.Should().Be(columnsHash);
        original.WherePlanHash.Should().Be(whereHash);
        original.FromPlanHash.Should().Be(fromHash);
        original.CtesPlanHash.Should().Be(ctesHash);
        original.ResultPlanHash.Should().Be(resultHash);
    }

    /// <summary>Pins that a CTE whose body carries a CTE hoists to dependency-first declarations and renders the established flat SQL.</summary>
    [Fact]
    public void Descriptions_NestedCteGraph_MatchesFlatRenderingAndKeepsInnerDeclaration()
    {
        using var ctx = CreateSqlServerContext();

        var inner = ctx.From<SimpleEntity>().Where(x => x.Id > 7).Select(x => new { x.Id });
        var nestedBody = ctx.With("cow_inner", inner)
            .From("cow_inner")
            .Select(t => new { id = t["id"].AsInt });
        var nestedRoot = ctx.With("cow_outer", nestedBody)
            .From("cow_outer")
            .Select(t => new { id = t["id"].AsInt });

        var innerDeclaration = nestedBody.Ctes.Should().ContainSingle().Which;
        var hoisted = CteHoister.Hoist(nestedRoot.Ctes);
        hoisted.Should().NotBeNull();
        hoisted!.Select(c => c.Name).Should().Equal(["cow_inner", "cow_outer"],
            "a nested declaration is hoisted before its consumer");
        hoisted[0].Should().BeSameAs(innerDeclaration,
            "the inner declaration stays reachable through both the nested body and the hoisted root list");

        // Differential oracle: the nested construction must render exactly like the equivalent flat
        // declaration chain, which is the pre-change rendering contract for this graph.
        var flatInner = ctx.From<SimpleEntity>().Where(x => x.Id > 7).Select(x => new { x.Id });
        var flatBody = ctx.From("cow_inner").Select(t => new { id = t["id"].AsInt });
        var flatRoot = ctx.With("cow_inner", flatInner)
            .With("cow_outer", flatBody)
            .From("cow_outer")
            .Select(t => new { id = t["id"].AsInt });

        var nestedSql = PrepareSql(ctx, nestedRoot, storeInCache: false);
        var flatSql = PrepareSql(ctx, flatRoot, storeInCache: false);

        nestedSql.Should().Be(flatSql, "hoisting a nested CTE graph must not change the rendered SQL");
        nestedSql.IndexOf("cow_inner", StringComparison.Ordinal)
            .Should().BeLessThan(nestedSql.IndexOf("cow_outer", StringComparison.Ordinal));
    }

    /// <summary>Pins that recursive owner rejections still fire during construction, with shared-description anchors, before any command is prepared.</summary>
    [Fact]
    public void Sharing_RecursiveOwnerRejections_ThrowBeforeAnyCommand()
    {
        using var ctx = CreateSqlServerContext();

        // Foreign reference in the step: the inner definition reuses the outer name. The anchor is a
        // clone, so its description is shared with the raw command whose bind state must stay untouched.
        var rawForeignAnchor = ctx.From<CowTypedCteRow>().Where(x => x.Id == 1).Select(x => x.Id);
        var foreignAnchor = (QueryCommand<int>)rawForeignAnchor.Clone();
        ReferenceEquals(rawForeignAnchor.Definition, foreignAnchor.Definition).Should().BeTrue();

        var foreignAct = () => foreignAnchor.AsRecursiveCte("cow_nums", outer =>
        {
            var innerAnchorBase = ctx.From<CowTypedCteRow>().Where(x => x.Id == 1).Select(x => x.Id);
            var innerAnchor = (QueryCommand<int>)innerAnchorBase.Clone();
            var innerStepBase = ctx.From(outer).Where(n => n < 3).Select(n => n + 1);
            var inner = innerAnchor.AsRecursiveCte("cow_nums", _ => (QueryCommand<int>)innerStepBase.Clone());
            return ctx.From(inner).Select(n => n);
        });

        foreignAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*in its step*different recursive definition*");
        foreignAnchor.IsPrepared.Should().BeFalse("the rejection precedes preparation");
        rawForeignAnchor.IsPrepared.Should().BeFalse();
        rawForeignAnchor.TypedRecursiveAnchor.Should().BeNull(
            "the recursive binding is node-owned and must not leak through the shared description");

        // Anchor self-access: the inner definition's anchor reads the outer self-reference. The anchor
        // itself is a clone (shared description) again.
        var anchorAct = () => ctx.From<CowTypedCteRow>().Where(x => x.Id == 1).Select(x => x.Id)
            .AsRecursiveCte("cow_outer", outer =>
            {
                var anchorBase = ctx.From(outer).Where(n => n < 3).Select(n => n + 1);
                var sharedAnchor = (QueryCommand<int>)anchorBase.Clone();
                return ctx.From(sharedAnchor.AsRecursiveCte("cow_inner", _ =>
                    ctx.From<CowTypedCteRow>().Where(x => x.Id == 1).Select(x => x.Id))).Select(n => n);
            });

        anchorAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*in its anchor*different recursive definition*");
    }

    /// <summary>Pins that a CTE graph publishes one raw-shape recipe, a second fresh command hits it exactly once, and the apply is differential-equal to ordinary preparation.</summary>
    [Fact]
    public void Descriptions_RawShapeRecipe_MissThenSingleHitApply()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, CteShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        QueryPlanner.RecipeReuses.Should().Be(0);

        var secondCommand = CteShape(ctx);
        var second = PrepareSql(ctx, secondCommand);
        QueryPlanner.RecipeReuses.Should().Be(1, "the fresh equivalent CTE command applies the captured preparation");
        QueryPlanner.RecipeApplyMismatches.Should().Be(0);
        second.Should().Be(first);
        secondCommand.IsPrepared.Should().BeTrue();

        // Single apply: the same already-prepared command goes through the warm plan path, never through
        // another recipe apply (the planner counter would otherwise move to 2).
        PrepareCommand(ctx, secondCommand);
        QueryPlanner.RecipeReuses.Should().Be(1,
            "a recipe is applied once per fresh command; the next call is the prepared-command path");

        // Differential against ordinary preparation of the same graph (no recipe, no store).
        using var fresh = CreateSqlServerContext();
        var ordinary = PrepareSql(fresh, CteShape(fresh), storeInCache: false);
        ordinary.Should().Be(second, "the recipe-applied SQL must equal ordinary preparation");
    }

    /// <summary>Pins that two commands built from clones of one shared description prepare concurrently into identical SQL with isolated preparation state.</summary>
    [Fact]
    public async Task Sharing_ConcurrentConstruction_IsolatesPreparationState()
    {
        using var ctx = CreateSqlServerContext();

        for (var iteration = 0; iteration < 50; iteration++)
        {
            var body = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });
            var bodyA = body.Clone();
            var bodyB = body.Clone();
            ReferenceEquals(bodyA.Definition, bodyB.Definition).Should().BeTrue(
                $"iteration {iteration}: both bodies must share one description");

            var rootA = ctx.With("cow_concurrent", bodyA).From("cow_concurrent").Select(t => t["id"].AsInt);
            var rootB = ctx.With("cow_concurrent", bodyB).From("cow_concurrent").Select(t => t["id"].AsInt);

            var sqlA = string.Empty;
            var sqlB = string.Empty;
            await Task.WhenAll(
                Task.Run(() => sqlA = PrepareSql(ctx, rootA, storeInCache: false)),
                Task.Run(() => sqlB = PrepareSql(ctx, rootB, storeInCache: false)));

            sqlA.Should().Be(sqlB, $"iteration {iteration}: the shared description renders one SQL");
            rootA.IsPrepared.Should().BeTrue($"iteration {iteration}: the first command prepares");
            rootB.IsPrepared.Should().BeTrue($"iteration {iteration}: the second command prepares");
            rootA.SelectList.Should().NotBeNull();
            rootB.SelectList.Should().NotBeNull();
            ReferenceEquals(rootA.SelectList, rootB.SelectList).Should().BeFalse(
                $"iteration {iteration}: preparation state is node-owned, not shared");
            ReferenceEquals(bodyA.SelectList, bodyB.SelectList).Should().BeFalse(
                $"iteration {iteration}: neither body observes the other's prepared select list");
        }
    }

    private static QueryCommand<int> CteShape(IDataContext ctx)
    {
        var recent = ctx.From<SimpleEntity>().Where(c => c.Id > 1).Select(c => new { c.Id });
        return ctx.With("cow_recipe", recent)
            .From("cow_recipe")
            .Join(ctx.From<SimpleEntity>(), (r, s) => r["id"].AsInt == s.Id)
            .Select(p => p.Item1["id"].AsInt);
    }
}
