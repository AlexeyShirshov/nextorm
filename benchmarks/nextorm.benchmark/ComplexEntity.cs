using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Benchmark;

[Table("complex_entity")]
public sealed class ComplexEntity
{
    public long Id { get; set; }
    [Column("nullableInt")]
    public int? Int { get; set; }
    [Column("someString")]
    public string? String { get; set; }
    [Column("d")]
    public double? Double { get; set; }
    [Column("dt")]
    public DateTime? Datetime { get; set; }
    [Column("b")]
    public bool? Boolean { get; set; }
    [Column("requiredString")]
    public string RequiredString { get; set; } = null!;
}

public sealed class LeftJoinRow
{
    public int Id { get; set; }
    public string? RightString { get; set; }
}

public sealed class FourJoinRow
{
    public int A { get; set; }
    public string? B { get; set; }
    public int C { get; set; }
    public string? D { get; set; }
}

public sealed class RowNumberRow
{
    public long Id { get; set; }
    public long Rn { get; set; }
}

public sealed class SumOverRow
{
    public long Id { get; set; }
    public long? Total { get; set; }
}

public sealed class CteJoinRow
{
    public int Id { get; set; }
    public int SimpleId { get; set; }
}

public sealed class CteNumberRow
{
    public int n { get; set; }
}

public sealed class JsonEachRow
{
    [Column("key")]
    public long Key { get; set; }
    [Column("value")]
    public long Value { get; set; }
}
