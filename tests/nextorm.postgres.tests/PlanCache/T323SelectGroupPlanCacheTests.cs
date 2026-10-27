using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T323 (#323) D1: plan identity of a <c>SelectGroup</c> command. The inferred form renders the same
/// SQL as the explicit <c>GroupBy</c> form, but it must keep its own plan key (its grouping columns are
/// synthesised at preparation), an identical rebuild must reuse that key, and a captured value must
/// rebind through the parameter without changing the SQL. No database is opened: the context carries a
/// placeholder connection.
/// </summary>
public class T323SelectGroupPlanCacheTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static void PreparePlan<T>(QueryCommand<T> cmd) => cmd.PrepareCommand(false, CancellationToken.None);

    private sealed class T323ThresholdRow
    {
        public int Id { get; set; }
        public decimal S { get; set; }
    }

    /// <summary>
    /// Builds the same command shape around a different captured threshold. The threshold stays a
    /// captured parameter (never a literal), so the two commands differ only in the bound value.
    /// </summary>
    private static QueryCommand<T323ThresholdRow> BuildWithThreshold(IDataContext ctx, decimal threshold)
        => ctx.From<T323LegacyEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .SelectGroup(x => new T323ThresholdRow { Id = x.Id, S = SqlFunctions.Sql.sum(x.Amount) });

    [Fact]
    public void SelectGroupPlan_ShouldBeDistinctFromExplicitAndPlain()
    {
        using var ctx = PostgresTestContext.Create();

        // SQL parity runs on its own instances: a command first prepared without hashes (the
        // SQL-generation path) then re-prepared does not recompute the cached part hashes, so the
        // plan-identity half below uses fresh commands prepared exactly once.
        SqlOf(ctx, ctx.From<T323LegacyEntity>().SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Be(
                SqlOf(ctx, ctx.From<T323LegacyEntity>().GroupBy(x => x.Id).Select(x => new { x.Id, C = SqlFunctions.Sql.count() })),
                "the inferred form renders the explicit SQL");

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
        comparer.GetHashCode(selectGroup).Should().Be(comparer.GetHashCode(twin));
    }

    [Fact]
    public void SelectGroupPlan_ShouldReuseItsOwnKey()
    {
        using var ctx = PostgresTestContext.Create();

        var first = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount) });
        var second = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount) });

        PreparePlan(first);
        PreparePlan(second);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeTrue("two independently built identical commands share one plan");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second), "equal plans must have equal hashes");
    }

    [Fact]
    public void CapturedValue_ShouldRebindWithoutChangingSql()
    {
        using var ctx = PostgresTestContext.Create();

        var lower = BuildWithThreshold(ctx, 1m);
        var upper = BuildWithThreshold(ctx, 10m);

        var lowerSql = SqlOf(ctx, lower);
        lowerSql.Should().Be(SqlOf(ctx, upper), "a captured value never changes the rendered SQL");
        lowerSql.Should().Contain("having");

        var lowerParameters = Prepare(ctx, lower).DbCommandParams.Cast<DbParameter>().ToArray();
        var upperParameters = Prepare(ctx, upper).DbCommandParams.Cast<DbParameter>().ToArray();

        lowerParameters.Should().ContainSingle();
        upperParameters.Should().ContainSingle();
        lowerParameters[0].ParameterName.Should().Be(upperParameters[0].ParameterName);
        lowerParameters[0].Value.Should().Be(1m);
        upperParameters[0].Value.Should().Be(10m);
        lowerSql.Should().NotContain("= 1", "the threshold is bound, never inlined");
    }
}
