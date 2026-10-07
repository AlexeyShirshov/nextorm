using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

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

[SqlTable("json_object_entity")]
public interface IPgJsonObjectEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("doc")]
    JsonObject Doc { get; set; }
}

public sealed class PgStorageJsonPoco
{
    public string? Name { get; set; }
}

public sealed class PgStorageJsonEntity
{
    public int Id { get; set; }

    [JsonColumn]
    public PgStorageJsonPoco Auto { get; set; } = new();

    [JsonColumn(Storage = JsonColumnStorage.Native)]
    public PgStorageJsonPoco Native { get; set; } = new();

    [JsonColumn(Storage = JsonColumnStorage.Text)]
    public PgStorageJsonPoco Text { get; set; } = new();
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

    [Fact]
    public void Dialect_ShouldAdvertisePostgresJsonSqlSurface()
    {
        PostgresDialect.Instance.SupportsJson.Should().BeTrue();
        PostgresDialect.Instance.SupportsPostgresJsonSql.Should().BeTrue();
    }

    [Fact]
    public void PostgresJsonSql_ShouldStillGenerateJsonFunctions()
    {
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(
            ctx,
            ctx.From<JsonEntity>().Where(x => x.Id > 0)
                .Select(x => new { V = SqlFunctions.Postgres.json_agg(x.Id) }));

        prepared.DbCommand.CommandText.Should().Contain("json_agg(");
    }

    [Fact]
    public void Dialect_ShouldAdvertiseJsonElementAsNativeProviderType()
    {
        PostgresDialect.Instance.NativeJsonProviderType.Should().Be(typeof(JsonElement));
    }

    [Theory]
    [InlineData(nameof(PgStorageJsonEntity.Auto))]
    [InlineData(nameof(PgStorageJsonEntity.Native))]
    public void NativeStorage_ShouldResolveToJsonElementConverterOnPostgres(string propertyName)
    {
        var property = new EntityMetadataBuilder<PgStorageJsonEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == propertyName);

        var resolved = ((IJsonColumnConverter)property.Converter!).Resolve(PostgresDialect.Instance);

        resolved.ProviderType.Should().Be(typeof(JsonElement));
        resolved.ConvertToProvider(new PgStorageJsonPoco { Name = "x" }).Should().BeOfType<JsonElement>();
    }

    [Fact]
    public void TextStorage_ShouldStayStringOnPostgres()
    {
        var property = new EntityMetadataBuilder<PgStorageJsonEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == nameof(PgStorageJsonEntity.Text));

        ((IJsonColumnConverter)property.Converter!).Resolve(PostgresDialect.Instance).ProviderType
            .Should().Be(typeof(string));
    }

    [Fact]
    public void BareJsonObjectProjection_ShouldBeRejectedAsUnsupported()
    {
        using var ctx = PostgresTestContext.Create();
        var command = ctx.From<IPgJsonObjectEntity>().Where(x => x.Id > 0).Select(x => new { x.Doc });

        var act = () => Prepare(ctx, command);

        // JsonObject is not an Npgsql jsonb representation; PostgreSQL must keep the accessor rejection.
        act.Should().Throw<NotSupportedException>().WithMessage("*JsonObject*");
    }

    [Fact]
    public void PostgresJsonOperators_ShouldStillGenerateArrowAndContainment()
    {
        using var ctx = PostgresTestContext.Create();

        var get = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get(SqlFunctions.Parameter<JsonDocument>(0), "name") }));
        get.DbCommand.CommandText.Should().Contain("->");

        var contains = Prepare(ctx, ctx.From<JsonEntity>().Where(x => x.Id > 0)
            .Select(x => new
            {
                V = SqlFunctions.Postgres.json_contains(
                    SqlFunctions.Parameter<JsonDocument>(0), SqlFunctions.Parameter<JsonDocument>(1)),
            }));
        contains.DbCommand.CommandText.Should().Contain("@>");
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
}
