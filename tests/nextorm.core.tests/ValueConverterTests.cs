using System.Data;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public enum ProbeStatus
{
    Unknown,
    Active,
    Closed,
}

public sealed class StatusToStringConverter : ValueConverter<ProbeStatus, string>
{
    public override string? ConvertToProvider(ProbeStatus model) => model.ToString();

    public override ProbeStatus ConvertFromProvider(string? provider) => Enum.Parse<ProbeStatus>(provider!);
}

public sealed class NullableEchoConverter : ValueConverter<string, string>
{
    public override bool ConvertsNulls => true;

    public override string? ConvertToProvider(string? model) => model is null ? "NULL" : model;

    public override string? ConvertFromProvider(string? provider) => provider == "NULL" ? null : provider;
}

public sealed class AttributedConverterEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(StatusToStringConverter))]
    public ProbeStatus Status { get; set; }
}

public interface IInterfaceConverterEntity
{
    int Id { get; set; }

    [ValueConverter(typeof(StatusToStringConverter))]
    ProbeStatus Status { get; set; }
}

public sealed class InterfaceConverterEntity : IInterfaceConverterEntity
{
    public int Id { get; set; }
    public ProbeStatus Status { get; set; }
}

public sealed class MismatchedConverterEntity
{
    public int Id { get; set; }

    [ValueConverter(typeof(StatusToStringConverter))]
    public string Value { get; set; } = string.Empty;
}

public sealed class FluentConverterEntity
{
    public int Id { get; set; }
    public ProbeStatus Status { get; set; }
}

public sealed class FluentMismatchedConverterEntity
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class ValueConverterTests
{
    [Fact]
    public void Attribute_OnProperty_ShouldPopulateMetadata()
    {
        var metadata = new EntityMetadataBuilder<AttributedConverterEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(AttributedConverterEntity.Status));

        property.Converter.Should().BeOfType<StatusToStringConverter>();
        property.Converter!.ProviderType.Should().Be(typeof(string));
        property.Converter.ConvertFromProvider("Active").Should().Be(ProbeStatus.Active);
        property.Converter.ConvertToProvider(ProbeStatus.Closed).Should().Be("Closed");
    }

    [Fact]
    public void Attribute_OnImplementedInterface_ShouldApplyToClassMetadata()
    {
        var metadata = new EntityMetadataBuilder<InterfaceConverterEntity>().Build();
        var property = metadata.Properties.Single(p => p.PropertyInfo.Name == nameof(InterfaceConverterEntity.Status));

        property.Converter.Should().BeOfType<StatusToStringConverter>();
    }

    [Fact]
    public void FluentHasConversion_ShouldPopulateMetadata()
    {
        var builder = new EntityMetadataBuilder<FluentConverterEntity>();
        builder.Property(x => x.Status).HasConversion(new StatusToStringConverter());

        var property = builder.Build().Properties.Single(p => p.PropertyInfo.Name == nameof(FluentConverterEntity.Status));

        property.Converter.Should().BeOfType<StatusToStringConverter>();
    }

    [Fact]
    public void FluentHasConversionExpressions_ShouldRoundTrip()
    {
        var builder = new EntityMetadataBuilder<FluentConverterEntity>();
        builder.Property(x => x.Status).HasConversion<ProbeStatus, string>(status => status.ToString(), text => Enum.Parse<ProbeStatus>(text));

        var property = builder.Build().Properties.Single(p => p.PropertyInfo.Name == nameof(FluentConverterEntity.Status));
        var converter = property.Converter!;

        converter.ConvertToProvider(ProbeStatus.Active).Should().Be("Active");
        converter.ConvertFromProvider("Closed").Should().Be(ProbeStatus.Closed);
    }

    [Fact]
    public void MismatchedModelType_ShouldThrowWhenBuildingMetadata()
    {
        var act = () => new EntityMetadataBuilder<MismatchedConverterEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ProbeStatus*");
    }

    [Fact]
    public void FluentMismatchedModelType_ShouldThrowWhenBuildingMetadata()
    {
        var builder = new EntityMetadataBuilder<FluentMismatchedConverterEntity>();
        builder.Property(x => x.Value).HasConversion(new StatusToStringConverter());

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ProbeStatus*");
    }

    [Fact]
    public void TypedConverter_InterfaceBridge_ShouldNotRequireBoxingMethod()
    {
        IPropertyValueConverter converter = new StatusToStringConverter();

        converter.ConvertToProvider(ProbeStatus.Active).Should().Be("Active");
        converter.ConvertFromProvider("Closed").Should().Be(ProbeStatus.Closed);
        converter.ProviderType.Should().Be(typeof(string));
        converter.ConvertsNulls.Should().BeFalse();
    }

    [Fact]
    public void ConvertsNulls_ShouldHaveSingleSourceOfTruth()
    {
        IPropertyValueConverter converter = new NullableEchoConverter();

        converter.ConvertToProvider(null).Should().Be("NULL");
        converter.ConvertFromProvider(null).Should().BeNull();
        converter.ConvertsNulls.Should().BeTrue();
    }

    [Fact]
    public void SelectExpression_ShouldReadProviderTypeWhenConverterIsPresent()
    {
        var column = new SelectExpression(typeof(ProbeStatus))
        {
            Index = 0,
            Converter = new StatusToStringConverter(),
            ProviderType = typeof(string),
        };

        column.GetDataRecordMethod().Name.Should().Be(nameof(IDataRecord.GetString));
    }

    [Fact]
    public void PlanComparer_ShouldDistinguishConverterInstances()
    {
        var comparer = new SelectExpressionPlanEqualityComparer(new QueryProvider());
        var first = new SelectExpression(typeof(string)) { Index = 0, Converter = new NullableEchoConverter(), ProviderType = typeof(string) };
        var second = new SelectExpression(typeof(string)) { Index = 0, Converter = new NullableEchoConverter(), ProviderType = typeof(string) };

        comparer.Equals(first, first).Should().BeTrue();
        comparer.Equals(first, second).Should().BeFalse();
    }
}
