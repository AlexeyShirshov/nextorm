using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

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

[SqlTable("keyonly_entity")]
public interface IKeyOnlyEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

public sealed class KeyOnlyEntity : IKeyOnlyEntity
{
    public long Id { get; set; }
}

/// <summary>
/// SQL generation of the key-upsert builder on PostgreSQL (no database connection). PostgreSQL renders
/// <c>INSERT ... ON CONFLICT (&lt;keys&gt;) DO UPDATE SET ...</c> with <c>excluded.&lt;column&gt;</c>.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_Entity_ShouldRenderOnConflictDoUpdate()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5, Total = 9 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void Merge_Batch_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using([
                new MergeEntity { Id = 1, Name = "a", Age = 1 },
                new MergeEntity { Id = 2, Name = "b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into merge_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void Merge_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into \"merge_entity\" (\"id\", \"name\", \"age\") values (@p0, @p1, @p2) on conflict (\"id\") do update set \"name\" = excluded.\"name\", \"age\" = excluded.\"age\"");
    }

    [Fact]
    public void OnKeys_WithoutKey_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IUnkeyedEntity>()
            .Using(new UnkeyedEntity { Name = "a" })
            .OnKeys();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OnKeys_OnIdentityKey_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IInsertEntity>()
            .Using(new InsertEntity { Name = "a", Age = 1 })
            .OnKeys();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Merge_OnKeyOnlyEntity_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IKeyOnlyEntity>()
            .Using(new KeyOnlyEntity { Id = 1 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Merge_WithoutUsing_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_WithoutBranches_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a" })
            .OnKeys()
            .ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Using_Twice_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a" })
            .Using(new MergeEntity { Id = 2, Name = "b" });

        act.Should().Throw<InvalidOperationException>();
    }
}
