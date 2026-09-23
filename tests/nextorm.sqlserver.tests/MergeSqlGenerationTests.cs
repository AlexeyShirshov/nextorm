using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

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
/// SQL generation of the key-upsert builder on SQL Server (no database connection). SQL Server renders
/// a <c>MERGE ... USING (VALUES ...) AS source ...</c> statement, terminated by a semicolon.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderMerge()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set target.name = source.name, target.age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age);");
    }

    [Fact]
    public void Merge_Batch_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2), (@p3, @p4, @p5)) as source (id, name, age) on target.id = source.id when matched then update set target.name = source.name, target.age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age);");
    }

    [Fact]
    public void OnKeys_WithoutKey_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.MergeInto<IUnkeyedEntity>()
            .Using(new UnkeyedEntity { Name = "a" })
            .OnKeys();

        act.Should().Throw<InvalidOperationException>();
    }
}
