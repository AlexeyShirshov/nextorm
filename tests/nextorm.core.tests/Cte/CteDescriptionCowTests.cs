using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;
using System.Linq.Expressions;
using System.Reflection;

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
        clone.Definition.Should().NotBeSameAs(original.Definition,
            "the definition memo is node-owned even while the raw description is shared");
        clone.Definition.Condition.Should().BeSameAs(original.Definition.Condition,
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

    /// <summary>
    /// Pins D-COW-1: paging is node-owned state, so each command in a fluent chain materialises its own
    /// <see cref="QueryDefinition"/> snapshot even while the chain shares one raw description. A memoised
    /// snapshot built for a sibling's limit/offset must never be served to this command.
    /// </summary>
    [Fact]
    public void Descriptions_IntermediateChain_EachDefinitionKeepsItsOwnPaging()
    {
        using var ctx = CreateSqlServerContext();

        var root = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var ordered = root.OrderBy(1);
        var limited = ordered.Limit(3);
        var paged = limited.Offset(1);

        // Reading a sibling's snapshot in between must not move another command's snapshot.
        ordered.Definition.Paging.Limit.Should().Be(0);
        limited.Definition.Paging.Limit.Should().Be(3);
        paged.Definition.Paging.Should().Match<Paging>(p => p.Limit == 3 && p.Offset == 1);
        limited.Definition.Paging.Offset.Should().Be(0);
        ordered.Definition.Paging.Limit.Should().Be(0,
            "the memo must not be shared across commands with different paging");

        // The memo keeps returning equal values for the same command, even after a sibling read.
        var first = limited.Definition;
        _ = paged.Definition;
        var second = limited.Definition;
        second.Paging.Limit.Should().Be(first.Paging.Limit);
        second.Paging.Offset.Should().Be(first.Paging.Offset);
        second.Should().Be(first, "a repeated read of the same command's definition is value-equal");
        ordered.Definition.Paging.Limit.Should().Be(0);
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
        clone.Definition.Should().NotBeSameAs(original.Definition,
            "the definition memo is node-owned even while the raw description is shared");
        clone.Definition.Exp.Should().BeSameAs(original.Definition.Exp,
            "the clone must share the original's raw description until it writes to it");

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
        foreignAnchor.Definition.Condition.Should().BeSameAs(rawForeignAnchor.Definition.Condition,
            "the anchor clone shares the raw description of the command it was cloned from");

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

    /// <summary>
    /// Pins that the live copy constructor and the plan-cache clone preserve the <c>JoinInto</c> pair
    /// projection type: the copy must keep the result type the parent command materialises.
    /// </summary>
    [Fact]
    public void Descriptions_JoinIntoPairClone_PreservesProjectionType()
    {
        var pair = CreateJoinIntoPairCommand();
        var projectionType = pair.ProjectionType;
        projectionType.Should().NotBeNull("a JoinInto pair command materialises a stitched projection");

        var clone = pair.Clone();
        clone.ProjectionType.Should().BeSameAs(projectionType,
            "the live copy constructor must preserve the pair projection type");
        clone.Definition.ProjectionType.Should().BeSameAs(projectionType);
        clone.GetType().Should().Be(pair.GetType(), "the clone keeps the pair command's result type");

        var cacheClone = pair.CloneForCache();
        cacheClone.ProjectionType.Should().BeSameAs(projectionType,
            "the plan-cache clone must preserve the pair projection type");
        cacheClone.Definition.ProjectionType.Should().BeSameAs(projectionType);
    }

    /// <summary>
    /// Pins that two commands sharing one raw description own independent definition memos: paging and
    /// node-owned raw state (navigation paths) never leak a sibling's snapshot, including under
    /// concurrent reads.
    /// </summary>
    [Fact]
    public async Task Descriptions_ConcurrentSiblings_KeepIndependentDefinitionMemos()
    {
        using var ctx = CreateSqlServerContext();

        var root = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var left = (QueryCommand<int>)root.Clone();
        var right = (QueryCommand<int>)root.Clone();
        left.Definition.Condition.Should().BeSameAs(right.Definition.Condition,
            "the siblings share one raw description");
        left.Paging.Limit = 4;
        right.Paging.Limit = 7;
        right.Paging.Offset = 2;

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
        {
            left.Definition.Paging.Should().Match<Paging>(p => p.Limit == 4 && p.Offset == 0);
            right.Definition.Paging.Should().Match<Paging>(p => p.Limit == 7 && p.Offset == 2);
            left.Definition.Should().NotBeSameAs(right.Definition,
                "each node owns its definition memo");
        })));

        root.Definition.Paging.Limit.Should().Be(0, "a sibling's memo must not move the root's snapshot");
        root.Definition.Paging.Offset.Should().Be(0);

        // A node-owned raw write invalidates only its own memo.
        var navigationParameter = Expression.Parameter(typeof(SimpleEntity), "root");
        var navigationPaths = new Dictionary<ParameterExpression, string> { [navigationParameter] = "Children" };
        left.NavigationPaths = navigationPaths;
        left.Definition.NavigationPaths.Should().BeSameAs(navigationPaths);
        right.Definition.NavigationPaths.Should().BeNull("the memo key covers node-owned navigation paths");
    }

    /// <summary>
    /// Pins the AC5 allocation fix: the node-owned <see cref="DefinitionMemo"/> is a value type stored in
    /// a nullable field, so the first definition read of a fresh Join4-shaped chain publishes the memo
    /// without a heap allocation, and a warm read keeps returning the same node-owned snapshot for zero
    /// bytes.
    /// </summary>
    [Fact]
    public void Descriptions_Join4DefinitionRead_MemoIsValueTypeAndWarmReadsAllocateNothing()
    {
        using var ctx = CreateSqlServerContext();

        var query = ctx.From<SimpleEntity>()
            .Join(ctx.From<SimpleEntity>(), (t1, t2) => t1.Id == t2.Id + 1)
            .Join(ctx.From<SimpleEntity>(), (p, t3) => p.Item2.Id == t3.Id - 1)
            .Join(ctx.From<SimpleEntity>(), (p, t4) => p.Item3.Id == t4.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id });

        typeof(DefinitionMemo).IsValueType.Should().BeTrue(
            "the node-owned memo must be a value type so a definition read allocates no memo object");

        var memoField = typeof(QueryCommand).GetField("_definitionMemo", BindingFlags.Instance | BindingFlags.NonPublic);
        memoField.Should().NotBeNull("the command owns the memo in a private field");
        memoField!.FieldType.Should().Be(typeof(DefinitionMemo?),
            "the memo is published through one nullable value-type field");

        var first = query.Definition;
        var second = query.Definition;
        second.Should().BeSameAs(first, "the warm memo serves the node-owned snapshot without rebuilding it");
        first.Joins.Should().HaveCount(3, "the Join4 shape carries three joins");

        for (var i = 0; i < 100; i++)
            _ = query.Definition;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            _ = query.Definition;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0, "a warm definition read on a Join4-shaped chain must not allocate");
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

        // The applied command owns a mutable join array (preparation replaces the elements in place), so
        // its plan-cache clone must own a deep copy: a later cache comparison must never observe that
        // array, and mutating the clone's array must not reach the applied command.
        var appliedJoins = secondCommand.Joins;
        appliedJoins.Should().NotBeNull("the recipe shape joins the CTE source");
        var appliedJoin = appliedJoins![0];
        var cacheClone = secondCommand.CloneForCache();
        cacheClone.Joins.Should().NotBeSameAs(appliedJoins,
            "the cache clone must not share the applied command's mutable join array");
        cacheClone.Joins![0] = null!;
        ReferenceEquals(secondCommand.Joins![0], appliedJoin).Should().BeTrue(
            "mutating the cache clone's join array must not reach the applied command");

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
            bodyA.Definition.Should().NotBeSameAs(bodyB.Definition,
                $"iteration {iteration}: the definition memo is node-owned, not published on the shared description");
            bodyA.Definition.Condition.Should().BeSameAs(bodyB.Definition.Condition,
                $"iteration {iteration}: both bodies still share the raw description");

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

        // Per-node mutable arrays where the baseline requires them: a live clone owns a private join
        // array (preparation writes prepared copies into it), while the elements stay shared until then;
        // the plan-cache key deep-clones both the array and its elements.
        var withJoin = ctx.From<SimpleEntity>()
            .Join(ctx.From<SimpleEntity>(), (p, c) => p.Id == c.Id)
            .Select(x => x.Item1.Id);
        var liveClone = (QueryCommand<int>)withJoin.Clone();
        liveClone.Joins.Should().NotBeSameAs(withJoin.Joins,
            "a live clone owns a private join array");
        liveClone.Joins![0].Should().BeSameAs(withJoin.Joins![0],
            "join elements stay shared until preparation replaces them");
        var cacheClone = withJoin.CloneForCache();
        cacheClone.Joins.Should().NotBeSameAs(withJoin.Joins,
            "the plan-cache key owns a private join array");
        cacheClone.Joins![0].Should().NotBeSameAs(withJoin.Joins![0],
            "the plan-cache key deep-clones the join elements");

        // The CTE declaration list is immutable raw state; the cache clone owns a deep copy of it.
        var cteCommand = ctx.With("cow_arrays", ctx.From<SimpleEntity>().Select(x => new { x.Id }))
            .From("cow_arrays")
            .Select(t => t["id"].AsInt);
        var cteCacheClone = cteCommand.CloneForCache();
        cteCacheClone.Ctes.Should().NotBeSameAs(cteCommand.Ctes,
            "the cache clone rebuilds the declaration list");
        cteCacheClone.Ctes![0].Should().NotBeSameAs(cteCommand.Ctes![0]);
        cteCacheClone.Ctes[0].Query.Should().NotBeSameAs(cteCommand.Ctes[0].Query,
            "the cache clone owns cloned CTE body commands");

        // The JoinInto pair projection type is raw state and must survive both clone kinds.
        var pair = CreateJoinIntoPairCommand();
        var projectionType = pair.ProjectionType;
        projectionType.Should().NotBeNull("a JoinInto pair command materialises a stitched projection");
        ((QueryCommand)pair.Clone()).ProjectionType.Should().BeSameAs(projectionType,
            "a live clone preserves the pair projection type");
        pair.CloneForCache().ProjectionType.Should().BeSameAs(projectionType,
            "the cache clone preserves the pair projection type");
    }

    private static QueryCommand<int> CteShape(IDataContext ctx)
    {
        var recent = ctx.From<SimpleEntity>().Where(c => c.Id > 1).Select(c => new { c.Id });
        return ctx.With("cow_recipe", recent)
            .From("cow_recipe")
            .Join(ctx.From<SimpleEntity>(), (r, s) => r["id"].AsInt == s.Id)
            .Select(p => p.Item1["id"].AsInt);
    }

    private static QueryCommand CreateJoinIntoPairCommand()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<MemoryJoinParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .WithData([new MemoryJoinParent { Id = 1 }]);
        return ctx.From<MemoryJoinParent>()
            .JoinInto(ctx.From<MemoryJoinChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .CreateJoinIntoPairCommand();
    }
}
