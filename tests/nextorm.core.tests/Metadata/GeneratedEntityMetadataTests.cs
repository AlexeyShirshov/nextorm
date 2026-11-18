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
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForACollectionNavigation()
        => AssertSameMetadata<GenRelationshipParent>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAReferenceNavigation()
    {
        AssertSameMetadata<GenRelationshipChildMapped>();
        AssertSameMetadata<GenRelationshipParentMapped>();
    }

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForCompositeRelationshipKeys()
        => AssertSameMetadata<GenCompositeParent>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForABaseTypeMapping()
        => AssertSameMetadata<GenInheritanceEntity>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnInheritedTableName()
        => AssertSameMetadata<GenInheritedTableEntity>();

    [Fact]
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForAnInheritedNavigation()
        => AssertSameMetadata<GenInheritanceNavEntity>();

    [Fact]
    public void TypeWithAHiddenBaseMember_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(GenHidingEntity), out _).Should().BeFalse(
            "the flattened enumeration returns a hidden base property twice, which the generated description does not reproduce");

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
    public void TypeWithARefusedRelationshipForm_ShouldKeepTheReflectivePath()
    {
        // Each of these declarations is a deterministic NotSupportedException in the reflective build. A
        // generated description that accepted one would turn the refusal into a working query, so the type
        // stays on the path that refuses it.
        foreach (var type in new[]
        {
            typeof(GenRelationshipMissingForm),
            typeof(GenRelationshipBothForms),
            typeof(GenRelationshipEmptyForeignKeys),
            typeof(GenRelationshipEmptyPrincipalKeys),
            typeof(GenRelationshipUnknownMember),
            typeof(GenRelationshipArityMismatch),
        })
        {
            GeneratedEntityMetadata.TryGet(type, out _).Should().BeFalse($"{type.Name} must keep the refusal path");
        }

        FluentActions.Invoking(() => new EntityMetadataBuilder<GenRelationshipMissingForm>().AutoBuild())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*GenRelationshipMissingForm.Children*must name the foreign-key property*");
    }

    [Fact]
    public void GeneratedRelationships_ShouldCarryTheDeclaredKeys()
    {
        GeneratedEntityMetadata.TryGet(typeof(GenRelationshipParentMapped), out var factory).Should().BeTrue();
        GeneratedEntityMetadata.TryGet(typeof(GenRelationshipChildMapped), out var childFactory).Should().BeTrue();

        var parent = factory().Relationships.Single();
        var child = childFactory().Relationships.Single();

        parent.Navigation!.Name.Should().Be(nameof(GenRelationshipParentMapped.Children));
        parent.IsCollection.Should().BeTrue();
        parent.RelatedType.Should().Be(typeof(GenRelationshipChildMapped));
        parent.ForeignKey.Select(property => property.PropertyInfo.Name).Should().Equal(nameof(GenRelationshipChildMapped.ParentId));
        parent.PrincipalKey.Select(property => property.PropertyInfo.Name).Should().Equal(nameof(GenRelationshipParentMapped.Id));

        child.Navigation!.Name.Should().Be(nameof(GenRelationshipChildMapped.Parent));
        child.IsCollection.Should().BeFalse();
        child.RelatedType.Should().Be(typeof(GenRelationshipParentMapped));
        child.ForeignKey.Select(property => property.PropertyInfo.Name).Should().Equal(nameof(GenRelationshipChildMapped.ParentId));
        child.PrincipalKey.Select(property => property.PropertyInfo.Name).Should().Equal(nameof(GenRelationshipParentMapped.Id));
        child.Kind.Should().Be(RelationshipKind.ManyToOne, "a reference navigation is the dependent side");
        parent.Kind.Should().Be(RelationshipKind.OneToMany);
    }

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

        generated.Relationships.Should().HaveCount(reflected.Relationships.Count);
        for (var i = 0; i < reflected.Relationships.Count; i++)
        {
            var expectedRelationship = reflected.Relationships[i];
            var actualRelationship = generated.Relationships[i];

            actualRelationship.Kind.Should().Be(expectedRelationship.Kind, $"relationship #{i} must keep the reflective kind");
            actualRelationship.DeclaringType.Should().Be(expectedRelationship.DeclaringType);
            actualRelationship.RelatedType.Should().Be(expectedRelationship.RelatedType);
            actualRelationship.Navigation!.Name.Should().Be(expectedRelationship.Navigation!.Name);
            actualRelationship.IsCollection.Should().Be(expectedRelationship.IsCollection);

            // The declared components, before the resolver validates them against the participating
            // mappings: the generated description must keep the same members in the same order.
            var expectedDeclared = (RelationshipMetadata)expectedRelationship;
            var actualDeclared = (RelationshipMetadata)actualRelationship;
            actualDeclared.ForeignKeyProperties!.Select(property => property.Name)
                .Should().Equal(expectedDeclared.ForeignKeyProperties!.Select(property => property.Name), $"relationship #{i} must declare the same foreign key");
            (actualDeclared.PrincipalKeyProperties is null).Should().Be(expectedDeclared.PrincipalKeyProperties is null);
            actualDeclared.PrincipalKeyProperties?.Select(property => property.Name)
                .Should().Equal(expectedDeclared.PrincipalKeyProperties?.Select(property => property.Name));
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

/// <summary>
/// The dependent side of the collection navigation below. It declares no mapping attribute of its own, so
/// the generator does not describe it as an entity - it only reads its foreign-key member - which is the
/// case that proves a relationship can point outside the covered set.
/// </summary>
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

[SqlTable("gen_relationship_parent")]
public sealed class GenRelationshipParentMapped
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(GenRelationshipChildMapped.ParentId))]
    public ICollection<GenRelationshipChildMapped> Children { get; } = new List<GenRelationshipChildMapped>();
}

[SqlTable("gen_relationship_child")]
public sealed class GenRelationshipChildMapped
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Relationship(ForeignKey = nameof(ParentId))]
    public GenRelationshipParentMapped? Parent { get; set; }
}

/// <summary>
/// A base entity: the key, a virtual mapped column and a keyed query filter live here, so the derived
/// mapping has to read the column attribute through the override and apply the filters base-first.
/// </summary>
public abstract class GenInheritanceRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("root_name")]
    public virtual string? Name { get; set; }

    public static Expression<Func<GenInheritanceRoot, bool>> RootFilter => entity => entity.Id > 0;
}

[SqlTable("gen_inheritance")]
[QueryFilter(FilterKey = "active", FilterLambda = nameof(DerivedFilter))]
public sealed class GenInheritanceEntity : GenInheritanceRoot
{
    [Column("child_value")]
    public int Value { get; set; }

    /// <summary>An override without attributes: the column mapping is read through to the base declaration.</summary>
    public override string? Name { get; set; }

    public static Expression<Func<GenInheritanceEntity, bool>> DerivedFilter => entity => entity.Value > 1;
}

/// <summary>The table name is declared on the base type only; the type attribute is inherited.</summary>
[SqlTable("gen_inherited_table")]
public abstract class GenInheritedTableRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

public sealed class GenInheritedTableEntity : GenInheritedTableRoot
{
    [Column("label")]
    public string? Label { get; set; }
}

/// <summary>A navigation declared on the base class is part of the derived entity's mapping.</summary>
public abstract class GenInheritanceNavRoot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(GenRelationshipChild.ParentId))]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_inheritance_nav")]
public sealed class GenInheritanceNavEntity : GenInheritanceNavRoot
{
    [Column("label")]
    public string? Label { get; set; }
}

/// <summary>A base property hidden rather than overridden: the reflective enumeration returns both.</summary>
public abstract class GenHidingRoot
{
    [Column("root_value")]
    public int Value { get; set; }
}

[SqlTable("gen_hiding")]
public sealed class GenHidingEntity : GenHidingRoot
{
    public new int Value { get; set; }
}

public sealed class GenCompositeChild
{
    public int ParentA { get; set; }

    public int ParentB { get; set; }
}

[SqlTable("gen_composite_parent")]
public sealed class GenCompositeParent
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Relationship(
        ForeignKeys = new[] { nameof(GenCompositeChild.ParentA), nameof(GenCompositeChild.ParentB) },
        PrincipalKeys = new[] { nameof(A), nameof(B) })]
    public ICollection<GenCompositeChild> Children { get; } = new List<GenCompositeChild>();
}

// The refusal shapes: every one of them is a deterministic NotSupportedException in the reflective build,
// so the generator must leave the type on the path that throws instead of describing it.
[SqlTable("gen_relationship_missing_form")]
public sealed class GenRelationshipMissingForm
{
    [Key]
    public int Id { get; set; }

    [Relationship]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_relationship_both_forms")]
public sealed class GenRelationshipBothForms
{
    [Key]
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(GenRelationshipChild.ParentId), ForeignKeys = new[] { nameof(GenRelationshipChild.ParentId) })]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_relationship_empty_foreign_keys")]
public sealed class GenRelationshipEmptyForeignKeys
{
    [Key]
    public int Id { get; set; }

    [Relationship(ForeignKeys = new string[0])]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_relationship_empty_principal_keys")]
public sealed class GenRelationshipEmptyPrincipalKeys
{
    [Key]
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(GenRelationshipChild.ParentId), PrincipalKeys = new string[0])]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_relationship_unknown_member")]
public sealed class GenRelationshipUnknownMember
{
    [Key]
    public int Id { get; set; }

    [Relationship(ForeignKey = "MissingMember")]
    public ICollection<GenRelationshipChild> Children { get; } = new List<GenRelationshipChild>();
}

[SqlTable("gen_relationship_arity")]
public sealed class GenRelationshipArityMismatch
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Relationship(ForeignKeys = new[] { nameof(GenCompositeChild.ParentA), nameof(GenCompositeChild.ParentB) }, PrincipalKeys = new[] { nameof(A) })]
    public ICollection<GenCompositeChild> Children { get; } = new List<GenCompositeChild>();
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
