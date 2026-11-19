using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json.Serialization;
using NextORM.Core;

namespace NextORM.AotScenarios;

[SqlTable("aot_converter_entity")]
public sealed class AotConverterEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("amount")]
    [ValueConverter(typeof(AotAmountConverter))]
    public decimal Amount { get; set; }

    [Column("payload")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    public AotJsonPayload? Payload { get; set; }
}

/// <summary>
/// The model stored in the JSON column. Its contract metadata comes from <see cref="AotJsonContext"/>: a
/// trimmed application keeps no reflective contract metadata, so the compile-time entity description hands
/// the context's options to the converter.
/// </summary>
public sealed class AotJsonPayload
{
    public string? Name { get; set; }

    public int Value { get; set; }
}

/// <summary>
/// Stores a decimal as <c>"v=" + invariant text</c>, so a mapping that skipped the converter reads back a
/// different number rather than the stored text parsed as a decimal.
/// </summary>
public sealed class AotAmountConverter : ValueConverter<decimal, string>
{
    public override string? ConvertToProvider(decimal model)
        => "v=" + model.ToString(CultureInfo.InvariantCulture);

    public override decimal ConvertFromProvider(string? provider)
        => provider is null || provider.Length <= 2
            ? 0m
            : decimal.Parse(provider.Substring(2), CultureInfo.InvariantCulture);
}

[JsonSerializable(typeof(AotJsonPayload))]
internal sealed partial class AotJsonContext : JsonSerializerContext;
