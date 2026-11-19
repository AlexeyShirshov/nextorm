using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotScenarios;

/// <summary>
/// The key of both relationship fixtures lives on the base type, so the shape also proves that a mapping
/// described at compile time covers the base chain rather than only the declaring type.
/// </summary>
public abstract class AotRelEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("person")]
public sealed class AotRelCustomer : AotRelEntity
{
    [Column("name")]
    public string? Name { get; set; }

    [Column("amount")]
    [ValueConverter(typeof(AotAmountConverter))]
    public decimal Amount { get; set; }

    [Relationship(ForeignKey = nameof(AotRelOrder.BuyerId))]
    public ICollection<AotRelOrder> Orders { get; } = new List<AotRelOrder>();
}

[SqlTable("orders")]
public sealed class AotRelOrder : AotRelEntity
{
    [Column("buyer_id")]
    public int BuyerId { get; set; }

    [Column("approver_id")]
    public int ApproverId { get; set; }

    [Relationship(ForeignKey = nameof(BuyerId))]
    public AotRelCustomer? Buyer { get; set; }

    [Relationship(ForeignKey = nameof(ApproverId))]
    public AotRelCustomer? Approver { get; set; }
}
