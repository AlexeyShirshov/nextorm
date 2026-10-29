using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

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

/// <summary>A destination whose active target filter the expression-INSERT guard observes.</summary>
[SqlTable("assign_filtered")]
public sealed class FilteredAssignEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }
}

/// <summary>
/// SQL generation of MERGE assignment expressions on SQLite (no database connection): the native
/// <c>ON CONFLICT ... DO UPDATE</c> lowering binds the original source row as statement parameters,
/// reads the target row through the table reference, uses <c>excluded.&lt;column&gt;</c> for the native
/// proposed row, and fails closed with a named diagnostic on an unsupported shape.
/// </summary>
public class MergeAssignmentSqlTests
{
    private static AssignEntity Source() => new() { Id = 1, Name = "a", Age = 5 };

    [Fact]
    public void KeyUpsert_UpdateExpression_ShouldBindSourceAsParametersAndKeepTargetOnTheLeft()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into assign_entity (id, name, age) values ($p2, $p3, $p4) on conflict (id) do update set name = $p0, age = (assign_entity.age + $p1)");
    }

    [Fact]
    public void KeyUpsert_UpdateExpression_ReadingTargetAndSource_ShouldQualifyTheTargetAndBindTheSource()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into assign_entity (id, name, age) values ($p1, $p2, $p3) on conflict (id) do update set age = (assign_entity.age + $p0)");
    }

    [Fact]
    public void KeyUpsert_InsertExpression_ShouldRenderAssignedColumnsFromTheSource()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql()
            .Should().Be("insert into assign_entity(id, name) values ($p0, $p1) on conflict (id) do update set name = excluded.name, age = excluded.age");
    }

    [Fact]
    public void KeyUpsert_UpdateAndInsertExpressions_ShouldShareOneParameterSequence()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name, Age = source.Age })
            .ToSql()
            .Should().Be("insert into assign_entity(id, name, age) values ($p1, $p2, $p3) on conflict (id) do update set name = $p0");
    }

    [Fact]
    public void KeyUpsert_ConstantValue_ShouldBeParameterizedNotInlined()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = 42 })
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Be("insert into assign_entity (id, name, age) values ($p1, $p2, $p3) on conflict (id) do update set age = $p0");
        sql.Should().NotContain("42", "an assignment constant is a bound parameter, never an inlined literal");
    }

    [Fact]
    public void KeyUpsert_StringConstantValue_ShouldBeParameterizedNotInlined()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = "literal" })
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Be("insert into assign_entity (id, name, age) values ($p1, $p2, $p3) on conflict (id) do update set name = $p0");
        sql.Should().NotContain("literal", "a string assignment is a bound parameter, never an inlined literal");
    }

    [Fact]
    public void KeyUpsert_ExcludedRole_ShouldRenderTheNativeProposedRow()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source, excluded) => new { Name = excluded.Name })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into assign_entity (id, name, age) values ($p0, $p1, $p2) on conflict (id) do update set name = excluded.name");
    }

    [Fact]
    public void KeyUpsert_ExpressionInsert_WithExcludedUpdateArm_ShouldReadTheNativeProposedRow()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source, excluded) => new { Age = excluded.Age })
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql()
            .Should().Be("insert into assign_entity(id, name) values ($p0, $p1) on conflict (id) do update set age = excluded.age");
    }

    [Fact]
    public void KeyUpsert_InsertConstantKey_ShouldFailBeforeExecution()
    {
        using var ctx = SqliteTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = 1L, Name = source.Name });

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Contain("conflict key 'Id'");
    }

    [Fact]
    public void KeyUpsert_BatchSource_ReadingSourceInTheUpdate_ShouldFailClosed()
    {
        using var ctx = SqliteTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using([
                Source(),
                new AssignEntity { Id = 2, Name = "b", Age = 6 },
            ])
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatchedInsert();

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*requires one materialized source row for original-source references in key-upsert UPDATE assignments*");
    }

    [Fact]
    public void KeyUpsert_UnsupportedValueShape_ShouldNameTheNodeKind()
    {
        using var ctx = SqliteTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = new string('x', source.Age) })
            .WhenNotMatchedInsert();

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*MERGE assignment for 'Name' cannot translate expression 'New'*");
    }

    [Fact]
    public void ExpressionInsert_WithActiveTargetFilter_ShouldFailClosedAndIgnoreFiltersOptOut()
    {
        using var ctx = SqliteTestContext.Create();
        var merge = ctx.CreateMergeBuilder<FilteredAssignEntity>(b => b.HasQueryFilter(e => e.TenantId == 1))
            .Using(new FilteredAssignEntity { Id = 1, Name = "a", TenantId = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name });

        var act = () => merge.ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*cannot isolate the write target*IgnoreFilters*");

        var unfiltered = ctx.CreateMergeBuilder<FilteredAssignEntity>()
            .Using(new FilteredAssignEntity { Id = 1, Name = "a", TenantId = 2 })
            .OnKeys()
            .IgnoreFilters()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql();

        unfiltered.Should().Be("insert into assign_filtered(id, name) values ($p0, $p1) on conflict (id) do update set name = excluded.name, tenant_id = excluded.tenant_id");
    }
}
