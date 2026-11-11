using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

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
/// SQL generation of MERGE assignment expressions on MariaDB (no database connection), exercised
/// through its own project and dialect — never routed through the MySQL test project. The native
/// <c>ON DUPLICATE KEY UPDATE</c> lowering binds the original source row as statement parameters, reads
/// the target row through the table reference and fails closed with a named diagnostic on an unsupported
/// shape.
/// </summary>
public class MergeAssignmentSqlTests
{
    private static AssignEntity Source() => new() { Id = 1, Name = "a", Age = 5 };

    [Fact]
    public void KeyUpsert_UpdateExpression_ShouldBindSourceAsParametersAndKeepTargetOnTheLeft()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name, Age = target.Age + 1 })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into assign_entity (id, name, age) values (@p2, _utf8mb4 X'61', @p4) on duplicate key update name = @p0, age = (assign_entity.age + @p1)");
    }

    [Fact]
    public void KeyUpsert_InsertExpression_ShouldRenderAssignedColumnsFromTheSource()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql()
            .Should().Be("insert into assign_entity(id, name) values (@p0, @p1) on duplicate key update name = values(name), age = values(age)");
    }

    [Fact]
    public void KeyUpsert_UpdateAndInsertExpressions_ShouldShareOneParameterSequence()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = source.Name })
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name, Age = source.Age })
            .ToSql()
            .Should().Be("insert into assign_entity(id, name, age) values (@p1, @p2, @p3) on duplicate key update name = @p0");
    }

    [Fact]
    public void KeyUpsert_ConstantValue_ShouldBeParameterizedNotInlined()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = 42 })
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Be("insert into assign_entity (id, name, age) values (@p1, _utf8mb4 X'61', @p3) on duplicate key update age = @p0");
        sql.Should().NotContain("42", "an assignment constant is a bound parameter, never an inlined literal");
    }

    [Fact]
    public void KeyUpsert_StringConstantValue_ShouldBeParameterizedNotInlined()
    {
        using var ctx = MariaDbTestContext.Create();

        var sql = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Name = "literal" })
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Be("insert into assign_entity (id, name, age) values (@p1, _utf8mb4 X'61', @p3) on duplicate key update name = @p0");
        sql.Should().NotContain("literal", "a string assignment is a bound parameter, never an inlined literal");
    }

    [Fact]
    public void KeyUpsert_ExcludedRole_ShouldRenderTheNativeProposedRow()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate((target, source, excluded) => new { Name = excluded.Name })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().Be("insert into assign_entity (id, name, age) values (@p0, _utf8mb4 X'61', @p2) on duplicate key update name = values(name)");
    }

    [Fact]
    public void KeyUpsert_InsertConstantKey_ShouldFailBeforeExecution()
    {
        using var ctx = MariaDbTestContext.Create();
        var merge = ctx.CreateMergeBuilder<IAssignEntity>()
            .Using(Source())
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = 1L, Name = source.Name });

        var act = () => merge.ToSql();

        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("assignments");
        exception.Message.Should().Be("MERGE key-upsert INSERT must assign conflict key 'Id' directly from source.Id. (Parameter 'assignments')");
    }

    [Fact]
    public void KeyUpsert_BatchSource_ReadingSourceInTheUpdate_ShouldFailClosed()
    {
        using var ctx = MariaDbTestContext.Create();
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
            .WithMessage("Provider 'MariaDbDialect' requires one materialized source row for original-source references in key-upsert UPDATE assignments.");
    }

    [Fact]
    public void KeyUpsert_UnsupportedValueShape_ShouldNameTheNodeKind()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
        var merge = ctx.CreateMergeBuilder<FilteredAssignEntity>(b => b.HasQueryFilter(e => e.TenantId == 1))
            .Using(new FilteredAssignEntity { Id = 1, Name = "a", TenantId = 2 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name });

        var act = () => merge.ToSql();

        // The provider cannot express the filter as an atomic target predicate in a key upsert, so the
        // pre-existing form-level capability guard (plan §7) fires before the assignment-filter guard:
        // fail closed, never silently bypass the filter.
        act.Should().Throw<NotSupportedException>()
            .WithMessage("An active global query filter cannot isolate the write target of this merge atomically: the provider's key upsert form would silently bypass it. Call IgnoreFilters() to disable the filter, or use the full-MERGE branch form on a provider that supports it (SQL Server, PostgreSQL 15+).");

        var unfiltered = ctx.CreateMergeBuilder<FilteredAssignEntity>()
            .Using(new FilteredAssignEntity { Id = 1, Name = "a", TenantId = 2 })
            .OnKeys()
            .IgnoreFilters()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name })
            .ToSql();

        unfiltered.Should().Be("insert into assign_filtered(id, name) values (@p0, @p1) on duplicate key update name = values(name), tenant_id = values(tenant_id)");
    }
}
