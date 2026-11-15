using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Metadata;

/// <summary>
/// The generated entity description must be indistinguishable from the reflective auto-build it replaces:
/// same table, same columns in the same order, same key/identity/computed flags. A divergence here is a
/// behavioural change in SQL generation, so these tests compare the two mechanisms field by field.
/// </summary>
public sealed class GeneratedEntityMetadataTests
{
    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnAttributedEntity()
        => AssertSameMetadata<GenPerson>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnIdentityKey()
        => AssertSameMetadata<GenIdentity>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnInterfaceEntity()
        => AssertSameMetadata<IGenPersonView>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForTypedColumnAttributes()
        => AssertSameMetadata<GenTypedColumns>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForQueryFilters()
        => AssertSameMetadata<GenFiltered>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForARangeColumnPair()
        => AssertSameMetadata<GenRangeColumns>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAValueConverter()
        => AssertSameMetadata<GenConverted>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAJsonColumn()
        => AssertSameMetadata<GenJsonMapped>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnInterfaceDeclaredMapping()
        => AssertSameMetadata<GenInterfaceMapped>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_WhenOnlyTheInterfaceMapsTheTable()
        => AssertSameMetadata<GenInterfaceOnlyEntity>();

    [Fact]
    public void GeneratedConverter_ShouldBeConstructedFromGeneratedCode()
    {
        GeneratedEntityMetadata.TryGet(typeof(GenConverted), out var factory).Should().BeTrue();

        var converter = factory().Properties.Single(property => property.PropertyInfo.Name == nameof(GenConverted.Amount)).Converter;

        converter.Should().BeOfType<GenMoneyConverter>("the generator emits `new GenMoneyConverter()` instead of Activator.CreateInstance");
        converter!.ProviderType.Should().Be(typeof(string));
        converter.ConvertToProvider(12.5m).Should().Be("12.5");
        converter.ConvertFromProvider("12.5").Should().Be(12.5m);
    }

    [Fact]
    public void GeneratedDescription_ShouldRejectAPrecisionOnANonDecimalColumn_LikeTheReflectiveBuild()
    {
        GeneratedEntityMetadata.TryGet(typeof(GenBadPrecision), out var factory).Should().BeTrue();

        var generated = FluentActions.Invoking(() => factory());

        generated.Should().Throw<InvalidOperationException>(
            "a declared precision/scale pair is only valid on a decimal column, exactly as the reflective build enforces");
    }

    [Fact]
    public void GeneratedDescription_ShouldApplyTheKeyConvention_WhenNoKeyIsDeclared()
    {
        GeneratedEntityMetadata.TryGet(typeof(GenNoKey), out var factory).Should().BeTrue(
            "the generator covers an attributed entity even without a declared key");

        var generated = factory();
        var reflected = new EntityMetadataBuilder<GenNoKey>().AutoBuild();

        generated.Properties.Single(property => property.IsKey).PropertyInfo.Name.Should().Be(
            reflected.Properties.Single(property => property.IsKey).PropertyInfo.Name);
    }

    [Fact]
    public void GeneratedJsonConverter_ShouldUseTheOptionsOfTheSourceGeneratedContext()
    {
        GeneratedEntityMetadata.TryGet(typeof(GenJsonMapped), out var factory).Should().BeTrue();

        var generated = factory().Properties.Single(property => property.PropertyInfo.Name == nameof(GenJsonMapped.Payload)).Converter;
        var reflected = new EntityMetadataBuilder<GenJsonMapped>().AutoBuild()
            .Properties.Single(property => property.PropertyInfo.Name == nameof(GenJsonMapped.Payload)).Converter;

        // The payload context renames properties to snake case, so the document proves which options the
        // converter actually serializes with.
        ((string)generated!.ConvertToProvider(new GenPerson { Id = 1, Name = "x", Age = 2 })!)
            .Should().Be("""{"id":1,"name":"x","age":2}""");
        ((string)reflected!.ConvertToProvider(new GenPerson { Id = 1, Name = "x", Age = 2 })!)
            .Should().Contain("\"Name\"", "the reflective path declares no options and keeps the default naming");
    }

    [Fact]
    public void TypeWithoutMappingAttribute_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(UnmappedDto), out _).Should().BeFalse(
            "a settable class with no mapping attribute is not necessarily an entity");

    [Fact]
    public void TypeWithARelationshipOnAGetOnlyNavigation_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(GenRelationshipParent), out _).Should().BeFalse(
            "a relationship is metadata the generated description does not carry, and a get-only navigation is not a column");

    [Fact]
    public void TypeWithAnUnmodelledAttribute_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(NotMappedEntity), out _).Should().BeFalse(
            "an attribute the generator does not reproduce keeps the reflective path");

    [Fact]
    public void TypeWithConflictingConverters_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(ConflictingConverters), out _).Should().BeFalse(
            "the reflective build refuses a value converter combined with a JSON column, so the generated description must not pick one");

    private static void AssertSameMetadata<T>()
    {
        GeneratedEntityMetadata.TryGet(typeof(T), out var factory).Should().BeTrue(
            $"the generator must describe {typeof(T).Name}");

        var generated = factory();
        var reflected = new EntityMetadataBuilder<T>().AutoBuild();

        generated.TableName.Should().Be(reflected.TableName);
        generated.Schema.Should().Be(reflected.Schema);
        generated.IsTableNameAuto.Should().Be(reflected.IsTableNameAuto);
        generated.Properties.Should().HaveCount(reflected.Properties.Count);

        for (var i = 0; i < reflected.Properties.Count; i++)
        {
            var expected = reflected.Properties[i];
            var actual = generated.Properties[i];

            actual.PropertyInfo.Name.Should().Be(expected.PropertyInfo.Name, $"column #{i} must keep the reflective order");
            actual.ColumnName.Should().Be(expected.ColumnName);
            actual.IsColumnNameAuto.Should().Be(expected.IsColumnNameAuto);
            actual.IsKey.Should().Be(expected.IsKey);
            actual.IsIdentity.Should().Be(expected.IsIdentity);
            actual.IsComputed.Should().Be(expected.IsComputed);
            actual.DecimalPrecision.Should().Be(expected.DecimalPrecision);
            actual.DecimalScale.Should().Be(expected.DecimalScale);
            actual.Collation.Should().Be(expected.Collation);
            actual.DurationUnit.Should().Be(expected.DurationUnit);
            actual.DurationPrecision.Should().Be(expected.DurationPrecision);

            if (expected.RangeColumns is null)
            {
                actual.RangeColumns.Should().BeNull();
            }
            else
            {
                actual.RangeColumns.Should().NotBeNull();
                actual.RangeColumns!.LowerColumn.Should().Be(expected.RangeColumns.LowerColumn);
                actual.RangeColumns.UpperColumn.Should().Be(expected.RangeColumns.UpperColumn);
                actual.RangeColumns.LowerInclusive.Should().Be(expected.RangeColumns.LowerInclusive);
                actual.RangeColumns.UpperInclusive.Should().Be(expected.RangeColumns.UpperInclusive);
            }

            (actual.Converter is null).Should().Be(expected.Converter is null, $"column #{i} must declare the same converter");
            actual.Converter?.GetType().Should().Be(expected.Converter?.GetType());
            actual.Converter?.ProviderType.Should().Be(expected.Converter?.ProviderType);
        }

        generated.Filters.Should().HaveCount(reflected.Filters.Count);
        for (var i = 0; i < reflected.Filters.Count; i++)
        {
            var expectedFilter = reflected.Filters[i];
            var actualFilter = generated.Filters[i];

            actualFilter.Key.Should().Be(expectedFilter.Key);
            // The predicate is compared by its expression text rather than by reference: a filter declared
            // as a computed property builds a new expression on every read.
            (actualFilter.Lambda is null).Should().Be(expectedFilter.Lambda is null, $"filter #{i} must carry the same declaration form");
            actualFilter.Lambda?.ToString().Should().Be(expectedFilter.Lambda?.ToString());
            (actualFilter.Func is null).Should().Be(expectedFilter.Func is null, $"filter #{i} must carry the same declaration form");
            actualFilter.Func?.Method.Name.Should().Be(expectedFilter.Func?.Method.Name);
        }
    }
}

[SqlTable("gen_person")]
public sealed class GenPerson
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("full_name")]
    public string? Name { get; set; }

    public int? Age { get; set; }
}

[SqlTable("gen_identity")]
public sealed class GenIdentity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public string? Value { get; set; }
}

[SqlTable("gen_no_key")]
public sealed class GenNoKey
{
    public int Id { get; set; }

    public string? Value { get; set; }
}

[SqlTable("gen_person")]
public interface IGenPersonView
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("full_name")]
    string? Name { get; set; }
}

[SqlTable("gen_json")]
public sealed class GenJsonMapped
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("payload")]
    [JsonColumn(Storage = JsonColumnStorage.Text)]
    public GenPerson? Payload { get; set; }

    [JsonColumn]
    public List<int>? Values { get; set; }
}

/// <summary>
/// The source-generated context of the JSON payload model. The compile-time entity description finds it and
/// hands its options to the JSON column converter, which is what a trimmed application needs.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GenPerson))]
internal sealed partial class GenJsonContext : JsonSerializerContext;

/// <summary>A strongly typed converter, as the reflective path instantiates it with <c>Activator</c>.</summary>
public sealed class GenMoneyConverter : ValueConverter<decimal, string>
{
    public override string? ConvertToProvider(decimal model)
        => model.ToString(CultureInfo.InvariantCulture);

    public override decimal ConvertFromProvider(string? provider)
        => provider is null ? 0m : decimal.Parse(provider, CultureInfo.InvariantCulture);
}

[SqlTable("gen_converted")]
public sealed class GenConverted
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("amount")]
    [ValueConverter(typeof(GenMoneyConverter))]
    public decimal Amount { get; set; }

    [ValueConverter(typeof(GenMoneyConverter))]
    public decimal? Optional { get; set; }

    [ValueConverter(typeof(EnumToStringConverter<GenState>))]
    public GenState State { get; set; }
}

/// <summary>The enum a closed generic converter is declared for.</summary>
public enum GenState
{
    Unknown,
    Active
}

/// <summary>The mapping is declared on the interface only; the reflective build falls back to it.</summary>
public interface IGenInterfaceMapping
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("full_name")]
    string? Name { get; set; }
}

[SqlTable("gen_interface_mapped")]
public sealed class GenInterfaceMapped : IGenInterfaceMapping
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>The mapping - table name included - is declared only on the interface.</summary>
[SqlTable("gen_interface_only")]
public interface IGenInterfaceOnly
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("full_name")]
    string? Name { get; set; }
}

public sealed class GenInterfaceOnlyEntity : IGenInterfaceOnly
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

public sealed class GenRelationshipChild
{
    [Key]
    public int Id { get; set; }

    public int ParentId { get; set; }
}

[SqlTable("gen_relationship")]
public sealed class GenRelationshipParent
{
    [Key]
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(GenRelationshipChild.ParentId))]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_not_mapped")]
public sealed class NotMappedEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [NotMapped]
    public string? Ignored { get; set; }
}

[SqlTable("gen_conflicting")]
public sealed class ConflictingConverters
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [ValueConverter(typeof(GenMoneyConverter))]
    [JsonColumn]
    public decimal? Value { get; set; }
}

[SqlTable("gen_typed")]
public sealed class GenTypedColumns
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [DecimalPrecision(12, 4)]
    public decimal Amount { get; set; }

    [Collation("de_DE")]
    [Column("full_name")]
    public string? Name { get; set; }

    [Duration(DurationUnit.Milliseconds, Precision = 3)]
    public TimeSpan Elapsed { get; set; }
}

[SqlTable("gen_range")]
public sealed class GenRangeColumns
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [RangeColumns("lower_bound", "upper_bound", UpperInclusive = true)]
    public Range<int> Span { get; set; }
}

[SqlTable("gen_filtered")]
[QueryFilter(FilterLambda = nameof(ActiveFilter))]
[QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFunc))]
[QueryFilter(FilterKey = "unused")]
public sealed class GenFiltered
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>An anonymous filter: additive, combined with the others using <c>and</c>.</summary>
    public static Expression<Func<GenFiltered, bool>> ActiveFilter => entity => entity.Id > 0;

    /// <summary>A keyed builder-function filter.</summary>
    public static readonly Func<EntityBuilder<GenFiltered>, IDataContext, EntityBuilder<GenFiltered>> TenantFunc =
        (builder, _) => builder.Where(entity => entity.Id > 1);
}

[SqlTable("gen_bad_precision")]
public sealed class GenBadPrecision
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [DecimalPrecision(12, 4)]
    public int NotDecimal { get; set; }
}

/// <summary>A plain DTO: settable, but it declares no mapping attribute.</summary>
public sealed class UnmappedDto
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
