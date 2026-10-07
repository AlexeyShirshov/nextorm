using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public sealed class ProbePoco
{
    public string? Name { get; set; }
    public List<int> Values { get; set; } = [];
}

public sealed class JsonAttributedEntity
{
    public int Id { get; set; }

    [JsonColumn]
    public ProbePoco Data { get; set; } = new();

    [JsonColumn(Storage = JsonColumnStorage.Native)]
    public ProbePoco Native { get; set; } = new();
}

public interface IJsonInterfaceEntity
{
    int Id { get; set; }

    [JsonColumn]
    ProbePoco Data { get; set; }
}

public sealed class JsonInterfaceEntity : IJsonInterfaceEntity
{
    public int Id { get; set; }
    public ProbePoco Data { get; set; } = new();
}

public sealed class JsonFluentEntity
{
    public int Id { get; set; }
    public ProbePoco Data { get; set; } = new();
}

public class JsonColumnTests
{
    private static ProbePoco Sample() => new() { Name = "value", Values = [1, 2, 3] };

    [Fact]
    public void Attribute_Auto_ShouldPopulateTextConverter()
    {
        var metadata = new EntityMetadataBuilder<JsonAttributedEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(JsonAttributedEntity.Data));

        property.Converter.Should().NotBeNull();
        property.Converter!.ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Attribute_Native_ShouldPopulateElementConverter()
    {
        var metadata = new EntityMetadataBuilder<JsonAttributedEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(JsonAttributedEntity.Native));

        property.Converter!.ProviderType.Should().Be(typeof(JsonElement));
    }

    [Fact]
    public void Attribute_Auto_ShouldRoundTripThroughText()
    {
        var metadata = new EntityMetadataBuilder<JsonAttributedEntity>().Build();
        var converter = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(JsonAttributedEntity.Data)).Converter!;

        var provider = converter.ConvertToProvider(Sample());

        provider.Should().BeOfType<string>();
        var back = converter.ConvertFromProvider(provider).Should().BeOfType<ProbePoco>().Subject;
        back.Name.Should().Be("value");
        back.Values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Attribute_Native_ShouldRoundTripThroughElement()
    {
        var metadata = new EntityMetadataBuilder<JsonAttributedEntity>().Build();
        var converter = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(JsonAttributedEntity.Native)).Converter!;

        var element = (JsonElement)converter.ConvertToProvider(Sample())!;

        var back = converter.ConvertFromProvider(element).Should().BeOfType<ProbePoco>().Subject;
        back.Values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Attribute_OnImplementedInterface_ShouldApplyToClassMetadata()
    {
        var metadata = new EntityMetadataBuilder<JsonInterfaceEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(JsonInterfaceEntity.Data));

        property.Converter.Should().NotBeNull();
        property.Converter!.ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void FluentJsonColumn_ShouldPopulateConverter()
    {
        var builder = new EntityMetadataBuilder<JsonFluentEntity>();
        builder.Property(x => x.Data).JsonColumn(options => options.Storage = JsonColumnStorage.Text);

        var property = builder.Build().Properties.Single(p => p.PropertyInfo.Name == nameof(JsonFluentEntity.Data));

        property.Converter.Should().NotBeNull();
        property.Converter!.ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void TextConverter_ShouldRoundTrip()
    {
        var converter = new JsonColumnConverter<ProbePoco, string>();

        var text = converter.ConvertToProvider(Sample());
        var back = converter.ConvertFromProvider(text);

        back!.Values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ElementConverter_ShouldRoundTrip()
    {
        var converter = new JsonColumnConverter<ProbePoco, JsonElement>();

        var element = converter.ConvertToProvider(Sample());
        var back = converter.ConvertFromProvider(element);

        back!.Name.Should().Be("value");
    }

    [Fact]
    public void Converter_ShouldHonorSerializerOptions()
    {
        var options = new JsonColumnOptions
        {
            Storage = JsonColumnStorage.Text,
            Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
        };
        var converter = new JsonColumnConverter<ProbePoco, string>(options);

        converter.ConvertToProvider(Sample()).Should().Contain("\"name\"");
    }

    [Fact]
    public void PlanComparer_ShouldDistinguishConvertersWithDifferentOptions()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = new SelectExpression(typeof(string))
        {
            Index = 0,
            ProviderType = typeof(string),
            Converter = new JsonColumnConverter<ProbePoco, string>(new JsonColumnOptions { Storage = JsonColumnStorage.Text }),
        };
        var second = new SelectExpression(typeof(string))
        {
            Index = 0,
            ProviderType = typeof(string),
            Converter = new JsonColumnConverter<ProbePoco, string>(new JsonColumnOptions { Storage = JsonColumnStorage.Text }),
        };

        comparer.Equals(first, first).Should().BeTrue();
        comparer.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void ObjectConverter_ShouldRoundTripThroughJsonObject()
    {
        var converter = new JsonColumnConverter<ProbePoco, JsonObject>();

        var obj = converter.ConvertToProvider(Sample()).Should().BeOfType<JsonObject>().Subject;
        // No options => default serializer naming, i.e. the declared PascalCase property name.
        obj["Name"]!.GetValue<string>().Should().Be("value");

        var back = converter.ConvertFromProvider(obj).Should().BeOfType<ProbePoco>().Subject;
        back.Values.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ObjectConverter_NullModel_ShouldRoundTripNull()
    {
        var converter = new JsonColumnConverter<ProbePoco, JsonObject>();

        converter.ConvertToProvider(null).Should().BeNull();
        converter.ConvertFromProvider(null).Should().BeNull();
    }

    [Fact]
    public void ObjectConverter_NonObjectRoot_ShouldThrowObjectRootGuard()
    {
        // A JSON array/primitive root cannot be stored in ClickHouse's object-rooted native JSON.
        var converter = new JsonColumnConverter<List<int>, JsonObject>();

        var act = () => converter.ConvertToProvider([1, 2, 3]);

        act.Should().Throw<NotSupportedException>().WithMessage("*object-root*");
    }

    [Fact]
    public void ObjectConverter_UndefinedElement_ShouldThrowObjectRootGuard()
    {
        var converter = new JsonColumnConverter<JsonElement, JsonObject>();

        var act = () => converter.ConvertToProvider(default);

        act.Should().Throw<NotSupportedException>().WithMessage("*object-root*");
    }

    [Fact]
    public void ObjectConverter_JsonNullRoot_ShouldThrowObjectRootGuard()
    {
        // A JSON-literal null root is not a JSON object either; it must hit the same guard rather than
        // being stored as an object.
        var converter = new JsonColumnConverter<JsonElement, JsonObject>();
        using var document = JsonDocument.Parse("null");

        var act = () => converter.ConvertToProvider(document.RootElement);

        act.Should().Throw<NotSupportedException>().WithMessage("*object-root*");
    }

    [Fact]
    public void ObjectConverter_ShouldHonorSerializerOptions()
    {
        var options = new JsonColumnOptions
        {
            Storage = JsonColumnStorage.Native,
            Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
        };
        var converter = new JsonColumnConverter<ProbePoco, JsonObject>(options);

        var obj = converter.ConvertToProvider(Sample()).Should().BeOfType<JsonObject>().Subject;

        obj["name"]!.GetValue<string>().Should().Be("value");
    }

    [Fact]
    public void PlanComparer_ShouldDistinguishJsonObjectConvertersWithDifferentOptions()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var snake = new JsonColumnConverter<ProbePoco, JsonObject>(new JsonColumnOptions
        {
            Storage = JsonColumnStorage.Native,
            Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
        });
        var defaultOptions = new JsonColumnConverter<ProbePoco, JsonObject>();

        var first = new SelectExpression(typeof(ProbePoco))
        {
            Index = 0,
            ProviderType = typeof(JsonObject),
            Converter = snake,
        };
        var sameConverterInstance = new SelectExpression(typeof(ProbePoco))
        {
            Index = 0,
            ProviderType = typeof(JsonObject),
            Converter = snake,
        };
        var differentOptions = new SelectExpression(typeof(ProbePoco))
        {
            Index = 0,
            ProviderType = typeof(JsonObject),
            Converter = defaultOptions,
        };

        // The same converter instance (as reused by the cached property metadata) keeps the plan identical...
        comparer.Equals(first, sameConverterInstance).Should().BeTrue();
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(sameConverterInstance));

        // ...while a converter carrying different serializer options is a distinct plan entry.
        comparer.Equals(first, differentOptions).Should().BeFalse();

        // And the difference is real: the options change the serialized property name.
        snake.ConvertToProvider(Sample()).Should().BeOfType<JsonObject>()
            .Which["name"]!.GetValue<string>().Should().Be("value");
        defaultOptions.ConvertToProvider(Sample()).Should().BeOfType<JsonObject>()
            .Which["Name"]!.GetValue<string>().Should().Be("value");
    }
}
