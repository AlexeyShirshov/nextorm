using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotSqlProbe;

/// <summary>A seeded customer row. The probe only reads this shape.</summary>
[SqlTable("aot_customer")]
public interface ICustomer
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>Customer name, unique in the seed.</summary>
    [Column("name")]
    string Name { get; set; }

    /// <summary>Two-letter country code.</summary>
    [Column("country")]
    string Country { get; set; }
}

/// <summary>A seeded order row. The probe only reads this shape.</summary>
[SqlTable("aot_order")]
public interface IOrder
{
    /// <summary>Primary key.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>Owning customer.</summary>
    [Column("customer_id")]
    int CustomerId { get; set; }

    /// <summary>Order amount.</summary>
    [Column("amount")]
    decimal Amount { get; set; }

    /// <summary>Order timestamp.</summary>
    [Column("order_date")]
    DateTime OrderDate { get; set; }
}
