using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

public enum SqlGenState
{
    Unknown,
    Active,
    Closed,
}

public sealed class SqlGenStateConverter : ValueConverter<SqlGenState, string>
{
    public override string? ConvertToProvider(SqlGenState model) => model.ToString();

    public override SqlGenState ConvertFromProvider(string? provider) => Enum.Parse<SqlGenState>(provider!);
}

[SqlTable("converter_entity")]
public interface IConverterEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("state")]
    [ValueConverter(typeof(SqlGenStateConverter))]
    SqlGenState State { get; set; }
}

public sealed class ConverterEntity : IConverterEntity
{
    public int Id { get; set; }
    public SqlGenState State { get; set; }
}

/// <summary>
/// Verifies that a value-converted property is planned like any other column and that the write seam
/// converts its value to the provider representation. No database connection is used.
/// </summary>
public class ValueConverterSqlGenerationTests
{
    [Fact]
    public void Insert_WithConvertedProperty_ShouldRenderColumn()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.InsertInto<ConverterEntity>()
            .Values(new ConverterEntity { Id = 1, State = SqlGenState.Active })
            .ToSql()
            .Should().Contain("state");
    }

    [Fact]
    public void UpdateSet_WithConvertedProperty_ShouldRenderColumn()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.Update<ConverterEntity>()
            .Set(x => x.State, SqlGenState.Active)
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Contain("state");
    }

    [Fact]
    public void WriteSeam_ShouldConvertToProviderValue()
    {
        using var ctx = MySqlTestContext.Create();

        var property = new EntityMetadataBuilder<ConverterEntity>().Build().Properties
            .Single(p => p.PropertyInfo.Name == nameof(ConverterEntity.State));
        var method = typeof(DataContext).Assembly.GetType("NextORM.Core.DurationStorage")!
            .GetMethod("ToParameterValue", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = method.Invoke(null, [SqlGenState.Active, property, ((DataContext)ctx).Dialect]);

        result.Should().Be("Active");
    }
}
