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
    public void TableOverride_EmptyTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>(o => o.Table(string.Empty));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TableOverride_SchemaWithoutTable_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>(new BulkInsertOptions { TableSchema = "staging" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TableOverride_TwoArg_EmptySchema_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>(o => o.Table(string.Empty, "bulk_target"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InMemory_ReturningKey_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.BulkInsertInto<BulkEntity>().Values([new BulkEntity { Id = 1, Name = "a" }]).ReturningKey<int>().ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_ShouldBuild()
    {
        var options = new BulkInsertOptionsBuilder()
            .CheckConstraints()
            .TableLock()
            .KeepNulls()
            .FireTriggers()
            .Build();

        options.CheckConstraints.Should().BeTrue();
        options.TableLock.Should().BeTrue();
        options.KeepNulls.Should().BeTrue();
        options.FireTriggers.Should().BeTrue();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_ShouldDefaultToNull()
    {
        var options = new BulkInsertOptionsBuilder().Build();

        options.CheckConstraints.Should().BeNull();
        options.TableLock.Should().BeNull();
        options.KeepNulls.Should().BeNull();
        options.FireTriggers.Should().BeNull();
    }

    [Fact]
    public void OptionsBuilder_BulkCopyFlags_CanBeDisabledExplicitly()
    {
        var options = new BulkInsertOptionsBuilder().TableLock(false).Build();

        options.TableLock.Should().BeFalse();
    }

    [Fact]
    public void BulkCopyFlags_None_ShouldBeEmpty()
    {
        BulkCopyFlags.None.IsAny.Should().BeFalse();
        new BulkCopyFlags(CheckConstraints: true, TableLock: false, KeepNulls: false, FireTriggers: false).IsAny.Should().BeTrue();
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
