using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public sealed class BuiltInEnumConverterEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(EnumToStringConverter<ProbeStatus>))]
    public ProbeStatus Status { get; set; }
}

public sealed class NullableBuiltInEnumConverterEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(EnumToStringConverter<ProbeStatus>))]
    public ProbeStatus? Status { get; set; }
}

public class EnumToStringConverterTests
{
    [Fact]
    public void ShouldRoundTrip()
    {
        IPropertyValueConverter converter = new EnumToStringConverter<ProbeStatus>();

        converter.ProviderType.Should().Be(typeof(string));
        converter.ConvertsNulls.Should().BeFalse();
        converter.ConvertToProvider(ProbeStatus.Active).Should().Be("Active");
        converter.ConvertFromProvider("Closed").Should().Be(ProbeStatus.Closed);
    }

    [Fact]
    public void Attribute_ShouldPopulateMetadata()
    {
        var metadata = new EntityMetadataBuilder<BuiltInEnumConverterEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(BuiltInEnumConverterEntity.Status));

        property.Converter.Should().BeOfType<EnumToStringConverter<ProbeStatus>>();
        property.Converter!.ProviderType.Should().Be(typeof(string));
    }

    [Fact]
    public void Attribute_OnNullableProperty_ShouldPopulateMetadata()
    {
        var metadata = new EntityMetadataBuilder<NullableBuiltInEnumConverterEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(NullableBuiltInEnumConverterEntity.Status));

        property.Converter.Should().BeOfType<EnumToStringConverter<ProbeStatus>>();
    }

    [Fact]
    public void FluentHasConversion_ShouldPopulateMetadata()
    {
        var builder = new EntityMetadataBuilder<FluentConverterEntity>();
        builder.Property(x => x.Status).HasConversion(new EnumToStringConverter<ProbeStatus>());

        var property = builder.Build().Properties.Single(p => p.PropertyInfo.Name == nameof(FluentConverterEntity.Status));

        property.Converter.Should().BeOfType<EnumToStringConverter<ProbeStatus>>();
    }
}
