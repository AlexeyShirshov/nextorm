using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

[SqlTable("assign_entity")]
public interface IAssignEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }

    [Column("age")]
    int Age { get; set; }
}

public sealed class AssignEntity : IAssignEntity
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public int Age { get; set; }
}

/// <summary>
/// MERGE assignment expressions on ClickHouse (no database connection): ClickHouse has no engine-level
/// DML upsert and no MERGE statement, so every expression form fails closed with the frozen named
/// diagnostic instead of rendering SQL that the provider cannot execute.
/// </summary>
public class MergeAssignmentSqlTests
{
    private static AssignEntity Source() => new() { Id = 1, Name = "a", Age = 5 };

    [Fact]
    public void KeyUpsert_ExpressionUpdate_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatchedInsert();

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Provider 'ClickHouseDialect' does not support MERGE assignment expressions.");
    }

    [Fact]
    public void KeyUpsert_ExpressionInsert_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name });

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Provider 'ClickHouseDialect' does not support MERGE assignment expressions.");
    }

    [Fact]
    public void FullMerge_ExpressionUpdate_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatched().ThenUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatched().ThenInsert();

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Provider 'ClickHouseDialect' does not support MERGE assignment expressions.");
    }

    [Fact]
    public void FullMerge_ExpressionInsert_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name });

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Provider 'ClickHouseDialect' does not support MERGE assignment expressions.");
    }
}
