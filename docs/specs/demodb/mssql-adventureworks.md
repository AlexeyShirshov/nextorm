# SQL Server / AdventureWorks 2022

Модельные запросы: [`docs/specs/mssql-demodb/`](../mssql-demodb).

> Запускаемая версия этих примеров — проект
> [`examples/nextorm.examples.mssql.adventureworks`](../../../examples/nextorm.examples.mssql.adventureworks).
> Сниппеты ниже иллюстративны; при расхождении ориентируйтесь на код проекта (`ctx.From(derivedQuery).Join(...)`
> движком не поддерживается, поэтому соединения с менеджером и в supply chain перенесены в один запрос).

База — AdventureWorks 2022 (`mcr.microsoft.com/mssql/server:2022-latest`). Таблицы лежат в схемах
`Sales`, `Production`, `Person`, `Purchasing`; nextorm подставляет имя из `[SqlTable]` в `FROM`
дословно, поэтому указывается полное имя (`Sales.SalesOrderHeader`).

Схема сверена с официальным install-скриптом
[`instawdb.sql`](https://github.com/microsoft/sql-server-samples/tree/master/samples/databases/adventure-works/oltp-install-script)
(Updated 2025-11-14). По итогам исправлены две ссылки в `mssql_supply_chain.sql`: у
`Purchasing.Vendor` нет колонки `VendorID` (PK — `BusinessEntityID`), а у `Production.WorkOrder`
нет `ScheduledEndDate` (плановое окончание — `DueDate`).

## Сущности

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.DemoDb.AdventureWorks;

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

// В AdventureWorks PK поставщика — BusinessEntityID (колонки VendorID нет);
// Purchasing.PurchaseOrderHeader.VendorID ссылается именно на неё.
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
    [Column("DueDate")] DateTime DueDate { get; set; }       // плановое окончание
    [Column("EndDate")] DateTime? EndDate { get; set; }      // фактическое, nullable
}
```

`FORMAT(…, 'yyyy-MM[-dd]')` не входит в кросс-провайдерный набор (это PostgreSQL-функция
`SqlFunctions.Postgres.format`). На SQL Server она объявляется пользовательской UDF — механизм
`[SqlFunction]` подставляет имя функции как есть:

```csharp
public static class DemoUdf
{
    [SqlFunction("format")]
    public static string Format(DateTime? value, string format) => throw new NotSupportedException();
}
```

Общие правила моделирования:

* **Усечение даты** `DATEADD(месяц/квартал, DATEDIFF(…, 0, OrderDate), 0)` эквивалентно
  `SqlFunctions.Sql.date_trunc("month"|"quarter", OrderDate)` — на SQL Server 2022 это `datetrunc`.
* **`STDEV` / `AVG(CAST(x AS FLOAT))`**: `stdev` — выборочное СКО (совпадает с `STDEV`); чтобы
  избежать целочисленного `AVG`, приводите к `double` до агрегата.
* **`PIVOT` не поддержан** (см. [todo_mssql §Отложено](../roadmap/todo_mssql.md)); эквивалент —
  условная агрегация `SUM(CASE WHEN quarter = n THEN margin END)`.
* **правило `GroupBy`**: вычисляемый ключ группы повторяется в `Select` дословно.

---

## 1. `mssql_vip_churn.sql` — «спящие» VIP-клиенты

```csharp
// CTE CustomerOrders
var customerOrders = ctx.From<ISalesOrderHeader>()
    .Join(ctx.From<ICustomer>(), (soh, c) => soh.CustomerId == c.CustomerId)
    .Join(ctx.From<IPerson>(), (p, per) => p.Item2.PersonId == per.BusinessEntityId)
    .Select(p => new
    {
        p.Item1.CustomerId,
        CustomerName = p.Item3.FirstName + " " + p.Item3.LastName,
        p.Item1.SalesOrderId,
        p.Item1.OrderDate,
        p.Item1.TotalDue,
        p.Item1.SalesPersonId,
        LastCompanyOrderDate = SqlFunctions.Sql.max_over(p.Item1.OrderDate).Over(),
        PrevOrderDate = SqlFunctions.Sql.lag(p.Item1.OrderDate)
            .Over(partitionBy: () => p.Item1.CustomerId, orderBy: () => p.Item1.OrderDate)
    });

// CTE CustomerMetrics
var metrics = ctx.From(customerOrders)
    .GroupBy(c => new { c.CustomerId, c.CustomerName, c.SalesPersonId })
    .Select(c => new
    {
        c.CustomerId,
        c.CustomerName,
        c.SalesPersonId,
        LifetimeValue = SqlFunctions.Sql.sum(c.TotalDue),
        LastPurchaseDate = SqlFunctions.Sql.max(c.OrderDate),
        DaysSinceLastOrder = SqlFunctions.Sql.date_diff("day",
            SqlFunctions.Sql.max(c.OrderDate), SqlFunctions.Sql.max(c.LastCompanyOrderDate))
    });

var rows = await ctx.From(metrics)
    .LeftJoin(ctx.From<ISalesPerson>(), (m, sp) => m.SalesPersonId == sp.BusinessEntityId)
    .LeftJoin(ctx.From<IPerson>(), (p, per) => p.Item2.BusinessEntityId == per.BusinessEntityId)
    .Where(p => p.Item1.LifetimeValue > 50000 && p.Item1.DaysSinceLastOrder > 180)
    .OrderByDescending(p => p.Item1.LifetimeValue)
    .Select(p => new
    {
        CustomerId = p.Item1.CustomerId,
        CustomerName = p.Item1.CustomerName,
        Ltv = Math.Round(p.Item1.LifetimeValue ?? 0, 2),
        LastPurchase = DemoUdf.Format(p.Item1.LastPurchaseDate, "yyyy-MM-dd"),
        DaysSinceLastOrder = p.Item1.DaysSinceLastOrder,
        Manager = p.Item3.FirstName + " " + p.Item3.LastName
    })
    .ToListAsync();
```

## 2. `mssql_rolling_kpi.sql` — скользящие KPI по регионам

```csharp
// CTE MonthlySales
var monthly = ctx.From<ISalesOrderHeader>()
    .Join(ctx.From<ISalesTerritory>(), (soh, st) => soh.TerritoryId == st.TerritoryId)
    .GroupBy(p => new
    {
        RegionName = p.Item2.Name,
        SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.OrderDate)
    })
    .Select(p => new
    {
        RegionName = p.Item2.Name,
        SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.OrderDate),
        MonthlyRevenue = SqlFunctions.Sql.sum(p.Item1.TotalDue)
    });

var rows = await ctx.From(monthly)
    .OrderBy(m => m.RegionName)
    .OrderByDescending(m => m.SalesMonth)
    .Select(m => new
    {
        m.RegionName,
        Month = DemoUdf.Format(m.SalesMonth, "yyyy-MM"),
        m.MonthlyRevenue,
        Cumulative = SqlFunctions.Sql.sum_over(m.MonthlyRevenue).Over(
            partitionBy: () => m.RegionName,
            orderBy: () => m.SalesMonth,
            frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow),
        MovingAvg3 = SqlFunctions.Sql.avg_over(m.MonthlyRevenue).Over(
            partitionBy: () => m.RegionName,
            orderBy: () => m.SalesMonth,
            frame: WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow))
    })
    .ToListAsync();
```

## 3. `mssql_supply_chain.sql` — задержки в цепочке поставок

```csharp
// CTE SupplierDelays
var supplierDelays = ctx.From<IPurchaseOrderDetail>()
    .Join(ctx.From<IPurchaseOrderHeader>(), (pod, poh) => pod.PurchaseOrderId == poh.PurchaseOrderId)
    .Join(ctx.From<IVendor>(), (p, v) => p.Item2.VendorId == v.BusinessEntityId)
    .Where(p => p.Item1.ModifiedDate > SqlFunctions.Sql.date_add("day", 5, p.Item1.DueDate))
    .Select(p => new
    {
        p.Item1.ProductId,
        p.Item2.VendorId,
        VendorName = p.Item3.Name,
        DelayDays = SqlFunctions.Sql.date_diff("day", p.Item1.DueDate, p.Item1.ModifiedDate)
    });

// CTE ProductionImpact
var productionImpact = ctx.From<IWorkOrder>()
    .Join(ctx.From<IProduct>(), (wo, pr) => wo.ProductId == pr.ProductId)
    .Where(p => p.Item1.EndDate > p.Item1.DueDate)
    .Select(p => new
    {
        p.Item1.ProductId,
        ProductName = p.Item2.Name,
        p.Item1.OrderQty,
        ProdDelayDays = SqlFunctions.Sql.date_diff("day", p.Item1.DueDate, p.Item1.EndDate)
    });

var rows = await ctx.From(productionImpact)
    .Join(supplierDelays, (pi, sd) => pi.ProductId == sd.ProductId)
    .OrderByDescending(p => p.Item1.ProdDelayDays)
    .OrderByDescending(p => p.Item2.DelayDays)
    .Select(p => new
    {
        Component = p.Item1.ProductName,
        Vendor = p.Item2.VendorName,
        SupplyDelay = p.Item2.DelayDays,
        ProductionQty = p.Item1.OrderQty,
        AssemblyShift = p.Item1.ProdDelayDays
    })
    .ToListAsync();
```

## 4. `mssql_product_abc_xyz.sql` — ABC/XYZ продуктовой матрицы

```csharp
// CTE ProductQuarterlySales
var quarterly = ctx.From<ISalesOrderDetail>()
    .Join(ctx.From<ISalesOrderHeader>(), (sod, soh) => sod.SalesOrderId == soh.SalesOrderId)
    .Join(ctx.From<IProduct>(), (p, pr) => p.Item1.ProductId == pr.ProductId)
    .GroupBy(p => new
    {
        p.Item1.ProductId,
        p.Item3.Name,
        SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item2.OrderDate)
    })
    .Select(p => new
    {
        p.Item1.ProductId,
        ProductName = p.Item3.Name,
        SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item2.OrderDate),
        QuarterlyRevenue = SqlFunctions.Sql.sum(p.Item1.LineTotal),
        QuarterlyQty = SqlFunctions.Sql.sum((double)p.Item1.OrderQty)   // CAST(OrderQty AS FLOAT)
    });

// CTE ProductAggregates
var aggregates = ctx.From(quarterly)
    .GroupBy(q => new { q.ProductId, q.ProductName })
    .Select(q => new
    {
        q.ProductId,
        q.ProductName,
        TotalRevenue = SqlFunctions.Sql.sum(q.QuarterlyRevenue),
        AvgQty = SqlFunctions.Sql.avg(q.QuarterlyQty),
        StdevQty = SqlFunctions.Sql.stdev(q.QuarterlyQty)
    });

// CTE AbcRanking
var abc = ctx.From(aggregates)
    .Select(a => new
    {
        a.ProductName,
        a.TotalRevenue,
        a.AvgQty,
        a.StdevQty,
        RunningPercent = SqlFunctions.Sql.sum_over(a.TotalRevenue).Over(SqlFunctions.Sql.desc(() => a.TotalRevenue))
                         / SqlFunctions.Sql.sum_over(a.TotalRevenue).Over()
    });

var rows = await ctx.From(abc)
    .OrderByDescending(a => a.TotalRevenue)
    .Select(a => new
    {
        ProductName = a.ProductName,
        TotalRevenue = Math.Round(a.TotalRevenue ?? 0, 2),
        Abc = a.RunningPercent <= 0.80m ? "A"
            : a.RunningPercent <= 0.95m ? "B"
            : "C",
        Xyz = a.AvgQty == 0 || a.StdevQty == null ? "Z"
            : a.StdevQty / a.AvgQty < 0.15 ? "X"
            : a.StdevQty / a.AvgQty <= 0.30 ? "Y"
            : "Z"
    })
    .ToListAsync();
```

## 5. `mssql_quarterly_pivot.sql` — кросс-таблица маржинальности

`PIVOT` штатно не поддержан, поэтому используется эквивалентная условная агрегация. Чтобы пустая
ячейка осталась `NULL` (а не `0`, как дал бы `ELSE 0`), тернарник возвращает `null`, а округление
делается в конструкторе DTO (SQL `ROUND(NULL, 2)` тоже `NULL`):

```csharp
// CTE OrderMargins
var margins = ctx.From<ISalesOrderDetail>()
    .Join(ctx.From<ISalesOrderHeader>(), (sod, soh) => sod.SalesOrderId == soh.SalesOrderId)
    .Join(ctx.From<IProduct>(), (p, pr) => p.Item1.ProductId == pr.ProductId)
    .Join(ctx.From<IProductSubcategory>(), (p, psc) => p.Item3.ProductSubcategoryId == psc.ProductSubcategoryId)
    .Join(ctx.From<IProductCategory>(), (p, pc) => p.Item4.ProductCategoryId == pc.ProductCategoryId)
    .Where(p => p.Item2.OrderDate.Year == 2013)
    .Select(p => new
    {
        CategoryName = p.Item5.Name,
        Quarter = SqlFunctions.Sql.date_diff("quarter",
            SqlFunctions.Sql.date_from_parts(2013, 1, 1), p.Item2.OrderDate) + 1,
        Margin = p.Item1.LineTotal - (p.Item3.StandardCost * p.Item1.OrderQty)
    });

var rows = await ctx.From(margins)
    .GroupBy(m => new { m.CategoryName })
    .OrderBy(m => m.CategoryName)
    .Select(m => new QuarterlyPivotRow(
        m.CategoryName,
        SqlFunctions.Sql.sum(m.Quarter == 1 ? m.Margin : (decimal?)null),
        SqlFunctions.Sql.sum(m.Quarter == 2 ? m.Margin : (decimal?)null),
        SqlFunctions.Sql.sum(m.Quarter == 3 ? m.Margin : (decimal?)null),
        SqlFunctions.Sql.sum(m.Quarter == 4 ? m.Margin : (decimal?)null)))
    .ToListAsync();

public sealed class QuarterlyPivotRow
{
    public string CategoryName { get; }
    public decimal? Q1 { get; }
    public decimal? Q2 { get; }
    public decimal? Q3 { get; }
    public decimal? Q4 { get; }

    public QuarterlyPivotRow(string categoryName, decimal? q1, decimal? q2, decimal? q3, decimal? q4)
    {
        CategoryName = categoryName;
        Q1 = Round(q1);
        Q2 = Round(q2);
        Q3 = Round(q3);
        Q4 = Round(q4);
    }

    // ROUND на стороне SQL округляет «half away from zero»; .NET по умолчанию — к чётному.
    private static decimal? Round(decimal? value) =>
        value is null ? null : decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
}
```
