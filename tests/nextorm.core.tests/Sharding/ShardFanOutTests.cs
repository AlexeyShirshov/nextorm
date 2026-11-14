using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

/// <summary>
/// Covers the fan-out surface that does not need a database: configuration guards and the explicit
/// refusal of operations that cannot be answered by concatenating per-shard results.
/// </summary>
public sealed class ShardFanOutTests
{
    [Fact]
    public void AcrossShards_WithoutShardRouter_ShouldThrow()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossShards())
            .Should().Throw<InvalidOperationException>().WithMessage("*shard router*");
    }

    [Fact]
    public void AcrossShards_WithEmptyTopology_ShouldThrow()
    {
        using var context = CreateContext(new ShardTopology());

        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossShards())
            .Should().Throw<InvalidOperationException>().WithMessage("*no configured shards*");
    }

    [Fact]
    public void AcrossShards_WithUnknownShard_ShouldThrow()
    {
        using var context = CreateContext(CreateTopology());

        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossShards("missing"))
            .Should().Throw<ArgumentException>().WithMessage("*not configured*");
        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossShards())
            .Should().NotThrow();
    }

    [Fact]
    public void GlobalOperations_ShouldThrowNotSupportedBeforeExecution()
    {
        using var context = CreateContext(CreateTopology());
        var query = context.From<UnshardedRow>().AcrossShards();

        FluentActions.Invoking(() => query.OrderBy(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.OrderByDescending(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Take(10)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Skip(10)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Distinct()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Count()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Any()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.First()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Single()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.ThenBy(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.ThenByDescending(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.All(row => row.Id > 0)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Sum(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Min(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Max(row => row.Id)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => query.Average(row => row.Id)).Should().Throw<NotSupportedException>();
    }

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology();
        topology.AddShard("east");
        topology.AddShard("west");
        return topology;
    }

    private static SqliteDataContext CreateContext(ShardTopology topology)
        => new("Data Source=:memory:", new DataContextBuilder().UseSharding(topology));

    private sealed class UnshardedRow
    {
        public int Id { get; set; }
    }
}
