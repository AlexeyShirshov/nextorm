using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

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

[SqlTable("value_entity")]
public interface IValueEntity
{
    [Column("value")]
    long Value { get; set; }
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

public sealed class ReturningDto
{
    public long Renamed { get; set; }
}

/// <summary>
/// SQL generation of the INSERT builder on PostgreSQL (no database connection). PostgreSQL uses
/// <c>@name</c> parameters and the <c>RETURNING</c> clause for generated keys.
/// </summary>
public class InsertSqlGenerationTests
{
    [Fact]
    public void Value_ShouldRenderParameterizedInsert()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void ValuesEntity_ShouldExcludeIdentityAndComputed()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(new InsertEntity { Id = 99, Name = "a", Age = 5, Total = 7 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1)");
    }

    [Fact]
    public void ValuesBatch_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ToSql()
            .Should().Be("insert into \"insert_entity\" (\"name\") values (@p0)");
    }

    [Fact]
    public void NamingConvention_ShouldTranslateAutoNames()
    {
        using var ctx = PostgresTestContext.CreateSnakeCaseQuoted();

        ctx.InsertInto<BareEntity>()
            .Value(x => x.Name, "a")
            .ToSql()
            .Should().Be("insert into \"bare_entity\" (\"name\") values (@p0)");
    }

    [Fact]
    public void Returning_ShouldAppendAllMappedColumns()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1) returning id, name, age, total");
    }

    [Fact]
    public void ReturningProjection_ShouldAppendSelectedColumns()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0) returning id, name");
    }

    [Fact]
    public void ReturningSingleMember_ShouldAppendOneColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0) returning id");
    }

    [Fact]
    public void ReturningMemberInit_ShouldReturnSourceColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new ReturningDto { Renamed = x.Id })
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0) returning id");
    }

    [Fact]
    public void ReturningBatch_ShouldAppendReturningAfterAllRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values([
                new InsertEntity { Name = "a", Age = 1 },
                new InsertEntity { Name = "b", Age = 2 },
            ])
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3) returning id, name, age, total");
    }

    [Fact]
    public void ReturningIdentitySelector_ShouldAppendColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity(x => x.Id)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0) returning id");
    }

    [Fact]
    public void ReturningKey_ShouldAppendKeyColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningKey<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0) returning id");
    }

    [Fact]
    public void ReturningIdentityFunction_ShouldRenderScalarIdentityQuery()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .ReturningIdentity<long>()
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (@p0); select lastval()");
    }

    [Fact]
    public void ValuesMapping_Anonymous_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

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
        using var ctx = PostgresTestContext.Create();

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
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Value("a")
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0)");
    }

    [Fact]
    public void Values_ScalarArray_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IScalarEntity>()
            .Values(new[] { "a", "b" })
            .ToSql()
            .Should().Be("insert into scalar_entity (name) values (@p0), (@p1)");
    }

    [Fact]
    public void Values_ColumnSequence_ShouldRenderMultipleRows()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Values(x => x.Name, new[] { "a", "b" })
            .Values(x => x.Age, new[] { 1, 2 })
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) values (@p0, @p1), (@p2, @p3)");
    }

    [Fact]
    public void Value_ComputedColumn_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>().Value(x => x.Total, 1);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Insert_NoValues_OnAllGeneratedEntity_ShouldRenderDefaultValues()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IDefaultEntity>()
            .ToSql()
            .Should().Be("insert into default_entity default values");
    }

    [Fact]
    public void Value_ColumnDefault_ShouldRenderDefaultKeyword()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, SqlDefault.Value)
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (default)");
    }

    [Fact]
    public void ValuesMapping_ColumnDefault_ShouldRenderDefaultKeyword()
    {
        using var ctx = PostgresTestContext.Create();
        var sources = new[] { new InsertSource { Name = "a" } };

        ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { Name = SqlDefault.Value })
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (default)");
    }

    [Fact]
    public void ValuesMapping_BoxedColumnDefault_ShouldRenderDefaultKeyword()
    {
        using var ctx = PostgresTestContext.Create();
        var sources = new[] { new InsertSource { Name = "a" } };

        ctx.InsertInto<IInsertEntity>()
            .Values(sources, s => new { Name = (object)SqlDefault.Value })
            .ToSql()
            .Should().Be("insert into insert_entity (name) values (default)");
    }

    [Fact]
    public void Insert_NoValues_WithReturningIdentity_ShouldAppendReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.InsertInto<IDefaultEntity>()
            .ReturningIdentity(x => x.Id)
            .ToSql()
            .Should().Be("insert into default_entity default values returning id");
    }

    [Fact]
    public void ValuesQuery_ShouldRenderInsertSelect()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 1;

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>().Where(s => s.Age > min), s => new { s.Name, s.Age })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source\n where (Age > @min)");
    }

    [Fact]
    public void ValuesQuery_WithReturning_ShouldAppendReturning()
    {
        using var ctx = PostgresTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .Returning(x => new { x.Id, x.Name })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source returning id, name");
    }

    [Fact]
    public void ValuesQuery_ComputedTarget_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age, Total = 1 });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValuesQuery_CombinedWithValue_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValuesQuery_RuntimeParameter_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>().Where(s => s.Age == SqlFunctions.Parameter<int>(0)), s => new { s.Name, s.Age })
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DataModifyingCte_InsertSelectBody_ShouldRenderInsertSelectInsideCte()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
                .Returning(x => new { x.Id }))
            .From("ins")
            .Select(r => new { r.Id }))
            .Should().Be("with ins as (insert into insert_entity (name, age) select Name, Age from insert_source returning id) select id from ins as \"t1\"");
    }

    [Fact]
    public void DataModifyingCte_MutationBodyReferencingReadCte_ShouldHoistBoth()
    {
        using var ctx = PostgresTestContext.Create();
        var scope = ctx.With("src", ctx.From<InsertSource>().Select(s => new { s.Name, s.Age }));

        var insert = ctx.InsertInto<IInsertEntity>()
            .Values(scope.From("src"), a => new { Name = a.GetString("Name"), Age = a.GetInt32("Age") })
            .Returning(x => new { x.Id });

        SqlOf(ctx, scope.With("ins", insert).From("ins").Select(r => new { r.Id }))
            .Should().Be("with src as (select Name, Age from insert_source), ins as (insert into insert_entity (name, age) select Name, Age from src returning id) select id from ins as \"t1\"");
    }

    [Fact]
    public void InsertFromMutationCte_ShouldHoistWithBeforeInsert()
    {
        using var ctx = PostgresTestContext.Create();
        var source = ctx.With("ins", ctx.InsertInto<IValueEntity>()
                .Value(x => x.Value, 5L)
                .Returning(x => new { x.Value }))
            .From("ins");

        Normalize(ctx.InsertInto<IValueEntity>()
            .Values(source, r => new { r.Value })
            .ToSql())
            .Should().Be("with ins as (insert into value_entity (value) values (@p0) returning value) insert into value_entity (value) select value from ins as \"t1\"");
    }

    [Fact]
    public void ValuesQuery_WithReadCte_ShouldHoistWithBeforeInsert()
    {
        using var ctx = PostgresTestContext.Create();
        var inner = ctx.From<InsertSource>().Select(s => new { s.Name, s.Age });
        var source = ctx.With("c", inner).From("c");

        var sql = Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(source, a => new { Name = a.GetString("name"), Age = a.GetInt32("age") })
            .ToSql());

        sql.Should().Be("with c as (select Name, Age from insert_source) insert into insert_entity (name, age) select name, age from c");
    }

    [Fact]
    public void ValuesQuery_MemberInitMapping_ShouldRenderInsertSelect()
    {
        using var ctx = PostgresTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new InsertEntity { Name = s.Name, Age = s.Age })
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source");
    }

    [Fact]
    public void ValuesQuery_ReturningIdentityFunction_ShouldAppendIdentityFunction()
    {
        using var ctx = PostgresTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .ReturningIdentity<long>()
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source; select lastval()");
    }

    [Fact]
    public void ValuesQuery_ReturningKey_ShouldAppendReturning()
    {
        using var ctx = PostgresTestContext.Create();

        Normalize(ctx.InsertInto<IInsertEntity>()
            .Values(ctx.From<InsertSource>(), s => new { s.Name, s.Age })
            .ReturningKey<long>()
            .ToSql())
            .Should().Be("insert into insert_entity (name, age) select Name, Age from insert_source returning id");
    }

    [Fact]
    public void ValuesQuery_UnnestSource_ShouldRenderInsertSelect()
    {
        using var ctx = PostgresTestContext.Create();
        var values = new long[] { 1, 2, 3 };

        Normalize(ctx.InsertInto<IValueEntity>()
            .Values(ctx.FromTableFunction(() => SqlFunctions.Postgres.unnest(values)), r => new { r.Value })
            .ToSql())
            .Should().Be("insert into value_entity (value) select unnest as \"Value\" from (select unnest from unnest(@values)) as \"t1\"");
    }

    [Fact]
    public void DataModifyingCte_SelectFromMutation_ShouldRenderWithInsertReturning()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new { x.Id, x.Name }))
            .From("ins")
            .Select(r => new { r.Id, r.Name }))
            .Should().Be("with ins as (insert into insert_entity (name) values (@p0) returning id, name) select id, name from ins as \"t1\"");
    }

    [Fact]
    public void DataModifyingCte_WhereOnReturnedColumn_ShouldQualifyTheAlias()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new { x.Id, x.Age }))
            .From("ins")
            .Where(r => r.Age > 0)
            .Select(r => new { r.Id }))
            .Should().Be("with ins as (insert into insert_entity (name) values (@p0) returning id, age) select id from ins as \"t1\"\n where (t1.age > 0)");
    }

    [Fact]
    public void DataModifyingCte_WithReadCte_ShouldRenderBothDeclarations()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new { x.Id }))
            .With("src", ctx.From<InsertSource>().Select(s => new { s.Name }))
            .From("ins")
            .Select(r => new { r.Id }))
            .Should().Be("with ins as (insert into insert_entity (name) values (@p0) returning id), src as (select Name from insert_source) select id from ins as \"t1\"");
    }

    [Fact]
    public void DataModifyingCte_FromTable_ShouldReadReadCteAlongsideMutation()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new { x.Id }))
            .With("src", ctx.From<InsertSource>().Select(s => new { s.Age }))
            .FromTable("src")
            .Select(t => new { age = t["Age"].AsInt }))
            .Should().Be("with ins as (insert into insert_entity (name) values (@p0) returning id), src as (select Age from insert_source) select Age from src");
    }

    [Fact]
    public void DataModifyingCte_IdentityReturning_ShouldReadAllColumns()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("ins", ctx.InsertInto<IInsertEntity>()
                .Values(new InsertEntity { Name = "a", Age = 1 })
                .Returning())
            .From("ins")
            .Select(r => new { r.Id, r.Name }))
            .Should().Be("with ins as (insert into insert_entity (name, age) values (@p0, @p1) returning id, name, age, total) select id, name from ins as \"t1\"");
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");
}
