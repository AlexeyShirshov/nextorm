using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse has no single-round-trip multi-statement guarantee, so the batch surface is gated off
/// with <see cref="NotSupportedException"/> rather than silently degraded to separate statements.
/// These tests only render SQL; no connection is opened.
/// </summary>
public class BatchSqlGenerationTests
{
    [Fact]
    public void Batch_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.Batch()
            .CreateTableAs("archive_ids", ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .Query(ctx.From<ISimpleEntity>().Select(x => new { x.Id }))
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*batch*");
    }

    [Fact]
    public void Dialect_ShouldNotReportBatchSupport()
    {
        using var ctx = ClickHouseTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsBatch.Should().BeFalse();
    }
}
