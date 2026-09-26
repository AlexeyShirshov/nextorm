using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation of the batch surface on MariaDB (no database connection): a side-effecting statement
/// and several result-bearing queries render as one <c>;</c>-joined command, sharing one parameter
/// sequence (captured variables are prefixed per statement, and <c>AddQuery</c> order is preserved).
/// </summary>
public class BatchSqlGenerationTests
{
    [Fact]
    public void Batch_MutationThenTwoAddQueries_ShouldRenderBothSelectsInOrder()
    {
        using var ctx = MariaDbTestContext.Create();
        var value = 42;
        var min = 5;
        var max = 6;

        var sql = ctx.Batch()
            .Update(ctx.Update<ISimpleEntity>().Set(x => x.Id, value).Where(x => x.Id > min))
            .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id > min).Select(x => new { x.Id }))
            .AddQuery(ctx.From<ISimpleEntity>().Where(x => x.Id < max).Select(x => new { x.Id }))
            .ToSql();

        var statements = sql.Split("; ");
        statements.Should().HaveCount(3);
        statements[0].Should().StartWith("update simple_entity");
        statements[1].Should().Contain("select id from simple_entity").And.Contain("@b1_min");
        statements[2].Should().Contain("select id from simple_entity").And.Contain("@b2_max");
    }

    [Fact]
    public void Dialect_ShouldReportBatchSupport()
    {
        using var ctx = MariaDbTestContext.Create();
        var dialect = ((DataContext)ctx).Dialect;

        dialect.SupportsBatch.Should().BeTrue();
    }
}
