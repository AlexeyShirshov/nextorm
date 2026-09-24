using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Builder-level validation of the bulk-insert terminal. The in-memory context is read-only, so any
/// terminal that needs the database throws; no database is involved.
/// </summary>
public class BulkInsertBuilderTests
{
    [Fact]
    public void NoValues_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().BulkInsert();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Values*");
    }

    [Fact]
    public void ValuesTwice_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity()]).Values([new BulkEntity()]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InMemory_BulkInsert_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).BulkInsert();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task InMemory_BulkInsertAsync_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).BulkInsertAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void InMemory_ToSql_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SyncTerminal_WithAsyncSource_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values(EmptyAsync()).BulkInsert();

        act.Should().Throw<InvalidOperationException>().WithMessage("*async*");
    }

    [Fact]
    public void NonPositiveBatchSize_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>(o => o.MaxBatchSize(0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidOptions_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>(new BulkInsertOptions { MaxBatchSize = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InMemory_ReturningKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).ReturningKey<int>().ToList();

        act.Should().Throw<NotSupportedException>();
    }

    private static async IAsyncEnumerable<BulkEntity> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}

public interface IBulkEntity
{
    int Id { get; set; }
    string Name { get; set; }
}

public class BulkEntity : IBulkEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
