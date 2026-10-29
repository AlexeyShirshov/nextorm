using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// T325 (#325) — MySQL's capability decision. MySQL 8 has a genuine server <c>json</c> type
/// (<c>information_schema.COLUMNS.DATA_TYPE = json</c>), but the provider has no bindable JSON CLR
/// representation: MySqlConnector rejects <see cref="JsonElement"/>/<see cref="JsonDocument"/>/
/// <see cref="System.Text.Json.Nodes.JsonNode"/> parameter values and always reads a <c>json</c> column
/// back as <see cref="string"/>. A flag that cannot be honoured is refused, so <c>SupportsJson</c> stays
/// <c>false</c> and a real <c>json</c> column is used through the text transport.
/// </summary>
public class MySqlJsonColumnCapabilityTests
{
    private static readonly ISqlDialect Dialect = MySqlDialect.Instance;

    [Fact]
    public void SupportsJson_IsFalse_BecauseTheProviderHasNoNativeJsonRepresentation()
        => Dialect.SupportsJson.Should().BeFalse();

    [Fact]
    public void Auto_ShouldResolveTheTextConverter()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MySqlJsonCapabilityPoco), JsonColumnStorage.Auto);

        var resolved = ((IJsonColumnConverter)converter).Resolve(Dialect);

        resolved.ProviderType.Should().Be(typeof(string));
        resolved.ConvertToProvider(new MySqlJsonCapabilityPoco { Name = "value" })
            .Should().BeOfType<string>()
            .Which.Should().Be("{\"Name\":\"value\"}");
    }

    [Fact]
    public void Text_ShouldResolveTheTextConverter()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MySqlJsonCapabilityPoco), JsonColumnStorage.Text);

        ((IJsonColumnConverter)converter).Resolve(Dialect).ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Native_ShouldBeRefusedBeforeAnyCommandRuns()
    {
        var converter = JsonColumnConverterFactory.Create(typeof(MySqlJsonCapabilityPoco), JsonColumnStorage.Native);

        // The refusal happens in converter resolution — no DataContext, connection or command is involved,
        // so no statement can have been executed when this throws.
        var act = () => ((IJsonColumnConverter)converter).Resolve(Dialect);

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(MySqlDialect)} does not support a native JSON column*")
            .WithMessage($"*{nameof(JsonColumnStorage.Text)}*");
    }

    public sealed class MySqlJsonCapabilityPoco
    {
        public string? Name { get; set; }
    }
}
