using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using nextorm.core;

namespace nextorm.mysql.tests;

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
