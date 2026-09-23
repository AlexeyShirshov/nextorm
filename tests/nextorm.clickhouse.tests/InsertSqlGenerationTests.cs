using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

[SqlTable("insert_entity")]
public interface IInsertEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class InsertEntity : IInsertEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

[SqlTable("scalar_entity")]
public interface IScalarEntity
{
    [Column("name")]
    string? Name { get; set; }
}

public sealed class ScalarEntity : IScalarEntity
{
    public string? Name { get; set; }
}

[SqlTable("default_entity")]
public interface IDefaultEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
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
/// SQL generation of the INSERT builder on ClickHouse (no database connection). ClickHouse has no
/// <c>RETURNING</c>/<c>OUTPUT</c>/identity function, so only the plain insert is rendered here;
/// <c>Returning</c>/<c>ReturningIdentity</c>/<c>ReturningKey</c> are rejected (see the integration suite).
/// </summary>
public class InsertSqlGenerationTests
{
    [Fact]
    public void ValuesQuery_ShouldRenderInsertSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var min = 1;

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>().Where(s => s.Age > min), s => new { s.Name, s.Age })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source\n where (Age > @min)");
    }

    [Fact]
    public void ValuesQuery_WithReturning_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .Returning(x => new { x.Id, x.Name })
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesQuery_CombinedWithValue_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age });

        act.Should().Throw<InvalidOperationException>();
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void DataModifyingCte_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.With("ins", ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*data-modifying*");
    }


    [Fact]
    public void Value_ShouldRenderParameterizedInsert()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void ValuesBatch_ShouldRenderMultipleRows()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .ToSql()
            .Should().Be("insert into insert_entity (id, name, age) values (@p0, @p1, @p2), (@p3, @p4, @p5)");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBackticks()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ToSql()
            .Should().Be("insert into `insert_entity` (`name`) values (@p0)");
    }

    [Fact]
    public void Dialect_ShouldNotSupportGeneratedIdentity()
    {
        var dialect = new NextORM.ClickHouse.ClickHouseDialect();

        dialect.SupportsReturning.Should().BeFalse();
        dialect.SupportsOutput.Should().BeFalse();
        dialect.SupportsLastInsertId.Should().BeFalse();
        dialect.SupportsIdentityFunction.Should().BeFalse();
    }

    [Fact]
    public void Returning_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var builder = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning();

        var act = () => builder.ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ReturningIdentityFunction_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var builder = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity<long>();

        var act = () => builder.ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ReturningKey_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var builder = ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<long>();

        var act = () => builder.ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesMapping_Anonymous_ShouldRenderMultipleRows()
    {
        using var ctx = ClickHouseTestContext.Create();

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
        using var ctx = ClickHouseTestContext.Create();

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
        using var ctx = ClickHouseTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Value("a")
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0)");
    }

    [Fact]
    public void Values_ScalarArray_ShouldRenderMultipleRows()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Values(new[] { "a", "b" })
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0), (@p1)");
    }

    [Fact]
    public void Values_ColumnSequence_ShouldRenderMultipleRows()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a", "b" })
            .Values(x => x.Age, new[] { 1, 2 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void Insert_NoValues_OnAllGeneratedEntity_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.InsertInto<IDefaultEntity>().ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Value_ColumnDefault_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Name, SqlDefault.Value).ToSql();

        act.Should().Throw<NotSupportedException>();
    }
}
