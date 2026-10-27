using System.Data.Common;
using FluentAssertions;
using NextORM.Postgres;

namespace NextORM.Core.Tests;

/// <summary>
/// T323 (#323) D4b: the plan-cache lifecycle of a <c>SelectGroup</c> command. The opt-in form renders
/// the explicit-grouping SQL, keeps its own plan key (distinct from the explicit and the plain
/// projection), survives <see cref="QueryCommand.ResetPreparation"/> with its derived grouping state
/// recomputed, keeps the flag and the reused plan through a clone, and never flips the sticky
/// <see cref="QueryCommand.Cache"/> policy of the context-shared command. Runs in the serialized
/// "Query cache controls" collection because it asserts plan-cache state. No database is opened: the
/// context carries a placeholder connection string and only preparation runs.
/// </summary>
[Collection("Query cache controls")]
public class T323SelectGroupCacheTests
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private static IDataContext CreateContext()
        => new PostgresDataContext(PlaceholderConnectionString, new DataContextBuilder());

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static void PreparePlan<T>(QueryCommand<T> cmd) => cmd.PrepareCommand(false, CancellationToken.None);

    /// <summary>A named projection so a clone keeps its generic parameter.</summary>
    private sealed class T323GroupRow
    {
        public int Id { get; set; }
        public int C { get; set; }
    }

    /// <summary>The constant-projection shape (no keys, no aggregates).</summary>
    private sealed class T323ConstantRow
    {
        public int V { get; set; }
    }

    /// <summary>The global-aggregate shape (no keys, one aggregate).</summary>
    private sealed class T323GlobalRow
    {
        public decimal S { get; set; }
    }

    [Fact]
    public void PlanKey_ShouldSeparateSelectGroupFromExplicitAndPlain()
    {
        using var ctx = CreateContext();

        // SQL parity runs on fresh instances for the reason recorded in D1: a command prepared first
        // through the no-hash path and then re-prepared does not recompute the cached part hashes, so
        // the plan-identity half below prepares each command exactly once.
        SqlOf(ctx, ctx.From<T323LegacyEntity>().SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Be(
                SqlOf(ctx, ctx.From<T323LegacyEntity>().GroupBy(x => x.Id).Select(x => new { x.Id, C = SqlFunctions.Sql.count() })),
                "the inferred form renders the explicit SQL");

        var plain = SqlOf(ctx, ctx.From<T323LegacyEntity>().Select(x => new { x.Id, C = SqlFunctions.Sql.count() }));
        plain.Should().NotContain("group by", "the plain projection stays ungrouped");

        var selectGroup = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var plainSelect = ctx.From<T323LegacyEntity>()
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var twin = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });

        PreparePlan(selectGroup);
        PreparePlan(explicitForm);
        PreparePlan(plainSelect);
        PreparePlan(twin);

        var comparer = selectGroup.GetQueryPlanEqualityComparer();
        comparer.Equals(selectGroup, explicitForm).Should().BeFalse(
            "a SelectGroup plan is not the explicit-grouping plan even though the SQL matches");
        comparer.Equals(selectGroup, plainSelect).Should().BeFalse(
            "a SelectGroup plan is not the plain projection plan");
        comparer.Equals(selectGroup, twin).Should().BeTrue("an identical rebuild must reuse the plan key");
        comparer.GetHashCode(selectGroup).Should().Be(comparer.GetHashCode(twin), "equal plans must have equal hashes");
    }

    [Fact]
    public void ResetPreparation_ShouldReprepareTheSameGrouping()
    {
        using var ctx = CreateContext();

        var command = ctx.From<T323LegacyEntity>().SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var before = SqlOf(ctx, command);
        before.Should().Contain("group by");

        command.GroupingList.Should().NotBeNull("preparation materialises the inferred keys");
        var countBefore = command.GroupingList!.Length;
        countBefore.Should().Be(1, "one inferred key produces one grouping column");
        command.SelectGroupMode.Should().Be(SelectGroupMode.Keyed);

        command.ResetPreparation();

        command.GroupingList.Should().BeNull("the reset discards the derived grouping state");
        command.SelectGroupMode.Should().Be(SelectGroupMode.None, "the reset discards the derived mode");

        var after = SqlOf(ctx, command);

        after.Should().Be(before, "re-preparation recomputes the same grouping");
        command.GroupingList.Should().NotBeNull("the derived state is recomputed, not lost");
        command.GroupingList!.Length.Should().Be(countBefore);
        command.SelectGroupMode.Should().Be(SelectGroupMode.Keyed);
        command.Cache.Should().BeTrue("a reset keeps the opt-in policy of the command");
    }

    [Fact]
    public void ResetPreparation_ShouldRecomputeGlobalAndConstantModes()
    {
        using var ctx = CreateContext();

        var global = ctx.From<T323LegacyEntity>().SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) });
        var globalBefore = SqlOf(ctx, global);
        globalBefore.Should().Contain("sum(");
        globalBefore.Should().NotContain("group by", "a global aggregate has no grouping key");
        global.SelectGroupMode.Should().Be(SelectGroupMode.GlobalAggregate);

        global.ResetPreparation();
        global.SelectGroupMode.Should().Be(SelectGroupMode.None);
        global.GroupingList.Should().BeNull();

        var globalAfter = SqlOf(ctx, global);
        globalAfter.Should().Be(globalBefore, "the global-aggregate heading shape survives a reset");
        global.GroupingList.Should().BeEmpty("a global aggregate has no grouping columns");
        global.SelectGroupMode.Should().Be(SelectGroupMode.GlobalAggregate);

        var constant = ctx.From<T323LegacyEntity>().SelectGroup(x => new { V = 1 });
        var constantBefore = SqlOf(ctx, constant);
        constantBefore.TrimStart().StartsWith("select distinct", StringComparison.OrdinalIgnoreCase)
            .Should().BeTrue($"a key-less constant projection renders DISTINCT, got: {constantBefore}");
        constantBefore.Should().NotContain("group by");
        constant.SelectGroupMode.Should().Be(SelectGroupMode.ConstantProjection);

        constant.ResetPreparation();
        constant.SelectGroupMode.Should().Be(SelectGroupMode.None);

        var constantAfter = SqlOf(ctx, constant);
        constantAfter.Should().Be(constantBefore, "the constant-projection heading shape survives a reset");
        constant.SelectGroupMode.Should().Be(SelectGroupMode.ConstantProjection);
    }

    [Fact]
    public void Clone_ShouldKeepTheOptInFlagAndReprepare()
    {
        using var ctx = CreateContext();

        var command = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new T323GroupRow { Id = x.Id, C = SqlFunctions.Sql.count() });
        var original = SqlOf(ctx, command);
        original.Should().Contain("group by");

        var clone = (QueryCommand<T323GroupRow>)command.Clone();

        clone.Should().NotBeSameAs(command);
        clone.IsSelectGroup.Should().BeTrue("the opt-in flag travels with the clone");
        clone.Cache.Should().BeTrue("the clone keeps the opt-in policy");

        var cloned = SqlOf(ctx, clone);

        cloned.Should().Be(original, "the clone prepares to the same statement");
        clone.Cache.Should().BeTrue("preparing the clone must not flip the sticky cache policy");
        command.Cache.Should().BeTrue("the original keeps its policy too");
        cloned.Should().Contain("group by", "the cloned grouping list keeps the key");

        // The two key-less modes are rendered from the derived mode, so a clone that lost it would
        // silently change the result set (no DISTINCT) or drop the global HAVING filter.
        var constant = ctx.From<T323LegacyEntity>().SelectGroup(x => new T323ConstantRow { V = 1 });
        var constantOriginal = SqlOf(ctx, constant);
        constantOriginal.TrimStart().StartsWith("select distinct", StringComparison.OrdinalIgnoreCase)
            .Should().BeTrue($"the original constant projection is DISTINCT-shaped, got: {constantOriginal}");

        var constantClone = (QueryCommand<T323ConstantRow>)constant.Clone();
        SqlOf(ctx, constantClone).Should().Be(constantOriginal,
            "a cloned constant projection must keep its DISTINCT heading");

        var global = ctx.From<T323LegacyEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0)
            .SelectGroup(x => new T323GlobalRow { S = SqlFunctions.Sql.sum(x.Amount) });
        var globalOriginal = SqlOf(ctx, global);
        globalOriginal.Should().Contain(" having ", "the original global aggregate is HAVING-shaped");

        var globalClone = (QueryCommand<T323GlobalRow>)global.Clone();
        SqlOf(ctx, globalClone).Should().Be(globalOriginal,
            "a cloned global aggregate must keep its HAVING filter");

        // The internal half of the same contract: the renderer reads the mode directly, so the clone
        // carries the derived mode instead of relying on a re-preparation it never performs.
        clone.SelectGroupMode.Should().Be(SelectGroupMode.Keyed, "the derived mode travels with the clone");
        constantClone.SelectGroupMode.Should().Be(SelectGroupMode.ConstantProjection);
        globalClone.SelectGroupMode.Should().Be(SelectGroupMode.GlobalAggregate);
    }

    [Fact]
    public void SelectGroup_ShouldNotDisableTheSharedPlanCache()
    {
        using var ctx = CreateContext();

        // The context-shared command Any() reuses; its sticky Cache=false would silently disable the
        // plan cache for the whole context, so it is asserted before and after every SelectGroup prepare.
        var shared = EntityBuilderExtensions.GetAnyCommand(ctx, ctx.From<T323LegacyEntity>().ToCommand());
        ctx.AnyCommand.Should().NotBeNull("the Any route allocates the context-shared command");
        ctx.AnyCommand!.Value.Should().BeSameAs(shared);
        shared.Cache.Should().BeTrue("the shared Any command starts cacheable");

        var command = ctx.From<T323LegacyEntity>().SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        SqlOf(ctx, command).Should().Contain("group by");
        command.Cache.Should().BeTrue("preparing a SelectGroup command must not disable its own plan cache");

        // The explicit no-store overload is the one the rendering helper uses: a call-local decision,
        // never a mutation of the command's policy.
        ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);
        command.Cache.Should().BeTrue("a no-store preparation must not flip the sticky Cache flag");

        ctx.GetPreparedQueryCommand(shared, false, false, CancellationToken.None).Should().NotBeNull();
        shared.Cache.Should().BeTrue(
            "preparing a SelectGroup command must not set the sticky Cache=false flag on the shared Any command");
        ctx.AnyCommand!.Value.Should().BeSameAs(shared, "the shared command stays the same instance");
    }

    [Fact]
    public void DifferentCapturedValues_ShouldShareOnePlanAndRebind()
    {
        using var ctx = CreateContext();

        QueryCommand<T323GroupRow> Build(decimal threshold)
            => ctx.From<T323LegacyEntity>()
                .Where(x => x.Amount > threshold)
                .SelectGroup(x => new T323GroupRow { Id = x.Id, C = SqlFunctions.Sql.count() });

        var lowerSql = SqlOf(ctx, Build(1m));
        lowerSql.Should().Contain("group by");
        lowerSql.Should().Be(SqlOf(ctx, Build(10m)), "a captured value never changes the rendered SQL");

        // Plan identity is asserted on commands prepared exactly once (see the note in the first fact).
        var lowerPlan = Build(1m);
        var upperPlan = Build(10m);
        PreparePlan(lowerPlan);
        PreparePlan(upperPlan);

        var comparer = lowerPlan.GetQueryPlanEqualityComparer();
        comparer.Equals(lowerPlan, upperPlan).Should().BeTrue("the captured threshold is a parameter, not part of the plan");
        comparer.GetHashCode(lowerPlan).Should().Be(comparer.GetHashCode(upperPlan), "equal plans must have equal hashes");

        var lowerParameters = Prepare(ctx, Build(1m)).DbCommandParams.Cast<DbParameter>().ToArray();
        var upperParameters = Prepare(ctx, Build(10m)).DbCommandParams.Cast<DbParameter>().ToArray();

        lowerParameters.Should().ContainSingle();
        upperParameters.Should().ContainSingle();
        lowerParameters[0].ParameterName.Should().Be(upperParameters[0].ParameterName);
        lowerParameters[0].Value.Should().Be(1m);
        upperParameters[0].Value.Should().Be(10m);
    }
}
