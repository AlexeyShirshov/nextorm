using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

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

/// <summary>
/// SQL generation of the key-upsert builder on MariaDB (no database connection). MariaDB inherits the
/// MySQL <c>INSERT ... ON DUPLICATE KEY UPDATE ...</c> form and the <c>VALUES()</c> reference.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderOnDuplicateKeyUpdate()
    {
        using var ctx = MariaDbTestContext.Create();

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
        using var ctx = MariaDbTestContext.Create();

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
}
