using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>Order fixture whose buyer and approver both reference the same <see cref="AliasPerson"/> CLR type.</summary>
[SqlTable("orders")]
public class AliasOrder
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("buyer_id")]
    public int BuyerId { get; set; }

    [Column("approver_id")]
    public int ApproverId { get; set; }
}

/// <summary>Person fixture joined twice (as buyer and as approver) under two lexical aliases.</summary>
[SqlTable("person")]
public class AliasPerson
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
