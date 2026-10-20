using LinqToDB.Mapping;

namespace NextORM.Examples.SqlServer.AdventureWorks;

// linq2db mappings for the AdventureWorks tables modelled by `Entities.cs`. Concrete classes are
// required by linq2db; the table/column names and CLR types match the nextorm interfaces exactly.

[Table("SalesOrderHeader", Schema = "Sales")]
public sealed class AwSalesOrderHeader
{
    [PrimaryKey, Column("SalesOrderID")] public int SalesOrderId { get; set; }
    [Column("OrderDate")] public DateTime OrderDate { get; set; }
    [Column("TotalDue")] public decimal TotalDue { get; set; }
    [Column("CustomerID")] public int CustomerId { get; set; }
    [Column("SalesPersonID")] public int? SalesPersonId { get; set; }
    [Column("TerritoryID")] public int? TerritoryId { get; set; }
}

[Table("SalesOrderDetail", Schema = "Sales")]
public sealed class AwSalesOrderDetail
{
    [PrimaryKey(1), Column("SalesOrderID")] public int SalesOrderId { get; set; }
    [PrimaryKey(2), Column("SalesOrderDetailID")] public int SalesOrderDetailId { get; set; }
    [Column("ProductID")] public int ProductId { get; set; }
    [Column("OrderQty")] public short OrderQty { get; set; }
    [Column("LineTotal")] public decimal LineTotal { get; set; }
}

[Table("Customer", Schema = "Sales")]
public sealed class AwCustomer
{
    [PrimaryKey, Column("CustomerID")] public int CustomerId { get; set; }
    [Column("PersonID")] public int? PersonId { get; set; }
}

[Table("Person", Schema = "Person")]
public sealed class AwPerson
{
    [PrimaryKey, Column("BusinessEntityID")] public int BusinessEntityId { get; set; }
    [Column("FirstName")] public string FirstName { get; set; } = "";
    [Column("LastName")] public string LastName { get; set; } = "";
}

[Table("SalesPerson", Schema = "Sales")]
public sealed class AwSalesPerson
{
    [PrimaryKey, Column("BusinessEntityID")] public int BusinessEntityId { get; set; }
    [Column("SalesQuota")] public decimal? SalesQuota { get; set; }
    [Column("SalesYTD")] public decimal SalesYtd { get; set; }
}

[Table("SalesTerritory", Schema = "Sales")]
public sealed class AwSalesTerritory
{
    [PrimaryKey, Column("TerritoryID")] public int TerritoryId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
}

[Table("Product", Schema = "Production")]
public sealed class AwProduct
{
    [PrimaryKey, Column("ProductID")] public int ProductId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
    [Column("StandardCost")] public decimal StandardCost { get; set; }
    [Column("ProductSubcategoryID")] public int? ProductSubcategoryId { get; set; }
}

[Table("ProductSubcategory", Schema = "Production")]
public sealed class AwProductSubcategory
{
    [PrimaryKey, Column("ProductSubcategoryID")] public int ProductSubcategoryId { get; set; }
    [Column("ProductCategoryID")] public int ProductCategoryId { get; set; }
}

[Table("ProductCategory", Schema = "Production")]
public sealed class AwProductCategory
{
    [PrimaryKey, Column("ProductCategoryID")] public int ProductCategoryId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
}

[Table("PurchaseOrderDetail", Schema = "Purchasing")]
public sealed class AwPurchaseOrderDetail
{
    [PrimaryKey(1), Column("PurchaseOrderID")] public int PurchaseOrderId { get; set; }
    [PrimaryKey(2), Column("PurchaseOrderDetailID")] public int PurchaseOrderDetailId { get; set; }
    [Column("ProductID")] public int ProductId { get; set; }
    [Column("DueDate")] public DateTime DueDate { get; set; }
    [Column("ModifiedDate")] public DateTime ModifiedDate { get; set; }
}

[Table("PurchaseOrderHeader", Schema = "Purchasing")]
public sealed class AwPurchaseOrderHeader
{
    [PrimaryKey, Column("PurchaseOrderID")] public int PurchaseOrderId { get; set; }
    [Column("VendorID")] public int VendorId { get; set; }
}

[Table("Vendor", Schema = "Purchasing")]
public sealed class AwVendor
{
    [PrimaryKey, Column("BusinessEntityID")] public int BusinessEntityId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
}

[Table("WorkOrder", Schema = "Production")]
public sealed class AwWorkOrder
{
    [PrimaryKey, Column("WorkOrderID")] public int WorkOrderId { get; set; }
    [Column("ProductID")] public int ProductId { get; set; }
    [Column("OrderQty")] public int OrderQty { get; set; }
    [Column("DueDate")] public DateTime DueDate { get; set; }
    [Column("EndDate")] public DateTime? EndDate { get; set; }
}
