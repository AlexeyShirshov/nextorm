using System.ComponentModel.DataAnnotations;
using NextORM.Core;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.AotScenarios;

/// <summary>
/// A query shape that runs identically under JIT and under a published NativeAOT binary. The returned
/// value is formatted deterministically (invariant culture, explicit ordering) so the two arms print the
/// same text and can be compared field by field.
/// </summary>
/// <param name="Id">Stable scenario identifier; it is the key both arms are joined on.</param>
/// <param name="Run">Executes the shape against a context bound to the seeded database.</param>
public sealed record AotScenario(string Id, Func<IDataContext, object?> Run);

[SqlTable("simple_entity")]
public sealed class AotSimpleEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("complex_entity")]
public sealed class AotComplexEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("nullableint")]
    public int? Int { get; set; }

    [Column("somestring")]
    public string? String { get; set; }

    [Column("m")]
    public decimal? Numeric { get; set; }

    [Column("dt")]
    public DateTime? Timestamp { get; set; }
}

[SqlTable("person")]
public sealed class AotPerson
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("orders")]
public sealed class AotOrder
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("buyer_id")]
    public int BuyerId { get; set; }

    [Column("approver_id")]
    public int ApproverId { get; set; }
}
