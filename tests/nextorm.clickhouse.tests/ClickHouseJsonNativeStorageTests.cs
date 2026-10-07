using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClickHouse.Driver.ADO.Parameters;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

public sealed class ChJsonPoco
{
    public string? Name { get; set; }
    public List<int> Values { get; set; } = [];
}

public sealed class ChJsonEntity
{
    public int Id { get; set; }

    [JsonColumn]
    public ChJsonPoco Auto { get; set; } = new();

    [JsonColumn(Storage = JsonColumnStorage.Native)]
    public ChJsonPoco Native { get; set; } = new();

    [JsonColumn(Storage = JsonColumnStorage.Text)]
    public ChJsonPoco Text { get; set; } = new();
}

public sealed class ChJsonArrayEntity
{
    public int Id { get; set; }

    [JsonColumn]
    public List<int> Values { get; set; } = [];
}

public sealed class ChJsonDomEntity
{
    public int Id { get; set; }

    [JsonColumn]
    public JsonObject Object { get; set; } = new();

    [JsonColumn]
    public JsonDocument Document { get; set; } = JsonDocument.Parse("{}");

    [JsonColumn]
    public JsonElement Element { get; set; }
}

/// <summary>
/// ClickHouse native <c>JSON</c> storage capability: the dialect flags, the dialect-resolved converter
/// (object-root <see cref="JsonObject"/>), the parameter normalization and the fail-closed PostgreSQL
/// JSON surface. No database connection is opened.
/// </summary>
public class ClickHouseJsonNativeStorageTests
{
    private static readonly ISqlDialect Dialect = ClickHouseDialect.Instance;

    [Fact]
    public void Dialect_ShouldAdvertiseNativeJsonStorageButNotPostgresJsonSql()
    {
        Dialect.SupportsJson.Should().BeTrue();
        Dialect.SupportsPostgresJsonSql.Should().BeFalse();
        Dialect.NativeJsonProviderType.Should().Be(typeof(JsonObject));
    }

    [Theory]
    [InlineData(nameof(ChJsonEntity.Auto))]   // Auto -> native on ClickHouse
    [InlineData(nameof(ChJsonEntity.Native))] // explicit Native
    public void NativeStorage_ShouldResolveToJsonObjectConverter(string propertyName)
    {
        var property = new EntityMetadataBuilder<ChJsonEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == propertyName);

        var resolved = ((IJsonColumnConverter)property.Converter!).Resolve(Dialect);

        resolved.ProviderType.Should().Be(typeof(JsonObject));
        resolved.ConvertToProvider(new ChJsonPoco { Name = "x", Values = [1, 2] })
            .Should().BeOfType<JsonObject>();
    }

    [Fact]
    public void TextStorage_ShouldStayStringAndNotForceNative()
    {
        var property = new EntityMetadataBuilder<ChJsonEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == nameof(ChJsonEntity.Text));

        var resolved = ((IJsonColumnConverter)property.Converter!).Resolve(Dialect);

        resolved.ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void NativeConverter_NonObjectRootModel_ShouldThrowObjectRootGuard()
    {
        var property = new EntityMetadataBuilder<ChJsonArrayEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == nameof(ChJsonArrayEntity.Values));

        var resolved = ((IJsonColumnConverter)property.Converter!).Resolve(Dialect);

        var act = () => resolved.ConvertToProvider(new List<int> { 1, 2 });

        act.Should().Throw<NotSupportedException>().WithMessage("*object-root*");
    }

    [Fact]
    public void CreateParam_ObjectRootJsonElement_ShouldBindJsonType()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        var element = JsonSerializer.SerializeToElement(new { a = 1 });

        var parameter = (ClickHouseDbParameter)ctx.CreateParam("@p", element);

        parameter.ClickHouseType.Should().Be("JSON");
        parameter.Value.Should().BeOfType<JsonObject>();
    }

    [Fact]
    public void CreateParam_ObjectRootJsonDocument_ShouldBindJsonType()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        using var document = JsonDocument.Parse("""{"a":1}""");

        var parameter = (ClickHouseDbParameter)ctx.CreateParam("@p", document);

        parameter.ClickHouseType.Should().Be("JSON");
        parameter.Value.Should().BeOfType<JsonObject>();
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void CreateParam_NonObjectJsonRoot_ShouldKeepExistingBinding(string json)
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        using var document = JsonDocument.Parse(json);

        var parameter = (ClickHouseDbParameter)ctx.CreateParam("@p", document);

        // Only an object root is normalized to native JSON; any other root must not be silently
        // rewritten into a type the server would reject.
        parameter.ClickHouseType.Should().BeNullOrEmpty();
        parameter.Value.Should().BeSameAs(document);
    }

    [Fact]
    public void PostgresJsonSql_ShouldBeRejectedOnClickHouse()
    {
        using var ctx = ClickHouseTestContext.Create();
        var command = ctx.From<IJsonObjectEntity>()
            .Select(x => new { V = SqlFunctions.Postgres.json_agg(x.Id) });

        var act = () => SqlOf(ctx, command);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void PostgresJsonOperatorArrow_ShouldBeRejectedOnClickHouse()
    {
        using var ctx = ClickHouseTestContext.Create();
        var command = ctx.From<IJsonObjectEntity>()
            .Select(x => new { V = SqlFunctions.Postgres.json_get(x.Doc, "name") });

        var act = () => SqlOf(ctx, command);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void PostgresJsonOperatorContains_ShouldBeRejectedOnClickHouse()
    {
        using var ctx = ClickHouseTestContext.Create();
        var command = ctx.From<IJsonObjectEntity>()
            .Select(x => new { V = SqlFunctions.Postgres.json_contains(x.Doc, "name") });

        var act = () => SqlOf(ctx, command);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateParam_ObjectRootDocument_ShouldNotRetainCallerDocumentLifetime()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();

        JsonDocument document = JsonDocument.Parse("""{"a":1}""");
        var fromDocument = (ClickHouseDbParameter)ctx.CreateParam("@d", document);
        var fromElement = (ClickHouseDbParameter)ctx.CreateParam("@e", document.RootElement);
        document.Dispose();

        // The bound node tree must be self-contained: touching it after the caller disposed the source
        // document must not throw ObjectDisposedException (parameter binding happens before execution,
        // the caller may dispose the document as soon as CreateParam returns).
        var documentAct = () => ((JsonObject)fromDocument.Value!).ToJsonString();
        documentAct.Should().NotThrow();
        ((JsonObject)fromDocument.Value!)["a"]!.GetValue<int>().Should().Be(1);

        var elementAct = () => ((JsonObject)fromElement.Value!).ToJsonString();
        elementAct.Should().NotThrow();
        ((JsonObject)fromElement.Value!)["a"]!.GetValue<int>().Should().Be(1);
    }

    private static IPropertyValueConverter ResolveProperty(string name)
        => ((IJsonColumnConverter)new EntityMetadataBuilder<ChJsonDomEntity>().Build()
            .Properties.Single(p => p.PropertyInfo.Name == name).Converter!).Resolve(Dialect);

    [Fact]
    public void AttributedDomProperties_ShouldResolveToJsonObjectConverter()
    {
        foreach (var name in new[]
        {
            nameof(ChJsonDomEntity.Object),
            nameof(ChJsonDomEntity.Document),
            nameof(ChJsonDomEntity.Element),
        })
        {
            ResolveProperty(name).ProviderType.Should().Be(typeof(JsonObject));
        }
    }

    [Fact]
    public void AttributedJsonObjectModel_ShouldRoundTripThroughNativeObject()
    {
        var resolved = ResolveProperty(nameof(ChJsonDomEntity.Object));

        var provider = resolved.ConvertToProvider(new JsonObject { ["a"] = 1 });

        provider.Should().BeOfType<JsonObject>();
        ((JsonObject)provider!)["a"]!.GetValue<int>().Should().Be(1);
        resolved.ConvertFromProvider(provider).Should().BeOfType<JsonObject>()
            .Which["a"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public void AttributedJsonElementModel_ShouldRoundTripThroughNativeObject()
    {
        var resolved = ResolveProperty(nameof(ChJsonDomEntity.Element));
        var element = JsonSerializer.SerializeToElement(new { a = 1 });

        var provider = resolved.ConvertToProvider(element);

        provider.Should().BeOfType<JsonObject>();
        ((JsonElement)resolved.ConvertFromProvider(provider)!).GetProperty("a").GetInt32().Should().Be(1);
    }

    [Fact]
    public void AttributedJsonDocumentModel_ShouldRoundTripThroughNativeObject()
    {
        var resolved = ResolveProperty(nameof(ChJsonDomEntity.Document));
        using var document = JsonDocument.Parse("""{"a":1}""");

        var provider = resolved.ConvertToProvider(document);

        provider.Should().BeOfType<JsonObject>();
        ((JsonObject)provider!)["a"]!.GetValue<int>().Should().Be(1);

        var back = resolved.ConvertFromProvider(provider);
        back.Should().BeOfType<JsonDocument>();
        ((JsonDocument)back!).RootElement.GetProperty("a").GetInt32().Should().Be(1);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText;
}
