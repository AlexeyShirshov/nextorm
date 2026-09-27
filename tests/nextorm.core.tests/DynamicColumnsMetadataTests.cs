using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public sealed class ObjectStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public object Extra { get; set; } = new();
}

public sealed class DictionaryStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

public sealed class InterfaceDictionaryStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public IDictionary<string, object?> Extra { get; set; } = new Dictionary<string, object?>();
}

public sealed class ReadOnlyDictionaryStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public IReadOnlyDictionary<string, object?> Extra { get; set; } = new Dictionary<string, object?>();
}

/// <summary>A concrete dictionary shape the metadata layer accepts through <see cref="IDictionary{TKey,TValue}"/>.</summary>
public sealed class ConcreteAssignableStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public SortedDictionary<string, object?> Extra { get; set; } = new();
}

public sealed class PrivateSetterStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; private set; } = new();
}

public sealed class TwoStoresEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> First { get; set; } = new();

    [DynamicColumns]
    public Dictionary<string, object?> Second { get; set; } = new();
}

public sealed class AttributeAndFluentStoreEntity
{
    public int Id { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

public sealed class FluentStoreEntity
{
    public int Id { get; set; }

    public Dictionary<string, object?> Extra { get; set; } = new();
}

/// <summary>
/// Unit tests for the dynamic-columns store metadata (see <see cref="DynamicColumnsAttribute"/>): the
/// accepted dictionary shapes, the rejected store types/setters, the single-store rule and the
/// attribute/fluent clash. No database is involved.
/// </summary>
public class DynamicColumnsMetadataTests
{
    [Fact]
    public void Store_WithObjectType_ShouldThrow()
    {
        Action act = () => new EntityMetadataBuilder<ObjectStoreEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Extra*string-keyed dictionary*");
    }

    [Fact]
    public void Store_WithSupportedShapes_ShouldBeAcceptedAndExcludedFromProperties()
    {
        var dictionary = new EntityMetadataBuilder<DictionaryStoreEntity>().Build();
        var interfaceDictionary = new EntityMetadataBuilder<InterfaceDictionaryStoreEntity>().Build();
        var readOnlyDictionary = new EntityMetadataBuilder<ReadOnlyDictionaryStoreEntity>().Build();
        var concrete = new EntityMetadataBuilder<ConcreteAssignableStoreEntity>().Build();

        dictionary.DynamicColumnsStore.Should().NotBeNull();
        interfaceDictionary.DynamicColumnsStore.Should().NotBeNull();
        readOnlyDictionary.DynamicColumnsStore.Should().NotBeNull();
        concrete.DynamicColumnsStore.Should().NotBeNull();

        // The store is not a column mapping: it is exposed separately and excluded from Properties.
        dictionary.Properties.Should().ContainSingle(p => p.PropertyInfo.Name == nameof(DictionaryStoreEntity.Id));
        interfaceDictionary.Properties.Should().ContainSingle(p => p.PropertyInfo.Name == nameof(InterfaceDictionaryStoreEntity.Id));
        readOnlyDictionary.Properties.Should().ContainSingle(p => p.PropertyInfo.Name == nameof(ReadOnlyDictionaryStoreEntity.Id));
        concrete.Properties.Should().ContainSingle(p => p.PropertyInfo.Name == nameof(ConcreteAssignableStoreEntity.Id));

        dictionary.DynamicColumnsStore!.PropertyInfo.Name.Should().Be(nameof(DictionaryStoreEntity.Extra));
    }

    [Fact]
    public void Store_WithNonPublicSetter_ShouldThrow()
    {
        Action act = () => new EntityMetadataBuilder<PrivateSetterStoreEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Extra*public setter*");
    }

    [Fact]
    public void TwoStores_ShouldThrowConfigurationException()
    {
        Action act = () => new EntityMetadataBuilder<TwoStoresEntity>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one dynamic-columns store*");
    }

    [Fact]
    public void Store_DeclaredWithAttributeAndFluentMapping_ShouldThrow()
    {
        var builder = new EntityMetadataBuilder<AttributeAndFluentStoreEntity>();
        _ = builder.Property(x => x.Extra).HasColumnName("extra");

        Action act = () => builder.Build();

        // The attribute store must not be silently dropped by the fluent mapping of the same property.
        act.Should().Throw<InvalidOperationException>().WithMessage("*Extra*fluent column mapping*");
    }

    [Fact]
    public void FluentStore_WithColumnName_ShouldThrow() => AssertFluentClash(b => b.HasColumnName("extra"));

    [Fact]
    public void FluentStore_WithKey_ShouldThrow() => AssertFluentClash(b => b.Key());

    [Fact]
    public void FluentStore_WithIdentity_ShouldThrow() => AssertFluentClash(b => b.Identity());

    [Fact]
    public void FluentStore_WithComputed_ShouldThrow() => AssertFluentClash(b => b.Computed());

    [Fact]
    public void FluentStore_WithCollation_ShouldThrow() => AssertFluentClash(b => b.Collation("NOCASE"));

    [Fact]
    public void FluentStore_WithDecimalPrecision_ShouldThrow() => AssertFluentClash(b => b.DecimalPrecision(10, 2));

    private static void AssertFluentClash(
        Func<EntityPropertyBuilder<FluentStoreEntity>, EntityPropertyBuilder<FluentStoreEntity>> configure)
    {
        var builder = new EntityMetadataBuilder<FluentStoreEntity>();
        _ = configure(builder.Property(x => x.Extra).DynamicColumnsStore());

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Extra*cannot also declare a column mapping*");
    }
}
