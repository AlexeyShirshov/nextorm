using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

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

[SqlTable("sales")]
public interface ISalesEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("category")]
    string? Category { get; set; }
    [Column("quarter")]
    int Quarter { get; set; }
    [Column("margin")]
    decimal? Margin { get; set; }
}

[SqlTable("quarterly")]
public interface IQuarterlyEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("category")]
    string? Category { get; set; }
    [Column("q1")]
    decimal? Q1 { get; set; }
    [Column("q2")]
    decimal? Q2 { get; set; }
}
