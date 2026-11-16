using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json.Serialization;
using NextORM.Core;

namespace NextORM.AotSqlProbe;

/// <summary>A seeded customer row. The probe only reads this shape.</summary>
[SqlTable("aot_customer")]
public interface ICustomer
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>Customer name, unique in the seed.</summary>
    [Column("name")]
    string Name { get; set; }

    /// <summary>Two-letter country code.</summary>
    [Column("country")]
    string Country { get; set; }
}

/// <summary>A seeded order row. The probe only reads this shape.</summary>
[SqlTable("aot_order")]
public interface IOrder
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>Owning customer.</summary>
    [Column("customer_id")]
    int CustomerId { get; set; }

    /// <summary>Order amount.</summary>
    [Column("amount")]
    decimal Amount { get; set; }

    /// <summary>Order timestamp.</summary>
    [Column("order_date")]
    DateTime OrderDate { get; set; }
}

/// <summary>
/// The concrete twin of <see cref="IOrder"/>: raw-SQL row mapping binds by column name and needs a
/// parameterless constructor, which an interface cannot provide.
/// </summary>
[SqlTable("aot_order")]
public sealed class AotOrderRow
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Owning customer.</summary>
    [Column("customer_id")]
    public int CustomerId { get; set; }

    /// <summary>Order amount.</summary>
    [Column("amount")]
    public decimal Amount { get; set; }

    /// <summary>Order timestamp.</summary>
    [Column("order_date")]
    public DateTime OrderDate { get; set; }
}

/// <summary>
/// The write fixture. The key is declared on the entity and supplied by the shape, so the probe needs no
/// identity column - ClickHouse has none - and the native arm and its control never print a generated value.
/// </summary>
[SqlTable("aot_write")]
public sealed class AotWrite
{
    /// <summary>Primary key, supplied by the shape.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Row marker, scoping each shape's rows.</summary>
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Row value, read back in order.</summary>
    [Column("age")]
    public int Age { get; set; }
}

/// <summary>The JSON-column fixture: the payload is stored as text and mapped through the generated converter.</summary>
[SqlTable("aot_json")]
public sealed class AotJsonEntity
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The stored document.</summary>
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
    /// <summary>Payload name.</summary>
    public string? Name { get; set; }

    /// <summary>Payload value.</summary>
    public int Value { get; set; }
}

/// <summary>The source-generated contract metadata the trimmed JSON converter serializes through.</summary>
[JsonSerializable(typeof(AotJsonPayload))]
internal sealed partial class AotJsonContext : JsonSerializerContext;

/// <summary>The eager-load parent: the navigation is declared by attribute, so the compile-time entity
/// description carries the relationship and the split loader runs without fluent configuration.</summary>
[SqlTable("aot_parent")]
public sealed class AotParent
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Parent name.</summary>
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The children loaded by the split loader.</summary>
    [Relationship(ForeignKey = nameof(AotChild.ParentId))]
    public ICollection<AotChild> Children { get; } = new List<AotChild>();
}

/// <summary>The eager-load child.</summary>
[SqlTable("aot_child")]
public sealed class AotChild
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Owning parent.</summary>
    [Column("parent_id")]
    public int ParentId { get; set; }

    /// <summary>Child name.</summary>
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>The value-converter fixture: the column stores the converter's text form, not the number.</summary>
[SqlTable("aot_converter")]
public sealed class AotConverted
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The converted amount.</summary>
    [Column("amount")]
    [ValueConverter(typeof(AotProbeAmountConverter))]
    public decimal Amount { get; set; }
}

/// <summary>
/// Stores a decimal as <c>"v=" + invariant text</c>, so a mapping that skipped the converter reads back a
/// different number rather than the stored text parsed as a decimal.
/// </summary>
public sealed class AotProbeAmountConverter : ValueConverter<decimal, string>
{
    /// <inheritdoc/>
    public override string? ConvertToProvider(decimal model)
        => "v=" + model.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override decimal ConvertFromProvider(string? provider)
        => provider is null || provider.Length <= 2
            ? 0m
            : decimal.Parse(provider.Substring(2), CultureInfo.InvariantCulture);
}

/// <summary>The many-to-many parent used by the native rejection check; the navigation is declared fluently.</summary>
[SqlTable("aot_m2m_parent")]
public sealed class AotM2mParent
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The linked tags.</summary>
    public ICollection<AotM2mTag> Tags { get; } = new List<AotM2mTag>();
}

/// <summary>The many-to-many child used by the native rejection check.</summary>
[SqlTable("aot_m2m_tag")]
public sealed class AotM2mTag
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

/// <summary>The junction between <see cref="AotM2mParent"/> and <see cref="AotM2mTag"/>.</summary>
[SqlTable("aot_m2m_link")]
public sealed class AotM2mLink
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The linked parent.</summary>
    [Column("parent_id")]
    public int ParentId { get; set; }

    /// <summary>The linked tag.</summary>
    [Column("child_id")]
    public int ChildId { get; set; }
}
