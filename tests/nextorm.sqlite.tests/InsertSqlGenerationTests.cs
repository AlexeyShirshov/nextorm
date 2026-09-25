using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

[SqlTable("insert_entity")]
public interface IInsertEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
    [Column("description")]
    string? Description { get; set; }
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("total")]
    int Total { get; set; }
}

public sealed class InsertEntity : IInsertEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Description { get; set; }
    public int Total { get; set; }
}

[SqlTable("scalar_entity")]
public interface IScalarEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
}

public sealed class ScalarEntity : IScalarEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
}

[SqlTable("default_entity")]
public interface IDefaultEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
}

public sealed class DefaultEntity : IDefaultEntity
{
    public long Id { get; set; }
}

[SqlTable("insert_source")]
public sealed class InsertSource
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Extra { get; set; }
}

[SqlTable("timestamp_entity")]
public interface ITimestampEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("created_on")]
    DateTime? CreatedOn { get; set; }
}

public sealed class TimestampEntity : ITimestampEntity
{
    public long Id { get; set; }
    public DateTime? CreatedOn { get; set; }
}

internal sealed class TimestampHolder
{
    public DateTime? CreatedOn { get; set; }
}

/// <summary>
/// SQL generation of the INSERT builder on SQLite (no database connection). SQLite uses
/// <c>$name</c> parameters and the ANSI <c>RETURNING</c> clause for generated keys.
/// </summary>
public class InsertSqlGenerationTests
{
    [Fact]
    public void ValuesQuery_ShouldRenderInsertSelect()
    {
        using var ctx = SqliteTestContext.Create();
        var min = 1;

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>().Where(s => s.Age > min), s => new { s.Name, s.Age })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source\n where (Age > $min)");
    }

    [Fact]
    public void ValuesQuery_WithReturning_ShouldAppendReturning()
    {
        using var ctx = SqliteTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .Returning(x => new { x.Id, x.Name })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source returning id, name");
    }

    [Fact]
    public void ValuesQuery_ComputedTarget_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, Total = 1 });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesQuery_CombinedWithValue_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age });

        act.Should().Throw<InvalidOperationException>();
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void DataModifyingCte_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.With("ins", ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }

    [Fact]
    public void Value_ShouldRenderParameterizedInsert()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values ($p0, $p1)");
    }

    [Fact]
    public void ValuesEntity_ShouldExcludeIdentityAndComputed()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(new InsertEntity { Id = 99, Name = "a", Age = 5, Description = "d", Total = 7 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2)");
    }

    [Fact]
    public void ValuesBatch_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .ToSql()
            .Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2), ($p3, $p4, $p5)");
    }

    [Fact]
    public void Value_ColumnReference_ShouldRenderColumnName()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, x => x.Description)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (description)");
    }

    [Fact]
    public void Value_StaticMember_ShouldBindParameter()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<ITimestampEntity>()
            .Value(x => x.CreatedOn, x => DateTime.Now)
            .ToSql()
            .Should().Be("insert into timestamp_entity (created_on) values ($p0)");
    }

    [Fact]
    public void Value_CapturedMember_ShouldBindParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var holder = new TimestampHolder { CreatedOn = new DateTime(2024, 1, 2, 3, 4, 5) };

        ctx.InsertInto<ITimestampEntity>()
            .Value(x => x.CreatedOn, x => holder.CreatedOn)
            .ToSql()
            .Should().Be("insert into timestamp_entity (created_on) values ($p0)");
    }

    [Fact]
    public void Value_SelfColumnReference_ShouldRenderColumn()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<ITimestampEntity>()
            .Value(x => x.CreatedOn, x => x.CreatedOn)
            .ToSql()
            .Should().Be("insert into timestamp_entity (created_on) values (created_on)");
    }

    [Fact]
    public void Value_EntityReferencingExpression_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<ITimestampEntity>()
            .Value(x => x.CreatedOn, x => x.Id > 0 ? DateTime.Now : DateTime.MinValue);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .ToSql()
            .Should().Be("insert into \"insert_entity\" (\"name\", \"age\") values ($p0, $p1)");
    }

    [Fact]
    public void NoValues_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.InsertInto<IInsertEntity>();

        var act = () => builder.ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValueAndValues_ShouldBeMutuallyExclusive()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(new InsertEntity { Name = "a" })
            .Value(x => x.Age, 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Returning_ShouldAppendAllMappedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values ($p0, $p1) returning id, name, age, description, total");
    }

    [Fact]
    public void ReturningProjection_ShouldAppendSelectedColumns()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("insert into insert_entity (name) values ($p0) returning id, name");
    }

    [Fact]
    public void ReturningSingleMember_ShouldAppendOneColumn()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values ($p0) returning id");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseReturning()
    {
        using var ctx = SqliteTestContext.CreateUppercase();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("INSERT INTO insert_entity (name) VALUES ($p0) RETURNING id");
    }

    [Fact]
    public void ReturningBatch_ShouldAppendReturningAfterAllRows()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age, description) values ($p0, $p1, $p2), ($p3, $p4, $p5) returning id, name, age, description, total");
    }

    [Fact]
    public void ReturningIdentitySelector_ShouldAppendColumn()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values ($p0) returning id");
    }

    [Fact]
    public void ReturningKey_ShouldAppendKeyColumn()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) values ($p0) returning id");
    }

    [Fact]
    public void ReturningIdentityFunction_ShouldRenderScalarIdentityQuery()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) values ($p0); select last_insert_rowid()");
    }

    [Fact]
    public void ValuesMapping_Anonymous_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        var sources = new[]
        {
            new InsertSource { Name = "a", Age = 1 },
            new InsertSource { Name = "b", Age = 2 },
        };

        ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { Name = s.Name, Age = s.Age })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values ($p0, $p1), ($p2, $p3)");
    }

    [Fact]
    public void ValuesMapping_EntityInitializer_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        var sources = new[]
        {
            new InsertSource { Name = "a", Age = 1 },
            new InsertSource { Name = "b", Age = 2 },
        };

        ctx.InsertInto<InsertEntity>()
            .Values(sources, s => new InsertEntity { Name = s.Name, Age = s.Age })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values ($p0, $p1), ($p2, $p3)");
    }

    [Fact]
    public void Value_Scalar_ShouldRenderSingleRow()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Value("a")
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values ($p0)");
    }

    [Fact]
    public void Values_ScalarArray_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Values(new[] { "a", "b" })
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values ($p0), ($p1)");
    }

    [Fact]
    public void Values_ColumnSequence_ShouldRenderMultipleRows()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a", "b" })
            .Values(x => x.Age, new[] { 1, 2 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values ($p0, $p1), ($p2, $p3)");
    }

    [Fact]
    public void Value_Scalar_OnMultiColumnEntity_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value("a");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValuesMapping_UnknownMember_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var sources = new[] { new InsertSource { Name = "a", Age = 1 } };

        var act = () => ctx.InsertInto<IInsertEntity>().Values(sources, s => new { s.Extra });

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ValuesMapping_ComputedMember_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var sources = new[] { new InsertSource { Name = "a", Age = 1 } };

        var act = () => ctx.InsertInto<IInsertEntity>().Values(sources, s => new { s.Name, Total = s.Age });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Values_ColumnSequence_MismatchedLength_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a", "b" })
            .Values(x => x.Age, new[] { 1 });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Values_ColumnSequence_DuplicateColumn_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a" })
            .Values(x => x.Name, new[] { "b" });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValuesMapping_AndValue_ShouldBeMutuallyExclusive()
    {
        using var ctx = SqliteTestContext.Create();
        var sources = new[] { new InsertSource { Name = "a", Age = 1 } };

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { s.Name, s.Age })
            .Value(x => x.Name, "b");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValuesMapping_EmptySource_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(Array.Empty<InsertSource>(), s => new { s.Name, s.Age });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Value_ComputedColumn_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Total, 1);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Value_ComputedColumnReference_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Total, x => x.Age);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Values_ColumnSequence_ComputedColumn_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Values(x => x.Total, new[] { 1 });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Insert_NoValues_OnAllGeneratedEntity_ShouldRenderDefaultValues()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.InsertInto<IDefaultEntity>()
            .ToSql()
            .Should().Be("insert into default_entity default values");
    }

    [Fact]
    public void Value_ColumnDefault_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Name, SqlDefault.Value).ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
