using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

public class RuntimeSqlShapeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapturedWindowKeys_ShouldRenderCurrentOrderAndPartition(bool useQueryCache)
    {
        using var context = PostgresTestContext.Create();
        var source = context.From<ISimpleEntity>().WithTag("captured window keys");

        string OrderSql(Expression<Func<object?>> order) => PrepareSql(context,
            source.Select(_ => SqlFunctions.Sql.row_number().Over((Expression<Func<object?>>?)null, order, null)), useQueryCache);

        string PartitionSql(Expression<Func<object?>>[] partitions) => PrepareSql(context,
            source.Select(_ => SqlFunctions.Sql.row_number().Over(partitions, null, null)), useQueryCache);

        string ArrayOrderSql(WindowOrder[] orders) => PrepareSql(context,
            source.Select(_ => SqlFunctions.Sql.row_number().Over(orders, null)), useQueryCache);

        OrderSql(() => 11).Should().Contain("over (order by 11)");
        OrderSql(() => 22).Should().Contain("over (order by 22)");
        PartitionSql([() => 33]).Should().Contain("over (partition by 33)");
        PartitionSql([() => 44, () => 55]).Should().Contain("over (partition by 44, 55)");
        ArrayOrderSql([new WindowOrder(() => 66, OrderDirection.Desc)]).Should().Contain("over (order by 66 desc)");
        ArrayOrderSql([new WindowOrder(() => 77, OrderDirection.Asc)]).Should().Contain("over (order by 77)");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapturedPercentileFraction_ShouldValidateEachValue(bool useQueryCache)
    {
        using var context = PostgresTestContext.Create();
        var source = context.From<ISimpleEntity>().WithTag("captured percentile fraction");

        QueryCommand<double> Build(double fraction) => source
            .Select(row => SqlFunctions.Postgres.percentile_cont(fraction, () => (double)row.Id));

        PrepareSql(context, Build(0.25), useQueryCache).Should().Contain("percentile_cont(");
        PrepareSql(context, Build(0.75), useQueryCache).Should().Contain("percentile_cont(");
        FluentActions.Invoking(() => PrepareSql(context, Build(1.5), useQueryCache))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("fraction");
    }

    [Fact]
    public void InlineWindowFrame_ShouldKeepTheSamePlanForDifferentFilterValues()
    {
        using var context = PostgresTestContext.Create();
        var source = context.From<ISimpleEntity>().WithTag("inline window frame");
        var threshold = 10;
        var query = source.Where(row => row.Id > threshold).Select(row => SqlFunctions.Sql.row_number()
            .Over(SqlFunctions.Sql.asc(() => row.Id), WindowFrame.Rows(1, 0)));

        DbPreparedQueryCommand<int> Prepare()
            => BoundQueryInvocation<int>.GetPlan(context.GetPreparedQueryCommand(
                query, false, true, CancellationToken.None))!;

        var first = Prepare();
        query.HasUnkeyedSqlShape.Should().BeFalse();
        first.CommandText.Should().Contain("rows between 1 preceding");
        threshold = 20;
        Prepare().Should().BeSameAs(first);
    }

    private static string PrepareSql<T>(IDataContext context, QueryCommand<T> query, bool useQueryCache)
    {
        var prepared = context.GetPreparedQueryCommand(query, false, useQueryCache, CancellationToken.None);
        return BoundQueryInvocation<T>.GetPlan(prepared)!.CommandText;
    }
}
