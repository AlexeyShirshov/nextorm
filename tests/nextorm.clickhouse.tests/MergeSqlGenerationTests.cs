using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

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
}

public sealed class MergeEntity : IMergeEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

/// <summary>
/// Key upsert on ClickHouse: there is no engine-level DML upsert (<c>ReplacingMergeTree</c> is an engine
/// property, not DML), so the builder rejects it with <see cref="NotSupportedException"/>.
/// </summary>
public class MergeSqlGenerationTests
{
    [Fact]
    public void Merge_OnClickHouse_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
