using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Integration.Tests;

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
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("nullableint")]
    int? Int { get; set; }
    [MaxLength(100)]
    [Column("somestring")]
    string? String { get; set; }
    [Column("tinyval")]
    byte TinyInt { get; set; }
    [Column("small")]
    short? SmallInt { get; set; }
    [Column("r")]
    float? Real { get; set; }
    [Column("d")]
    double? Double { get; set; }
    [Column("m")]
    decimal? Numeric { get; set; }
    [Column("dt")]
    DateTime? Datetime { get; set; }
    [Column("onlydate")]
    DateTime Date { get; set; }
    [Column("b")]
    bool? Boolean { get; set; }
    [Required]
    [Column("requiredstring")]
    string RequiredString { get; set; }
}

[SqlTable("binary_entity")]
public class BinaryEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("data")]
    public byte[]? Data { get; set; }
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
    int[] Nums { get; set; }
}

[SqlTable("insert_entity")]
public interface IInsertEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    long Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class InsertEntity : IInsertEntity
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}

public sealed record InsertSource(string Name, int Age);

[SqlTable("merge_entity")]
public interface IMergeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("name")]
    string? Name { get; set; }
    [Column("age")]
    int Age { get; set; }
}

public sealed class MergeEntity : IMergeEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
}
