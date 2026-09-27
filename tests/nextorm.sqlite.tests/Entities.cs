using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

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

[SqlTable("order")]
public interface IKeywordEntity
{
    [Key]
    [Column("select")]
    int Value { get; set; }
}

public class BareEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; }
    public int Stock { get; private set; }
}

public interface IBareEntity
{
    int Id { get; set; }
    string? Name { get; set; }
}

[SqlTable("ExplicitTable")]
public class ExplicitlyMappedEntity
{
    [Key]
    [Column("ExplicitColumn")]
    public int Value { get; set; }
    public string? FirstName { get; set; }
}

[SqlTable("dynamic_entity")]
public class DynamicColumnsEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public string? Name { get; set; }
    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
