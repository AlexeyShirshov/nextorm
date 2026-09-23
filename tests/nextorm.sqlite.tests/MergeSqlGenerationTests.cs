using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

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
/// SQL generation of the key-upsert builder on SQLite (no database connection). SQLite 3.24+ renders
/// <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> with <c>excluded.&lt;column&gt;</c>.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderOnConflictDoUpdate()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values ($p0, $p1, $p2) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void Merge_Batch_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values ($p0, $p1, $p2), ($p3, $p4, $p5) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void OnKeys_WithoutKey_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.MergeInto<IUnkeyedEntity>()
            .Using(new UnkeyedEntity { Name = "a" })
            .OnKeys();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullMerge_OnSqlite_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
