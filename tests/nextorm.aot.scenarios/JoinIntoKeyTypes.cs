using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotScenarios;

/// <summary>A parent with an Int32 key linked to children whose actual CLR keys are Int64.</summary>
[SqlTable("aot_key_int_long_parent")]
public sealed class AotIntLongParent
{
    /// <summary>Parent key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Children reached through a mixed-width key junction.</summary>
    public ICollection<AotIntLongChild> Children { get; set; } = new List<AotIntLongChild>();
}

/// <summary>A child whose key is Int64 while its junction parent key is Int32.</summary>
[SqlTable("aot_key_int_long_child")]
public sealed class AotIntLongChild
{
    /// <summary>Child key.</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }
}

/// <summary>The mixed-width junction from an Int32 parent key to an Int64 child key.</summary>
[SqlTable("aot_key_int_long_link")]
public sealed class AotIntLongLink
{
    /// <summary>Junction row identity.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Int32 parent foreign key.</summary>
    [Column("parent_id")]
    public int ParentId { get; set; }

    /// <summary>Int64 child foreign key.</summary>
    [Column("child_id")]
    public long ChildId { get; set; }
}

/// <summary>The junction stores its decimal parent foreign key as provider text.</summary>
[SqlTable("aot_key_converted_link")]
public sealed class AotConvertedKeyLink
{
    /// <summary>Junction row identity.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Converted decimal parent foreign key.</summary>
    [Column("parent_id")]
    [ValueConverter(typeof(AotAmountConverter))]
    public decimal ParentId { get; set; }

    /// <summary>Child foreign key.</summary>
    [Column("child_id")]
    public int ChildId { get; set; }
}

/// <summary>A composite-key parent used to exercise a nullable junction key component.</summary>
[SqlTable("aot_key_nullable_parent")]
public sealed class AotNullableKeyParent
{
    /// <summary>Entity identity.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Nullable first relationship-key component.</summary>
    [Column("tenant_id")]
    public int? TenantId { get; set; }

    /// <summary>Children reached by both relationship-key components.</summary>
    public ICollection<AotNullableKeyChild> Children { get; set; } = new List<AotNullableKeyChild>();
}

/// <summary>A child with an ordinary Int32 key in the nullable-key scenario.</summary>
[SqlTable("aot_key_nullable_child")]
public sealed class AotNullableKeyChild
{
    /// <summary>Child key.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

/// <summary>The nullable composite-key junction used to check that SQL NULL does not associate rows.</summary>
[SqlTable("aot_key_nullable_link")]
public sealed class AotNullableKeyLink
{
    /// <summary>Junction row identity.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Nullable first parent-key component.</summary>
    [Column("parent_tenant")]
    public int? ParentTenant { get; set; }

    /// <summary>Second parent-key component.</summary>
    [Column("parent_id")]
    public int ParentId { get; set; }

    /// <summary>Child foreign key.</summary>
    [Column("child_id")]
    public int ChildId { get; set; }
}
