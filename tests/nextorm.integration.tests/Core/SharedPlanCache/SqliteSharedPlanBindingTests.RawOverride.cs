using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedRawOverride_BindsTheParameterObjectOfTheCurrentCall(bool prepare)
    {
        var sql = $"select Id from {TableName} where Id = @Target";
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));

        Execute(first, 1).Should().Equal(1);
        firstTap.LastParameters.Should().Equal((0, "Target", 1));
        var plan = FindPlan();
        var rows = Execute(second, 2);
        secondTap.LastParameters.Should().Equal(new[] { (0, "Target", 2) }, "each raw statement must bind its own parameter object");
        rows.Should().Equal(2);
        FindPlan().Should().BeSameAs(plan);
        plan.ParamSeeds.Single().Value.Should().BeNull();
        plan.DbCommandParams[0].Value.Should().Be(DBNull.Value);

        DbPreparedQueryCommand<int> FindPlan()
            => QueryPlanStore.Values.OfType<DbPreparedQueryCommand<int>>().Should().ContainSingle(plan => plan.CommandText == sql).Which;

        List<int> Execute(SqliteDataContext context, int target)
        {
            var parameters = new { Target = target };
            var query = context.From<BindingRow>().Select(row => row.Id);
            return prepare
                ? query.PrepareFromSql(sql, parameters, PrepareFromSqlMode.StoreInCache, TestContext.Current.CancellationToken).ToList(context)
                : query.WithSql(sql, parameters).ToList();
        }
    }

    [Fact]
    public void CachedRawOverride_SeparatesOrderedLayoutsAndPreservesPositionalValues()
    {
        var sql = $"select Id from {TableName} where Id = @Target and Id <= @norm_p0";
        var tap = new BindingTap();
        using var connection = OpenSeededConnection();
        using var context = new SqliteDataContext(connection, new DataContextBuilder().AddInterceptor(tap));
        var query = context.From<BindingRow>().Select(row => row.Id);
        var token = TestContext.Current.CancellationToken;

        var first = query.PrepareFromSql(sql, new { Target = 2, norm_p0 = 0 }, PrepareFromSqlMode.StoreInCache, token);
        var reversed = query.PrepareFromSql(sql, new { norm_p0 = 0, Target = 1 }, PrepareFromSqlMode.StoreInCache, token);
        var warm = query.PrepareFromSql(sql, new { Target = 1, norm_p0 = 0 }, PrepareFromSqlMode.StoreInCache, token);
        var uncached = query.PrepareFromSql(sql, new { Target = 2, norm_p0 = 0 }, PrepareFromSqlMode.None, token);

        var firstPlan = BoundQueryInvocation<int>.GetPlan(first);
        BoundQueryInvocation<int>.GetPlan(warm).Should().BeSameAs(firstPlan);
        BoundQueryInvocation<int>.GetPlan(reversed).Should().NotBeSameAs(firstPlan);
        QueryPlanStore.Values.Should().NotContain(BoundQueryInvocation<int>.GetPlan(uncached)!);

        context.ToList(first, [2]).Should().Equal(2);
        tap.LastParameters.Should().Equal((0, "Target", 2), (1, "norm_p0", 2));
        context.ToList(warm, [1]).Should().Equal(1);
        tap.LastParameters.Should().Equal((0, "Target", 1), (1, "norm_p0", 1));
        context.ToList(reversed, [1]).Should().Equal(1);
        tap.LastParameters.Should().Equal((0, "norm_p0", 1), (1, "Target", 1));
        context.ToList(uncached, [2]).Should().Equal(2);
        tap.LastParameters.Should().Equal((0, "Target", 2), (1, "norm_p0", 2));
    }
}
