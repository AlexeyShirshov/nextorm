using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public sealed class AttributedDecimalEntity
{
    public int Id { get; set; }

    [DecimalPrecision(12, 4)]
    public decimal Amount { get; set; }

    public decimal Plain { get; set; }
}

public sealed class FluentDecimalEntity
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public sealed class NonDecimalAnnotationEntity
{
    public int Id { get; set; }

    [DecimalPrecision(12, 4)]
    public int Count { get; set; }
}

public sealed class BoundaryDecimalEntity
{
    public int Id { get; set; }

    [DecimalPrecision(1, 0)]
    public decimal Min { get; set; }

    [DecimalPrecision(38, 38)]
    public decimal Max { get; set; }
}

public sealed class NegativeScaleDecimalEntity
{
    public int Id { get; set; }

    [DecimalPrecision(10, -1)]
    public decimal Amount { get; set; }
}

/// <summary>Maps a non-decimal model (<see cref="string"/>) to the decimal provider representation.</summary>
public sealed class StringToDecimalConverter : ValueConverter<string, decimal>
{
    public override decimal ConvertToProvider(string? model) => decimal.Parse(model!, CultureInfo.InvariantCulture);

    public override string? ConvertFromProvider(decimal provider) => provider.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Maps a decimal model to the string provider representation, so a declared pair is meaningless.</summary>
public sealed class DecimalToStringConverter : ValueConverter<decimal, string>
{
    public override string? ConvertToProvider(decimal model) => model.ToString(CultureInfo.InvariantCulture);

    public override decimal ConvertFromProvider(string? provider) => decimal.Parse(provider!, CultureInfo.InvariantCulture);
}

public sealed class ConvertedDecimalEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(StringToDecimalConverter))]
    [DecimalPrecision(12, 4)]
    public string Amount { get; set; } = string.Empty;
}

public sealed class DecimalToTextEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(DecimalToStringConverter))]
    [DecimalPrecision(12, 4)]
    public decimal Amount { get; set; }
}

public sealed class DecimalJsonEntity
{
    public int Id { get; set; }

    [JsonColumn]
    [DecimalPrecision(12, 4)]
    public decimal Amount { get; set; }
}

public sealed class FluentConvertedDecimalEntity
{
    public int Id { get; set; }
    public string Amount { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for the decimal precision/scale mapping metadata (<see cref="IPropertyMetadata.DecimalPrecision"/>/
/// <see cref="IPropertyMetadata.DecimalScale"/>), its attribute and fluent sources, and the rejection of the
/// annotation on a non-decimal property. No database is involved.
/// </summary>
public class DecimalMetadataTests
{
    [Fact]
    public void DecimalPrecisionAttribute_ShouldSetPrecisionAndScale()
    {
        var metadata = new EntityMetadataBuilder<AttributedDecimalEntity>().Build();

        var amount = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedDecimalEntity.Amount));
        var plain = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedDecimalEntity.Plain));

        amount.DecimalPrecision.Should().Be(12);
        amount.DecimalScale.Should().Be(4);

        // A decimal property without the attribute stays unconfigured: the provider keeps its default
        // (SQL Server (38, 18), ClickHouse (38, 10)).
        plain.DecimalPrecision.Should().BeNull();
        plain.DecimalScale.Should().BeNull();
    }

    [Fact]
    public void FluentDecimalPrecision_ShouldSetPrecisionAndScale()
    {
        var builder = new EntityMetadataBuilder<FluentDecimalEntity>();
        _ = builder.Property(x => x.Amount).DecimalPrecision(12, 4).HasColumnName("Amount");
        var metadata = builder.Build();

        var amount = metadata.Properties.Single();
        amount.DecimalPrecision.Should().Be(12);
        amount.DecimalScale.Should().Be(4);
    }

    [Fact]
    public void DecimalPrecisionAttribute_OnNonDecimalProperty_ShouldThrow()
    {
        Action act = () => new EntityMetadataBuilder<NonDecimalAnnotationEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Count*");
    }

    [Fact]
    public void DecimalPrecisionAttribute_AtDomainBoundaries_ShouldSetPrecisionAndScale()
    {
        var metadata = new EntityMetadataBuilder<BoundaryDecimalEntity>().Build();

        var min = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(BoundaryDecimalEntity.Min));
        var max = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(BoundaryDecimalEntity.Max));

        // The inclusive domain edges (1,0) and (38,38) are valid.
        min.DecimalPrecision.Should().Be(1);
        min.DecimalScale.Should().Be(0);
        max.DecimalPrecision.Should().Be(38);
        max.DecimalScale.Should().Be(38);
    }

    [Fact]
    public void DecimalPrecisionAttribute_WithNegativeScale_ShouldThrow()
    {
        Action act = () => new EntityMetadataBuilder<NegativeScaleDecimalEntity>().Build();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("scale");
    }

    [Fact]
    public void FluentDecimalPrecision_WithInvalidScale_ShouldThrowOnBuild()
    {
        var builder = new EntityMetadataBuilder<FluentDecimalEntity>();
        _ = builder.Property(x => x.Amount).DecimalPrecision(10, 11);

        // The fluent setter only records the pair; the range is validated when the mapping is built.
        Action act = () => builder.Build();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("scale");
    }

    [Fact]
    public void DecimalPrecisionAttribute_OnNonDecimalModelConvertedToDecimal_ShouldSetPrecisionAndScale()
    {
        // The bound provider type is decimal (through the converter), so the pair is valid even though
        // the model property is a string.
        var metadata = new EntityMetadataBuilder<ConvertedDecimalEntity>().Build();

        var amount = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(ConvertedDecimalEntity.Amount));
        amount.DecimalPrecision.Should().Be(12);
        amount.DecimalScale.Should().Be(4);
    }

    [Fact]
    public void DecimalPrecisionAttribute_OnDecimalModelConvertedToText_ShouldThrow()
    {
        // The converter owns the provider representation: the bound column is text, so the decimal pair
        // is rejected instead of silently ignored.
        Action act = () => new EntityMetadataBuilder<DecimalToTextEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Amount*");
    }

    [Fact]
    public void DecimalPrecisionAttribute_OnDecimalModelMappedToJsonColumn_ShouldThrow()
    {
        Action act = () => new EntityMetadataBuilder<DecimalJsonEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Amount*");
    }

    [Fact]
    public void FluentDecimalPrecision_OnNonDecimalModelConvertedToDecimal_ShouldSetPrecisionAndScale()
    {
        var builder = new EntityMetadataBuilder<FluentConvertedDecimalEntity>();
        _ = builder.Property(x => x.Amount).DecimalPrecision(12, 4).HasConversion(new StringToDecimalConverter());
        var metadata = builder.Build();

        var amount = metadata.Properties.Single();
        amount.DecimalPrecision.Should().Be(12);
        amount.DecimalScale.Should().Be(4);
    }

    [Fact]
    public void FluentDecimalPrecision_OnDecimalModelConvertedToText_ShouldThrowOnBuild()
    {
        var builder = new EntityMetadataBuilder<FluentDecimalEntity>();
        _ = builder.Property(x => x.Amount).DecimalPrecision(12, 4).HasConversion(new DecimalToStringConverter());

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Amount*");
    }

    [Fact]
    public void FluentDecimalPrecision_OnDecimalModelMappedToJsonColumn_ShouldThrowOnBuild()
    {
        var builder = new EntityMetadataBuilder<FluentDecimalEntity>();
        _ = builder.Property(x => x.Amount).DecimalPrecision(12, 4).JsonColumn();

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Amount*");
    }
}
