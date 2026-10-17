using LinqToDB;
using LinqToDB.Async;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// The comparison arm: the same eleven queries as <see cref="AdventureWorksQueries"/> (which model the
/// files in <c>Sql/</c>), expressed with linq2db over <see cref="Linq2DbEntities"/>. T-SQL
/// <c>FORMAT</c> and the <c>DATEADD(quarter, DATEDIFF(quarter, 0, …), 0)</c> month/quarter-start idiom
/// are emitted with <see cref="Sql.Expr{T}"/>; the <c>PIVOT</c> query is expressed as the equivalent
/// conditional aggregation, which is what PIVOT is rewritten to internally. Methods return the row list
/// and never print: the runner and the benchmarks share them.
/// </summary>
public static class Linq2DbQueries
{
    // 1. Sql/mssql_vip_churn.sql
    public static async Task<object> VipChurn(IDataContext db, CancellationToken ct)
    {
        var customerOrders = (from soh in db.GetTable<AwSalesOrderHeader>()
                              join c in db.GetTable<AwCustomer>() on soh.CustomerId equals c.CustomerId
                              join p in db.GetTable<AwPerson>() on c.PersonId equals (int?)p.BusinessEntityId
                              select new
                              {
                                  CustomerID = soh.CustomerId,
                                  CustomerName = p.FirstName + " " + p.LastName,
                                  SalesOrderID = soh.SalesOrderId,
                                  OrderDate = soh.OrderDate,
                                  TotalDue = soh.TotalDue,
                                  SalesPersonID = soh.SalesPersonId,
                                  LastCompanyOrderDate = Sql.Expr<DateTime>($"MAX({soh.OrderDate}) OVER ()"),
                                  PrevOrderDate = Sql.Window.Lag(soh.OrderDate,
                                      w => w.PartitionBy(soh.CustomerId).OrderBy(soh.OrderDate))
                              }).AsCte("CustomerOrders");

        var metrics = (from c in customerOrders
                       group c by new { c.CustomerID, c.CustomerName, c.SalesPersonID } into g
                       select new CustomerMetricRow
                       {
                           CustomerID = g.Key.CustomerID,
                           CustomerName = g.Key.CustomerName,
                           SalesPersonID = g.Key.SalesPersonID,
                           LifetimeValue = g.Sum(x => x.TotalDue),
                           LastPurchaseDate = g.Max(x => x.OrderDate),
                           DaysSinceLastOrder = Sql.DateDiff(Sql.DateParts.Day,
                               g.Max(x => x.OrderDate), g.Max(x => x.LastCompanyOrderDate))
                       }).AsCte("CustomerMetrics");

        return await (from m in metrics
                      join sp in db.GetTable<AwSalesPerson>() on m.SalesPersonID equals (int?)sp.BusinessEntityId into sps
                      from sp in sps.DefaultIfEmpty()
                      join per in db.GetTable<AwPerson>() on sp.BusinessEntityId equals per.BusinessEntityId into pers
                      from per in pers.DefaultIfEmpty()
                      where m.LifetimeValue > 50000 && m.DaysSinceLastOrder > 180
                      orderby m.LifetimeValue descending
                      select new
                      {
                          m.CustomerID,
                          m.CustomerName,
                          Ltv = Math.Round(m.LifetimeValue, 2),
                          LastPurchase = Sql.Expr<string?>($"FORMAT({m.LastPurchaseDate}, 'yyyy-MM-dd')"),
                          m.DaysSinceLastOrder,
                          Manager = per.FirstName + " " + per.LastName
                      }).ToListAsync(ct);
    }

    // 2. Sql/mssql_rolling_kpi.sql
    public static async Task<object> RollingKpi(IDataContext db, CancellationToken ct)
    {
        var monthly = (from soh in db.GetTable<AwSalesOrderHeader>()
                       join st in db.GetTable<AwSalesTerritory>() on soh.TerritoryId equals st.TerritoryId
                       group soh by new
                       {
                           RegionName = st.Name,
                           SalesMonth = Sql.DateAdd(Sql.DateParts.Month,
                               Sql.DateDiff(Sql.DateParts.Month, Sql.Expr<DateTime>($"0"), soh.OrderDate),
                               Sql.Expr<DateTime>($"0"))
                       } into g
                       select new
                       {
                           g.Key.RegionName,
                           g.Key.SalesMonth,
                           MonthlyRevenue = g.Sum(x => x.TotalDue)
                       }).AsCte("MonthlySales");

        return await (from m in monthly
                      orderby m.RegionName, m.SalesMonth descending
                      select new
                      {
                          Region = m.RegionName,
                          Month = Sql.Expr<string?>($"FORMAT({m.SalesMonth}, 'yyyy-MM')"),
                          MonthlyRevenue = Math.Round(m.MonthlyRevenue, 2),
                          Cumulative = Math.Round(Sql.Window.Sum(m.MonthlyRevenue,
                              w => w.PartitionBy(m.RegionName).OrderBy(m.SalesMonth)
                                  .RowsBetween.Unbounded.And.CurrentRow), 2),
                          MovingAvg3 = Math.Round(Sql.Window.Average(m.MonthlyRevenue,
                              w => w.PartitionBy(m.RegionName).OrderBy(m.SalesMonth)
                                  .RowsBetween.ValuePreceding(2).And.CurrentRow), 2)
                      }).ToListAsync(ct);
    }

    // 3. Sql/mssql_supply_chain.sql
    public static async Task<object> SupplyChain(IDataContext db, CancellationToken ct)
    {
        var supplierDelays = (from pod in db.GetTable<AwPurchaseOrderDetail>()
                              join poh in db.GetTable<AwPurchaseOrderHeader>() on pod.PurchaseOrderId equals poh.PurchaseOrderId
                              join v in db.GetTable<AwVendor>() on poh.VendorId equals v.BusinessEntityId
                              where pod.ModifiedDate > Sql.DateAdd(Sql.DateParts.Day, 5, pod.DueDate)
                              select new
                              {
                                  ProductID = pod.ProductId,
                                  VendorID = poh.VendorId,
                                  VendorName = v.Name,
                                  DelayDays = Sql.DateDiff(Sql.DateParts.Day, pod.DueDate, pod.ModifiedDate)
                              }).AsCte("SupplierDelays");

        var productionImpact = (from wo in db.GetTable<AwWorkOrder>()
                                join pr in db.GetTable<AwProduct>() on wo.ProductId equals pr.ProductId
                                where wo.EndDate > wo.DueDate
                                select new
                                {
                                    ProductID = wo.ProductId,
                                    ProductName = pr.Name,
                                    OrderQty = wo.OrderQty,
                                    ProdDelayDays = Sql.DateDiff(Sql.DateParts.Day, wo.DueDate, wo.EndDate)
                                }).AsCte("ProductionImpact");

        return await (from pi in productionImpact
                      join sd in supplierDelays on pi.ProductID equals sd.ProductID
                      orderby pi.ProdDelayDays descending, sd.DelayDays descending
                      select new
                      {
                          Component = pi.ProductName,
                          Vendor = sd.VendorName,
                          SupplyDelay = sd.DelayDays,
                          ProductionQty = pi.OrderQty,
                          AssemblyShift = pi.ProdDelayDays
                      }).ToListAsync(ct);
    }

    // 4. Sql/mssql_product_abc_xyz.sql
    public static async Task<object> ProductAbcXyz(IDataContext db, CancellationToken ct)
    {
        var quarterly = (from sod in db.GetTable<AwSalesOrderDetail>()
                         join soh in db.GetTable<AwSalesOrderHeader>() on sod.SalesOrderId equals soh.SalesOrderId
                         join pr in db.GetTable<AwProduct>() on sod.ProductId equals pr.ProductId
                         group new { sod, soh, pr } by new
                         {
                             sod.ProductId,
                             ProductName = pr.Name,
                             SalesQuarter = Sql.DateAdd(Sql.DateParts.Quarter,
                                 Sql.DateDiff(Sql.DateParts.Quarter, Sql.Expr<DateTime>($"0"), soh.OrderDate),
                                 Sql.Expr<DateTime>($"0"))
                         } into g
                         select new
                         {
                             g.Key.ProductId,
                             g.Key.ProductName,
                             g.Key.SalesQuarter,
                             QuarterlyRevenue = g.Sum(x => x.sod.LineTotal),
                             QuarterlyQty = g.Sum(x => x.sod.OrderQty)
                         }).AsCte("ProductQuarterlySales");

        var aggregates = (from q in quarterly
                          group q by new { q.ProductId, q.ProductName } into g
                          select new
                          {
                              g.Key.ProductId,
                              g.Key.ProductName,
                              TotalRevenue = g.Sum(x => x.QuarterlyRevenue),
                              AvgQty = (double?)g.Average(x => (double)x.QuarterlyQty),
                              StdevQty = (double?)LinqToDB.AnalyticFunctions.StdDev(g, x => (double)x.QuarterlyQty)
                          }).AsCte("ProductAggregates");

        var abc = (from a in aggregates
                   select new
                   {
                       a.ProductName,
                       a.TotalRevenue,
                       a.AvgQty,
                       a.StdevQty,
                       RunningPercent = Sql.Window.Sum(a.TotalRevenue, w => w.OrderByDesc(a.TotalRevenue))
                                        / Sql.Window.Sum(a.TotalRevenue, w => w.RowsBetween.Unbounded.And.Unbounded)
                   }).AsCte("AbcRanking");

        return await (from a in abc
                      orderby a.TotalRevenue descending
                      select new
                      {
                          a.ProductName,
                          TotalRevenue = Math.Round(a.TotalRevenue, 2),
                          Abc = a.RunningPercent <= 0.80m ? "A"
                              : a.RunningPercent <= 0.95m ? "B"
                              : "C",
                          Xyz = a.AvgQty == 0 || a.StdevQty == null ? "Z"
                              : a.StdevQty / a.AvgQty < 0.15 ? "X"
                              : a.StdevQty / a.AvgQty <= 0.30 ? "Y"
                              : "Z"
                      }).ToListAsync(ct);
    }

    // 5. Sql/mssql_quarterly_pivot.sql
    // PIVOT is conditional aggregation in disguise: GROUP BY the row key, then one SUM(CASE …) per
    // quarter. nextorm uses the native PIVOT; the result set is identical.
    public static async Task<object> QuarterlyPivot(IDataContext db, CancellationToken ct)
    {
        var orderMargins = from sod in db.GetTable<AwSalesOrderDetail>()
                           join soh in db.GetTable<AwSalesOrderHeader>() on sod.SalesOrderId equals soh.SalesOrderId
                           join pr in db.GetTable<AwProduct>() on sod.ProductId equals pr.ProductId
                           join psc in db.GetTable<AwProductSubcategory>() on pr.ProductSubcategoryId equals psc.ProductSubcategoryId
                           join pc in db.GetTable<AwProductCategory>() on psc.ProductCategoryId equals pc.ProductCategoryId
                           where Sql.DatePart(Sql.DateParts.Year, soh.OrderDate) == 2013
                           select new
                           {
                               CategoryName = pc.Name,
                               QuarterNum = Sql.DatePart(Sql.DateParts.Quarter, soh.OrderDate),
                               Margin = sod.LineTotal - pr.StandardCost * sod.OrderQty
                           };

        return await (from m in orderMargins
                      group m by m.CategoryName into g
                      orderby g.Key
                      select new
                      {
                          Category = g.Key,
                          Q1 = g.Sum(x => x.QuarterNum == 1 ? x.Margin : (decimal?)null),
                          Q2 = g.Sum(x => x.QuarterNum == 2 ? x.Margin : (decimal?)null),
                          Q3 = g.Sum(x => x.QuarterNum == 3 ? x.Margin : (decimal?)null),
                          Q4 = g.Sum(x => x.QuarterNum == 4 ? x.Margin : (decimal?)null)
                      }).ToListAsync(ct);
    }

    // 6. top_products_by_category
    public static async Task<object> TopProductsByCategory(IDataContext db, CancellationToken ct)
    {
        var productRevenue = from sod in db.GetTable<AwSalesOrderDetail>()
                             join soh in db.GetTable<AwSalesOrderHeader>() on sod.SalesOrderId equals soh.SalesOrderId
                             join pr in db.GetTable<AwProduct>() on sod.ProductId equals pr.ProductId
                             join psc in db.GetTable<AwProductSubcategory>() on pr.ProductSubcategoryId equals psc.ProductSubcategoryId
                             join pc in db.GetTable<AwProductCategory>() on psc.ProductCategoryId equals pc.ProductCategoryId
                             group new { sod, pr, pc } by new { Category = pc.Name, sod.ProductId, Product = pr.Name } into g
                             select new
                             {
                                 g.Key.Category,
                                 g.Key.ProductId,
                                 g.Key.Product,
                                 Revenue = g.Sum(x => x.sod.LineTotal)
                             };

        var ranked = from p in productRevenue
                     select new
                     {
                         p.Category,
                         p.Product,
                         p.Revenue,
                         Place = Sql.Window.RowNumber(w => w.PartitionBy(p.Category).OrderByDesc(p.Revenue))
                     };

        return await (from p in ranked
                      where p.Place <= 3
                      orderby p.Category, p.Place
                      select new
                      {
                          p.Category,
                          p.Place,
                          p.Product,
                          Revenue = Math.Round(p.Revenue, 2)
                      }).ToListAsync(ct);
    }

    // 7. territory_yoy
    public static async Task<object> TerritoryYearOverYear(IDataContext db, CancellationToken ct)
    {
        var yearly = from soh in db.GetTable<AwSalesOrderHeader>()
                     join st in db.GetTable<AwSalesTerritory>() on soh.TerritoryId equals st.TerritoryId
                     group soh by new { Region = st.Name, Year = soh.OrderDate.Year } into g
                     select new
                     {
                         g.Key.Region,
                         g.Key.Year,
                         Revenue = g.Sum(x => x.TotalDue)
                     };

        var withPrevious = from y in yearly
                           select new
                           {
                               y.Region,
                               y.Year,
                               y.Revenue,
                               PrevRevenue = Sql.Window.Lag(y.Revenue,
                                   w => w.PartitionBy(y.Region).OrderBy(y.Year))
                           };

        return await (from y in withPrevious
                      orderby y.Region, y.Year
                      select new
                      {
                          y.Region,
                          y.Year,
                          Revenue = Math.Round(y.Revenue, 2),
                          GrowthPct = y.PrevRevenue <= 0m
                              ? (decimal?)null
                              : Math.Round((y.Revenue - y.PrevRevenue) / y.PrevRevenue * 100, 1)
                      }).ToListAsync(ct);
    }

    // 8. customer_rfm
    public static async Task<object> CustomerRfm(IDataContext db, CancellationToken ct)
    {
        var rfm = from soh in db.GetTable<AwSalesOrderHeader>()
                  join c in db.GetTable<AwCustomer>() on soh.CustomerId equals c.CustomerId
                  join per in db.GetTable<AwPerson>() on c.PersonId equals per.BusinessEntityId
                  group soh by new { soh.CustomerId, Name = per.FirstName + " " + per.LastName } into g
                  select new
                  {
                      g.Key.CustomerId,
                      g.Key.Name,
                      Frequency = g.Count(),
                      Monetary = g.Sum(x => x.TotalDue),
                      Recency = g.Max(x => x.OrderDate)
                  };

        var scored = from r in rfm
                     select new
                     {
                         r.CustomerId,
                         r.Name,
                         r.Frequency,
                         r.Monetary,
                         FScore = Sql.Window.NTile(4, w => w.OrderByDesc(r.Frequency)),
                         MScore = Sql.Window.NTile(4, w => w.OrderByDesc(r.Monetary)),
                         RScore = Sql.Window.NTile(4, w => w.OrderByDesc(r.Recency))
                     };

        return await (from r in scored
                      orderby r.Monetary descending
                      select new
                      {
                          r.CustomerId,
                          r.Name,
                          r.Frequency,
                          Monetary = Math.Round(r.Monetary, 2),
                          Segment = r.RScore == 1 && r.FScore == 1 && r.MScore == 1 ? "Champions"
                              : r.RScore <= 2 && r.FScore <= 2 ? "Loyal"
                              : r.RScore >= 3 && r.FScore >= 3 ? "At risk"
                              : "Others"
                      }).Take(20).ToListAsync(ct);
    }

    // 9. quota_attainment
    public static async Task<object> QuotaAttainment(IDataContext db, CancellationToken ct)
    {
        return await (from sp in db.GetTable<AwSalesPerson>()
                      join per in db.GetTable<AwPerson>() on sp.BusinessEntityId equals per.BusinessEntityId
                      where sp.SalesQuota != null
                      orderby sp.SalesYtd / sp.SalesQuota descending
                      select new
                      {
                          Name = per.FirstName + " " + per.LastName,
                          Quota = sp.SalesQuota,
                          SalesYtd = sp.SalesYtd,
                          AttainmentPct = Math.Round((sp.SalesYtd / sp.SalesQuota * 100) ?? 0m, 1)
                      }).ToListAsync(ct);
    }

    // 10. territory_growth_mom
    public static async Task<object> TerritoryGrowthMonthOverMonth(IDataContext db, CancellationToken ct)
    {
        var monthly = from soh in db.GetTable<AwSalesOrderHeader>()
                      join st in db.GetTable<AwSalesTerritory>() on soh.TerritoryId equals st.TerritoryId
                      group soh by new
                      {
                          Region = st.Name,
                          SalesMonth = Sql.DateAdd(Sql.DateParts.Month,
                              Sql.DateDiff(Sql.DateParts.Month, Sql.Expr<DateTime>($"0"), soh.OrderDate),
                              Sql.Expr<DateTime>($"0"))
                      } into g
                      select new
                      {
                          g.Key.Region,
                          g.Key.SalesMonth,
                          MonthlyRevenue = g.Sum(x => x.TotalDue)
                      };

        var withPrevious = from m in monthly
                           select new
                           {
                               m.Region,
                               m.SalesMonth,
                               m.MonthlyRevenue,
                               PrevRevenue = Sql.Window.Lag(m.MonthlyRevenue,
                                   w => w.PartitionBy(m.Region).OrderBy(m.SalesMonth))
                           };

        return await (from m in withPrevious
                      orderby m.Region, m.SalesMonth
                      select new
                      {
                          m.Region,
                          Month = m.SalesMonth,
                          Revenue = Math.Round(m.MonthlyRevenue, 2),
                          MomPct = m.PrevRevenue <= 0m
                              ? (decimal?)null
                              : Math.Round((m.MonthlyRevenue - m.PrevRevenue) / m.PrevRevenue * 100, 1)
                      }).ToListAsync(ct);
    }

    // 11. customer_pareto
    public static async Task<object> CustomerPareto(IDataContext db, CancellationToken ct)
    {
        var customerRevenue = from soh in db.GetTable<AwSalesOrderHeader>()
                              group soh by soh.CustomerId into g
                              select new { CustomerId = g.Key, Revenue = g.Sum(x => x.TotalDue) };

        var ranked = from c in customerRevenue
                     select new
                     {
                         c.CustomerId,
                         c.Revenue,
                         RunningRevenue = Sql.Window.Sum(c.Revenue, w => w.OrderByDesc(c.Revenue)),
                         TotalRevenue = Sql.Window.Sum(c.Revenue, w => w.RowsBetween.Unbounded.And.Unbounded)
                     };

        return await (from c in ranked
                      orderby c.Revenue descending
                      select new
                      {
                          c.CustomerId,
                          Revenue = Math.Round(c.Revenue, 2),
                          CumulativePct = Math.Round(c.RunningRevenue / c.TotalRevenue * 100, 2),
                          Class = c.RunningRevenue / c.TotalRevenue <= 0.8m ? "Vital (A)" : "Rest"
                      }).Take(40).ToListAsync(ct);
    }
}
