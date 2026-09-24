using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

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
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column("total")]
    int Total { get; set; }
}

public sealed class InsertEntity : IInsertEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
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
}

/// <summary>
/// SQL generation of the INSERT builder on SQL Server (no database connection). SQL Server uses
/// bracket-quoted identifiers and the <c>OUTPUT inserted.&lt;column&gt;</c> clause for generated keys.
/// </summary>
public class InsertSqlGenerationTests
{
    [Fact]
    public void ValuesQuery_ShouldRenderInsertSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var min = 1;

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>().Where(s => s.Age > min), s => new { s.Name, s.Age })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source\n where (Age > @min)");
    }

    [Fact]
    public void ValuesQuery_WithOutput_ShouldPlaceOutputBeforeSelect()
    {
        using var ctx = SqlServerTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .Returning(x => new { x.Id, x.Name })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) output inserted.id, inserted.name select Name, Age from insert_source");
    }

    [Fact]
    public void ValuesQuery_ComputedTarget_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, Total = 1 });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesQuery_CombinedWithValue_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age });

        act.Should().Throw<InvalidOperationException>();
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void DataModifyingCte_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.With("ins", ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }


    [Fact]
    public void Value_ShouldRenderParameterizedInsert()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void ValuesEntity_ShouldExcludeIdentityAndComputed()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(new InsertEntity { Id = 99, Name = "a", Age = 5, Total = 7 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void ValuesBatch_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBrackets()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ToSql()
            .Should().Be("insert into [insert_entity] ([name]) values (@p0)");
    }

    [Fact]
    public void Returning_ShouldPlaceOutputBeforeValues()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) output inserted.id, inserted.name, inserted.age, inserted.total values (@p0, @p1)");
    }

    [Fact]
    public void ReturningProjection_ShouldOutputSelectedColumns()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id, inserted.name values (@p0)");
    }

    [Fact]
    public void ReturningSingleMember_ShouldOutputOneColumn()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id values (@p0)");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseOutput()
    {
        using var ctx = SqlServerTestContext.CreateUppercase();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("INSERT INTO insert_entity (name) OUTPUT inserted.id VALUES (@p0)");
    }

    [Fact]
    public void ReturningIdentitySelector_ShouldOutputColumn()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id values (@p0)");
    }

    [Fact]
    public void ReturningKey_ShouldOutputKeyColumn()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id values (@p0)");
    }

    [Fact]
    public void ReturningIdentityFunction_ShouldRenderScalarIdentityQuery()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0); select scope_identity()");
    }

    [Fact]
    public void ValuesMapping_Anonymous_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        var sources = new[]
        {
            new InsertSource { Name = "a", Age = 1 },
            new InsertSource { Name = "b", Age = 2 },
        };

        ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { Name = s.Name, Age = s.Age })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void ValuesMapping_EntityInitializer_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        var sources = new[]
        {
            new InsertSource { Name = "a", Age = 1 },
            new InsertSource { Name = "b", Age = 2 },
        };

        ctx.InsertInto<InsertEntity>()
            .Values(sources, s => new InsertEntity { Name = s.Name, Age = s.Age })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void Value_Scalar_ShouldRenderSingleRow()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Value("a")
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0)");
    }

    [Fact]
    public void Values_ScalarArray_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Values(new[] { "a", "b" })
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0), (@p1)");
    }

    [Fact]
    public void Values_ColumnSequence_ShouldRenderMultipleRows()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a", "b" })
            .Values(x => x.Age, new[] { 1, 2 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void Value_ComputedColumn_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Total, 1);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Insert_NoValues_OnAllGeneratedEntity_ShouldRenderDefaultValues()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IDefaultEntity>()
            .ToSql()
            .Should().Be("insert into default_entity default values");
    }

    [Fact]
    public void Insert_NoValues_WithOutput_ShouldPlaceOutputBeforeDefaultValues()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IDefaultEntity>()
            .ReturningIdentity(x => x.Id)
            .ToSql()
            .Should().Be("insert into default_entity output inserted.id default values");
    }

    [Fact]
    public void Value_ColumnDefault_ShouldRenderDefaultKeyword()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, SqlDefault.Value)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (default)");
    }
}
