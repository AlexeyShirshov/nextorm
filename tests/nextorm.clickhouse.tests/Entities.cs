using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

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
    [Column("nums")]
    long[] Nums { get; set; }
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
