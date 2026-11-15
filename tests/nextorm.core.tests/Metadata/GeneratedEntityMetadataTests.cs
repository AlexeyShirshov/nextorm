using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    public void GeneratedDescription_ShouldMatchReflectiveAutoBuild_ForARangeColumnPair()
        => AssertSameMetadata<GenRangeColumns>();

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
    public void TypeWithoutMappingAttribute_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(UnmappedDto), out _).Should().BeFalse(
            "a settable class with no mapping attribute is not necessarily an entity");

    [Fact]
    public void TypeWithAnUnmodelledAttribute_ShouldKeepTheReflectivePath()
        => GeneratedEntityMetadata.TryGet(typeof(JsonMappedEntity), out _).Should().BeFalse(
            "the JSON converter changes the mapping in a way the generator does not reproduce");

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
public sealed class JsonMappedEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [JsonColumn]
    public GenPerson? Payload { get; set; }
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
