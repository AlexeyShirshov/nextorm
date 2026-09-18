using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlite.tests;

/// <summary>
/// Regression tests for the IN-list translation optimisations:
/// <list type="bullet">
/// <item>the value-extraction delegate is compiled once per collection shape and cached in
/// <see cref="DataContextCache.InValuesCache"/>, so repeated builds do not call <c>Compile()</c> again;</item>
/// <item>an inline <c>new[] { ... }</c> list stays cacheable and a structurally equal call site hits
/// the same plan cache entry.</item>
/// </list>
/// These never open a database connection: only the SQL build path (plan construction) is exercised.
/// </summary>
public class InListCacheTests
{
    private static readonly long[] CapturedValues = { 1, 3, 10 };

    private static QueryCommand<long> InlineAtInA(EntityBuilder<IComplexEntity> e)
        => e.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id);

    private static QueryCommand<long> InlineAtInB(EntityBuilder<IComplexEntity> e)
        => e.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id);

    private static QueryCommand<long> CapturedAtIn(EntityBuilder<IComplexEntity> e)
        => e.Where(c => NORM.SQL.@in(c.Id, CapturedValues)).Select(c => c.Id);

    private static QueryCommand<long> CapturedAtInB(EntityBuilder<IComplexEntity> e)
        => e.Where(c => NORM.SQL.@in(c.Id, CapturedValues)).Select(c => c.Id);

    private static Expression GetAtInValues(QueryCommand cmd)
    {
        var body = (MethodCallExpression)((LambdaExpression)cmd.Condition!).Body;
        return body.Arguments[1];
    }

    [Fact]
    public void In_CapturedCollection_SameLength_ShouldReuseCachedPlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        // Before the shape fold a captured collection disabled the plan cache entirely; now the
        // evaluated length/null shape is part of the key, so two same-shape builds share one plan.
        var first = ctx.GetPreparedQueryCommand(CapturedAtIn(e), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(CapturedAtInB(e), false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void In_InlineArray_StructurallyEqualCallSites_ShouldReuseCachedPlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = ctx.GetPreparedQueryCommand(InlineAtInA(e), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(InlineAtInB(e), false, true, CancellationToken.None);

        // Two different call sites with the same inline values must resolve to the same cached plan.
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void In_CapturedCollection_SameShape_ShouldReuseCompiledAccessor()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cmd1 = CapturedAtIn(e);
        ctx.GetPreparedQueryCommand(cmd1, false, false, CancellationToken.None);

        var cmd2 = CapturedAtIn(e);
        ctx.GetPreparedQueryCommand(cmd2, false, false, CancellationToken.None);

        var key1 = new ExpressionKey(GetAtInValues(cmd1), cmd1);
        var key2 = new ExpressionKey(GetAtInValues(cmd2), cmd2);

        DataContextCache.InValuesCache.TryGetValue(key1, out var accessor1).Should().BeTrue();
        DataContextCache.InValuesCache.TryGetValue(key2, out var accessor2).Should().BeTrue();

        // The shape was compiled once: both builds must observe the same delegate instance.
        ReferenceEquals(accessor1, accessor2).Should().BeTrue();
    }

    [Fact]
    public void In_CapturedArray_ReassignedBetweenBuilds_ShouldReadCurrentValues()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var values = new long[] { 1 };
        var first = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(c => NORM.SQL.@in(c.Id, values)).Select(c => c.Id), false, false, CancellationToken.None);

        first.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0");
        first.DbCommandParams[0].Value.Should().Be(1L);

        // The same expression tree (compiler-cached lambda) now reads the reassigned array: the cached
        // accessor must not freeze the values captured on the first build.
        values = new long[] { 2, 3 };

        var second = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(c => NORM.SQL.@in(c.Id, values)).Select(c => c.Id), false, false, CancellationToken.None);

        second.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0", "p1");
        second.DbCommandParams[0].Value.Should().Be(2L);
        second.DbCommandParams[1].Value.Should().Be(3L);
    }

    [Fact]
    public void In_CapturedList_GrownBetweenBuilds_ShouldReadCurrentValues()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var values = new List<long> { 1 };
        var first = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(c => values.Contains(c.Id)).Select(c => c.Id), false, false, CancellationToken.None);

        first.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0");

        values.Add(3);

        var second = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(c => values.Contains(c.Id)).Select(c => c.Id), false, false, CancellationToken.None);

        second.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0", "p1");
        second.DbCommandParams[1].Value.Should().Be(3L);
    }
}
