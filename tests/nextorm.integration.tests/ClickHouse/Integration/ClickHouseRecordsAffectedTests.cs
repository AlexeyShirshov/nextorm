using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D226 product-surface coverage for <see cref="ProcedureResult.RecordsAffected"/> and the batch
/// surface on ClickHouse, which does not derive <see cref="CommonTestSuite"/> and therefore gets no
/// shared affected-row case. The ClickHouse driver exposes no DML affected-row oracle: every
/// statement reports <c>0</c> (never <c>-1</c>) and a query reports <c>0</c> as well, so these cases
/// pin the product behaviour — the <c>-1</c> sentinel while the reader is live and the driver value
/// after release, on both the sync and the async path — and pin the genuinely unsupported entry
/// points with executed guards instead of silent skips.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseRecordsAffectedTests : ProviderTestSuite
{
    private const string Table = "ch_records_affected";

    private static readonly object SeedGate = new();
    private static bool _seeded;

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private void EnsureSeed()
    {
        lock (SeedGate)
        {
            if (_seeded)
                return;

            var ctx = _sut.DataProvider;
            Execute(ctx, $"drop table if exists {Table}");
            Execute(ctx, $"create table {Table} (id Int32) engine = Memory");
            Execute(ctx, $"insert into {Table} (id) values (1), (2), (3)");
            _seeded = true;
        }
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        using (ctx.ExecuteRaw(sql))
        {
        }
    }

    // --- Supported ClickHouse surface: raw DML and raw queries, sync and async. ---

    [Fact]
    public void ExecuteRaw_Insert_ShouldKeepTheSentinelUntilRelease_ThenTheDriverValue()
    {
        EnsureSeed();
        var ctx = _sut.DataProvider;

        var result = ctx.ExecuteRaw($"insert into {Table} (id) values (991)");

        // Nothing is published while the reader is live.
        result.RecordsAffected.Should().Be(-1);

        result.Dispose();

        // The ClickHouse driver reports 0 for every statement: not -1 (the sentinel) and not a count.
        result.RecordsAffected.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteRawAsync_Insert_ShouldKeepTheSentinelUntilRelease_ThenTheDriverValue()
    {
        EnsureSeed();
        var ctx = _sut.DataProvider;

        await using var result = await ctx.ExecuteRawAsync(
            $"insert into {Table} (id) values (992)",
            [],
            TestContext.Current.CancellationToken);

        result.RecordsAffected.Should().Be(-1);

        await result.DisposeAsync();

        result.RecordsAffected.Should().Be(0);
    }

    [Fact]
    public void ExecuteRaw_Query_ShouldKeepTheSentinelUntilRelease_ThenTheDriverValue()
    {
        EnsureSeed();
        var ctx = _sut.DataProvider;

        var result = ctx.ExecuteRaw($"select count(*) as c from {Table}");

        result.Read<int>().Should().ContainSingle();

        result.RecordsAffected.Should().Be(-1);

        result.Dispose();

        // A ClickHouse query also reports 0 (this driver never returns the -1 sentinel itself).
        result.RecordsAffected.Should().Be(0);
    }

    // --- Unsupported ClickHouse entry points: executed guards, not silent skips. ---

    [Fact]
    public void Batch_ShouldFailClosedWhenTheBatchRenders()
    {
        EnsureSeed();
        var ctx = _sut.DataProvider;

        // The ClickHouse context exposes the batch role, but its dialect has no single-round-trip
        // multi-statement form, so rendering fails closed: no BatchResult — and therefore no
        // BatchResult.RecordsAffected — can be produced on this provider.
        var builder = ctx.CreateBatchBuilder()
            .AddQuery(_sut.ComplexEntity.Where(c => c.Id > 1).Select(c => c.Id))
            .AddQuery(_sut.ComplexEntity.OrderBy(c => c.Id).Select(c => c.Id));

        var act = () => builder.Execute();

        act.Should().Throw<NotSupportedException>().WithMessage("*batch*");
    }

    [Fact]
    public void ExecuteProcedure_ShouldThrowNotSupported()
    {
        EnsureSeed();
        var ctx = _sut.DataProvider;

        var act = () => ctx.ExecuteProcedure("my_proc");

        act.Should().Throw<NotSupportedException>();
    }
}
