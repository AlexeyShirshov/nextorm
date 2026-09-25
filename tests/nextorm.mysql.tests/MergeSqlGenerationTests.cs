using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

[SqlTable("merge_entity")]
public interface IMergeEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("total")]
    int Total { get; set; }
}

public sealed class MergeEntity : IMergeEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public int Total { get; set; }
}

[SqlTable("unkkey_entity")]
public interface IUnkeyedEntity
{
    [Column("name")]
    string? Name { get; set; }
}

public sealed class UnkeyedEntity : IUnkeyedEntity
{
    public string? Name { get; set; }
}

/// <summary>
/// SQL generation of the key-upsert builder on MySQL (no database connection). MySQL renders
/// <c>INSERT ... ON DUPLICATE KEY UPDATE ...</c>, assigning the incoming value through <c>VALUES()</c>.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderOnDuplicateKeyUpdate()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2) on duplicate key update name = values(name), age = values(age)");
    }

    [Fact]
    public void Merge_Batch_ShouldRenderMultipleRows()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5) on duplicate key update name = values(name), age = values(age)");
    }

    [Fact]
    public void Merge_UppercaseKeywords_ShouldUppercaseTheClause()
    {
        using var ctx = MySqlTestContext.CreateUppercase();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("INSERT INTO merge_entity (id, name, age) VALUES (@p0, @p1, @p2) ON DUPLICATE KEY UPDATE name = VALUES(name), age = VALUES(age)");
    }

    [Fact]
    public void FullMerge_OnMySql_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void KeyUpsert_Returning_ShouldThrowBecauseMySQLHasNoReturning()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>();
    }
}
