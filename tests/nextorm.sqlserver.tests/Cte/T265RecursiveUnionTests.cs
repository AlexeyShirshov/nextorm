using System.Collections;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>Row shape of the recursive CTE used by the T265 tests.</summary>
public sealed class T265CteRow
{
    public int n { get; set; }
}

/// <summary>
/// #265: a legacy <c>WithRecursive</c> declaration whose body is a distinct <c>UNION</c> is rejected by
/// SQL Server, so preparation must fail closed with an actionable <see cref="NotSupportedException"/>
/// before execution and before the plan cache. The typed <c>AsRecursiveCte</c> API, legacy
/// <c>UnionAll</c>, a non-recursive distinct <c>UNION</c> and a non-union recursive declaration keep
/// their existing behavior. No database is opened: the placeholder connection is never used.
/// </summary>
public class T265RecursiveUnionTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static QueryCommand<T265CteRow> BuildLegacy(IDataContext ctx, bool distinct, int? maxRecursion = null)
    {
        var e = ctx.From<ISimpleEntity>();
        var anchor = e.Where(s => s.Id == 1).Select(s => new T265CteRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new T265CteRow { n = t["n"].AsInt + 1 });
        var body = distinct ? anchor.Union(step) : anchor.UnionAll(step);
        return ctx.WithRecursive("nums", body, maxRecursion).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });
    }

    /// <summary>
    /// Number of entries in the thread-local plan cache. The store is internal and the test assembly has
    /// <c>InternalsVisibleTo</c>; reading the private field is a test-only observation, no production
    /// cache API is added.
    /// </summary>
    private static int PlanCacheEntryCount()
    {
        var field = typeof(QueryPlanStore).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        return (field?.GetValue(null) as ICollection)?.Count ?? 0;
    }

    [Fact]
    public void LegacyDistinct_ThrowsActionableBeforeCaching()
    {
        using var ctx = SqlServerTestContext.Create();
        var before = PlanCacheEntryCount();

        var exception = FluentActions.Invoking(() => SqlOf(ctx, BuildLegacy(ctx, distinct: true)))
            .Should().Throw<NotSupportedException>().Which;

        exception.Message.Should().Contain("SQL Server").And.Contain("UnionAll").And.Contain("AsRecursiveCte");
        PlanCacheEntryCount().Should().Be(before);
    }

    [Fact]
    public void LegacyDistinct_RepeatedPrepareDoesNotPopulateCache()
    {
        using var ctx = SqlServerTestContext.Create();
        var before = PlanCacheEntryCount();

        var first = Record.Exception(() => SqlOf(ctx, BuildLegacy(ctx, distinct: true)));
        var second = Record.Exception(() => SqlOf(ctx, BuildLegacy(ctx, distinct: true)));

        first.Should().BeOfType<NotSupportedException>();
        second.Should().BeOfType<NotSupportedException>();
        second!.Message.Should().Be(first!.Message);

        // The first rejection happens while the CTE is prepared, i.e. before the planner looks the plan
        // up and before it stores one; a second, equivalent preparation is rejected identically.
        PlanCacheEntryCount().Should().Be(before);
    }

    [Fact]
    public void LegacyDistinct_WarmCacheCannotBypassValidation()
    {
        using var ctx = SqlServerTestContext.Create();

        var validSql = SqlOf(ctx, BuildLegacy(ctx, distinct: false));
        validSql.Should().Contain(" union all ");

        FluentActions.Invoking(() => SqlOf(ctx, BuildLegacy(ctx, distinct: true)))
            .Should().Throw<NotSupportedException>().WithMessage("*UNION (distinct)*");

        SqlOf(ctx, BuildLegacy(ctx, distinct: false)).Should().Be(validSql);
    }

    [Fact]
    public void LegacyAll_RemainsSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, BuildLegacy(ctx, distinct: false));

        sql.Should().StartWith("with nums as (");
        sql.Should().Contain(" union all ");
    }

    [Fact]
    public void LegacyWithoutUnion_DoesNotHitDistinctGuard()
    {
        using var ctx = SqlServerTestContext.Create();

        var body = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new T265CteRow { n = t["n"].AsInt + 1 });
        var cmd = ctx.WithRecursive("nums", body).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });

        var sql = string.Empty;
        FluentActions.Invoking(() => sql = SqlOf(ctx, cmd)).Should().NotThrow();
        sql.Should().StartWith("with nums as (");
    }

    [Fact]
    public void TypedRecursive_RemainsUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        var nums = ctx.From<ISimpleEntity>()
            .Where(s => s.Id == 1)
            .Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));
        var sql = SqlOf(ctx, ctx.From(nums).Select(n => n));
        sql.Should().Contain("union all").And.NotContain("maxrecursion");

        var limited = ctx.From<ISimpleEntity>()
            .Where(s => s.Id == 1)
            .Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1), 10);
        var limitedSql = SqlOf(ctx, ctx.From(limited).Select(n => n));
        limitedSql.Should().Contain("union all").And.Contain("option (maxrecursion 10)");
    }

    [Fact]
    public void MutationLegacyDistinct_UsesSharedValidationOrExistingProviderGate()
    {
        using var ctx = SqlServerTestContext.Create();

        // The observed rejection path on SQL Server is the existing provider gate, not the new guard:
        // MutationCteQuery.Create -> EnsureSupported (MutationCteQuery.cs) throws while the data-modifying
        // scope is built, so WithRecursive can never be attached and PrepareCtes is never reached. The
        // ordering fact (gate strictly before preparation) is recorded in the cycle status file.
        var act = () => ctx.With("ins", ctx.CreateInsertBuilder<ISimpleEntity>()
            .Value(x => x.Id, 1)
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }

    [Fact]
    public void NonrecursiveDistinct_RemainsSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var left = ctx.From<ISimpleEntity>().Where(s => s.Id == 1).Select(s => new T265CteRow { n = s.Id });
        var right = ctx.From<ISimpleEntity>().Where(s => s.Id == 2).Select(s => new T265CteRow { n = s.Id });
        var cmd = ctx.With("nums", left.Union(right)).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });

        var sql = SqlOf(ctx, cmd);

        sql.Should().Contain(" union ").And.NotContain("union all");
    }
}
