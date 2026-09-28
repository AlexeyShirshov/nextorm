using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

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

[SqlTable("array_entity")]
public interface IArrayEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("tags")]
    string[] Tags { get; set; }
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

[SqlTable("reservation")]
public interface IRangeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("during")]
    Range<int> During { get; set; }
}

public class BareEntity
{
    public int Id { get; set; }
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

/// <summary>
/// SQL-generation-only dynamic-columns entity with a non-key mapped column, so the full-MERGE branch
/// (which needs a column to update independently of the store) can be exercised.
/// </summary>
[SqlTable("dynamic_writable_entity")]
public class DynamicWritableEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("label")]
    public string? Label { get; set; }
    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
