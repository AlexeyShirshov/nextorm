using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

public class SimpleEntity : ISimpleEntity
{
    public int Id { get; set; }
}

[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("nullableint")]
    int? Int { get; set; }
    [Column("somestring")]
    string? String { get; set; }
    [Column("b")]
    bool? Boolean { get; set; }
    [Column("dt")]
    DateTime? Datetime { get; set; }
}

[SqlTable("tuple_entity")]
public interface ITupleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("pair")]
    Tuple<int, string> Pair { get; set; }
}

/// <summary>An entity whose only written column is nullable, so <c>KeepNulls=false</c> is default-equivalent.</summary>
[SqlTable("nullable_entity")]
public interface INullableEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

/// <summary>The concrete entity for <see cref="INullableEntity"/>.</summary>
public sealed class NullableEntity : INullableEntity
{
    public long Id { get; set; }

    public string? Name { get; set; }
}

[SqlTable("dynamic_entity")]
public class DynamicColumnsEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
