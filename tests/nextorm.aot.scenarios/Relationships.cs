using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotScenarios;

[SqlTable("person")]
public sealed class AotRelCustomer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Relationship(ForeignKey = nameof(AotRelOrder.BuyerId))]
    public ICollection<AotRelOrder> Orders { get; } = new List<AotRelOrder>();
}

[SqlTable("orders")]
public sealed class AotRelOrder
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("buyer_id")]
    public int BuyerId { get; set; }

    [Column("approver_id")]
    public int ApproverId { get; set; }

    [Relationship(ForeignKey = nameof(BuyerId))]
    public AotRelCustomer? Buyer { get; set; }
}
