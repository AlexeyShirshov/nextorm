using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Examples.SqlServer.AdventureWorks;

// Schema: AdventureWorks 2022 (OLTP), schemas Sales/Production/Person/Purchasing.

[SqlTable("Sales.SalesOrderHeader")]
public interface ISalesOrderHeader
{
    [Key, Column("SalesOrderID")] int SalesOrderId { get; set; }
    [Column("OrderDate")] DateTime OrderDate { get; set; }
    [Column("TotalDue")] decimal TotalDue { get; set; }
    [Column("CustomerID")] int CustomerId { get; set; }
    [Column("SalesPersonID")] int? SalesPersonId { get; set; }
    [Column("TerritoryID")] int? TerritoryId { get; set; }
}

[SqlTable("Sales.SalesOrderDetail")]
public interface ISalesOrderDetail
{
    [Key, Column("SalesOrderID")] int SalesOrderId { get; set; }
    [Key, Column("SalesOrderDetailID")] int SalesOrderDetailId { get; set; }
    [Column("ProductID")] int ProductId { get; set; }
    [Column("OrderQty")] short OrderQty { get; set; }
    [Column("LineTotal")] decimal LineTotal { get; set; }
}

[SqlTable("Sales.Customer")]
public interface ICustomer
{
    [Key, Column("CustomerID")] int CustomerId { get; set; }
    [Column("PersonID")] int? PersonId { get; set; }
}

[SqlTable("Person.Person")]
public interface IPerson
{
    [Key, Column("BusinessEntityID")] int BusinessEntityId { get; set; }
    [Column("FirstName")] string FirstName { get; set; }
    [Column("LastName")] string LastName { get; set; }
}

[SqlTable("Sales.SalesPerson")]
public interface ISalesPerson
{
    [Key, Column("BusinessEntityID")] int BusinessEntityId { get; set; }
    [Column("SalesQuota")] decimal? SalesQuota { get; set; }
    [Column("SalesYTD")] decimal SalesYtd { get; set; }
}

[SqlTable("Sales.SalesTerritory")]
public interface ISalesTerritory
{
    [Key, Column("TerritoryID")] int TerritoryId { get; set; }
    [Column("Name")] string Name { get; set; }
}

[SqlTable("Production.Product")]
public interface IProduct
{
    [Key, Column("ProductID")] int ProductId { get; set; }
    [Column("Name")] string Name { get; set; }
    [Column("StandardCost")] decimal StandardCost { get; set; }
    [Column("ProductSubcategoryID")] int? ProductSubcategoryId { get; set; }
}

[SqlTable("Production.ProductSubcategory")]
public interface IProductSubcategory
{
    [Key, Column("ProductSubcategoryID")] int ProductSubcategoryId { get; set; }
    [Column("ProductCategoryID")] int ProductCategoryId { get; set; }
}

[SqlTable("Production.ProductCategory")]
public interface IProductCategory
{
    [Key, Column("ProductCategoryID")] int ProductCategoryId { get; set; }
    [Column("Name")] string Name { get; set; }
}

[SqlTable("Purchasing.PurchaseOrderDetail")]
public interface IPurchaseOrderDetail
{
    [Key, Column("PurchaseOrderID")] int PurchaseOrderId { get; set; }
    [Key, Column("PurchaseOrderDetailID")] int PurchaseOrderDetailId { get; set; }
    [Column("ProductID")] int ProductId { get; set; }
    [Column("DueDate")] DateTime DueDate { get; set; }
    [Column("ModifiedDate")] DateTime ModifiedDate { get; set; }
}

[SqlTable("Purchasing.PurchaseOrderHeader")]
public interface IPurchaseOrderHeader
{
    [Key, Column("PurchaseOrderID")] int PurchaseOrderId { get; set; }
    [Column("VendorID")] int VendorId { get; set; }
}

// In AdventureWorks the vendor primary key is BusinessEntityID (there is no VendorID column);
// Purchasing.PurchaseOrderHeader.VendorID points at it.
[SqlTable("Purchasing.Vendor")]
public interface IVendor
{
    [Key, Column("BusinessEntityID")] int BusinessEntityId { get; set; }
    [Column("Name")] string Name { get; set; }
}

[SqlTable("Production.WorkOrder")]
public interface IWorkOrder
{
    [Key, Column("WorkOrderID")] int WorkOrderId { get; set; }
    [Column("ProductID")] int ProductId { get; set; }
    [Column("OrderQty")] int OrderQty { get; set; }
    [Column("DueDate")] DateTime DueDate { get; set; }
    [Column("EndDate")] DateTime? EndDate { get; set; }
}
