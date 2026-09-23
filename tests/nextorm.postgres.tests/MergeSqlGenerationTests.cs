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

    [Fact]
    public void FullMerge_AllBranches_ShouldRenderMerge()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedDelete_ShouldRenderDeleteBranch()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then delete when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_SelectedColumns_ShouldRenderSubset()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate(d => new { d.Name })
            .WhenNotMatched().ThenInsert(d => new { d.Id, d.Name })
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name when not matched then insert (id, name) values (source.id, source.name)");
    }

    [Fact]
    public void FullMerge_NotMatchedBySource_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenNotMatchedBySource().ThenDelete()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void FullMerge_ReturningProjection_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age) returning target.id, target.name");
    }

    [Fact]
    public void FullMerge_QuerySource_ShouldRenderUsingSelect()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>())
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total from merge_entity) as source on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedDoNothing_ShouldRenderDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenDoNothing()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then do nothing when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_NotMatchedDoNothing_ShouldRenderDoNothing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenDoNothing()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then do nothing");
    }

    [Fact]
    public void FullMerge_OnCondition_ShouldRenderOnSearchCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .On((t, s) => t.Id == s.Id && s.Age > 0)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on (target.id = source.id and (source.age > 0)) when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_MatchedCondition_ShouldRenderAndCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched and target.name != source.name then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_NotMatchedCondition_ShouldRenderAndCondition()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set name = source.name, age = source.age when not matched and (source.age > 0) then insert (id, name, age) values (source.id, source.name, source.age)");
    }

    [Fact]
    public void FullMerge_QuerySourceWithParameter_ShouldRenderSourceParameterOnce()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 5;

        ctx.MergeInto<IMergeEntity>()
            .Using(ctx.From<IMergeEntity>().Where(x => x.Id > min))
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql()
            .Should().Be("merge into merge_entity as target using (select id, name, age, total from merge_entity\n where (id > cast(@min as bigint))) as source on target.id = source.id when matched then update set name = source.name, age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age)");
    }
}
