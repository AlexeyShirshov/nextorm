using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

public sealed class SqlGenJsonPoco
{
    public string? Name { get; set; }
    public List<int> Values { get; set; } = [];
}

[SqlTable("json_entity")]
public interface IJsonEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("data")]
    [JsonColumn]
    SqlGenJsonPoco Data { get; set; }

    [Column("data_text")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    SqlGenJsonPoco DataText { get; set; }
}

public sealed class JsonEntity : IJsonEntity
{
    public int Id { get; set; }
    public SqlGenJsonPoco Data { get; set; } = new();
    public SqlGenJsonPoco DataText { get; set; } = new();
}

/// <summary>
/// Verifies that a JSON-mapped property participates in queries like any other converted column: a
/// projection carries its converter and a constant beside it binds the provider representation (native
/// JSON on PostgreSQL, text when forced). No database connection is used.
/// </summary>
public class JsonColumnSqlGenerationTests
{
    [Fact]
    public void Select_JsonProperty_ShouldCarryConverterIntoMaterialization()
    {
        using var ctx = PostgresTestContext.Create();

        var cmd = ctx.From<JsonEntity>().Where(x => x.Id > 0).Select(x => x.Data);
        Prepare(ctx, cmd);

        cmd.SelectList!.Should().ContainSingle();
        cmd.SelectList[0].Converter.Should().NotBeNull();
    }

    [Fact]
    public void Select_AnonymousJsonProperty_ShouldCarryConverterIntoMaterialization()
    {
        using var ctx = PostgresTestContext.Create();

        var cmd = ctx.From<JsonEntity>().Where(x => x.Id > 0).Select(x => new { x.Data });
        Prepare(ctx, cmd);

        cmd.SelectList!.Single(c => c.PropertyName == nameof(JsonEntity.Data)).Converter.Should().NotBeNull();
    }

    [Fact]
    public void Where_WithNativeJsonPropertyConstant_ShouldBindJsonElement()
    {
        using var ctx = PostgresTestContext.Create();
        var data = new SqlGenJsonPoco { Name = "native", Values = [1, 2] };

        var prepared = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Data == data).ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("data = @");
        prepared.DbCommand.Parameters.Cast<DbParameter>().Single().Value.Should().BeOfType<JsonElement>();
    }

    [Fact]
    public void Where_WithTextJsonPropertyConstant_ShouldBindString()
    {
        using var ctx = PostgresTestContext.Create();
        var data = new SqlGenJsonPoco { Name = "text", Values = [3] };

        var prepared = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.DataText == data).ToCommand());

        prepared.DbCommand.CommandText.Should().Contain("data_text = @");
        var value = prepared.DbCommand.Parameters.Cast<DbParameter>().Single().Value;
        value.Should().BeOfType<string>();
        value.Should().NotBeNull();
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
}
