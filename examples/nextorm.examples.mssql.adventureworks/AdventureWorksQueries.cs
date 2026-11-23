using System.Linq.Expressions;
using NextORM.Core;
using NextORM.Examples.SqlServer.AdventureWorks.Generated;
using NextORM.Extensions;
using NextORM.Generated.nextorm_examples_mssql_adventureworks;
using NextORM.SqlServer;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models and states whether it is <c>WORKING</c>; when it is not, the comment says why (with the
/// roadmap document that tracks the gap). <c>README.md</c> maps every SQL construct to the nextorm
/// construct that replaces it. Each method returns its row list; the runner prints it.
/// </summary>
public static class AdventureWorksQueries
{
    // 1. Sql/mssql_vip_churn.sql
    // WORKING: two typed CTEs (`AsCte`) chained through an aliased inner chain; every slot of both chains
    // is named (`WithAlias`), including the CTE-producing chain, which consumes the fix for #230. The
    // metrics root projects the named `CustomerMetricRow` DTO because the alias surface cannot render an
    // anonymous chain type (#229). FORMAT uses DemoUdf.
    public static async Task<object> VipChurn(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        // CTE CustomerOrders; the manager join stays in the outer SELECT. AsCte makes it a typed
        // descriptor (member access) instead of reading it by name through TableAlias. The chain carries
        // aliases itself and is then consumed by the aliased CustomerMetrics chain below — before #230 the
        // downstream `Alias.Metrics`/`SalesPerson`/`Manager` were dropped from the generated marker.
        var customerOrders = ctx.SalesOrderHeader.Query().WithAlias(Alias.Order)
            .Join(ctx.Customer.Query(), (soh, c) => soh.Order.CustomerID == c.CustomerID, Alias.Customer)
            .Join(ctx.Person.Query(), (p, per) => p.Customer.PersonID == per.BusinessEntityID, Alias.Person)
            .Select(p => new
            {
                p.Order.CustomerID,
                CustomerName = p.Person.FirstName + " " + p.Person.LastName,
                p.Order.SalesOrderID,
                OrderDate = p.Order.OrderDate,
                TotalDue = p.Order.TotalDue,
                p.Order.SalesPersonID,
                LastCompanyOrderDate = SqlFunctions.Sql.max_over(p.Order.OrderDate).Over(),
                PrevOrderDate = SqlFunctions.Sql.lag(p.Order.OrderDate)
                    .Over(partitionBy: () => p.Order.CustomerID, orderBy: () => p.Order.OrderDate)
            })
            .AsCte("CustomerOrders");

        // CTE CustomerMetrics; reads the typed CustomerOrders descriptor, so the step is typed too.
        // Explicit GroupBy + Select, not SelectGroup: the join-alias generator's syntax recovery does not
        // recognise SelectGroup as the operator that establishes a consumed chain's element type, so the
        // downstream `Alias.Metrics`/`SalesPerson`/`Manager` members are not emitted.
        var metrics = ctx.DataContext.From(customerOrders)
            .GroupBy(c => new { c.CustomerID, c.CustomerName, c.SalesPersonID })
            .Select(c => new CustomerMetricRow
            {
                CustomerID = c.CustomerID,
                CustomerName = c.CustomerName,
                SalesPersonID = c.SalesPersonID,
                LifetimeValue = SqlFunctions.Sql.sum(c.TotalDue),
                LastPurchaseDate = SqlFunctions.Sql.max(c.OrderDate),
                DaysSinceLastOrder = SqlFunctions.Sql.date_diff("day",
                    SqlFunctions.Sql.max(c.OrderDate),
                    SqlFunctions.Sql.max(c.LastCompanyOrderDate))
            })
            .AsCte("CustomerMetrics");

        // Named slots (#160): the root is Alias.Metrics (named after the CTE passed to From), the two
        // LEFT JOINs are Alias.SalesPerson and Alias.Manager. SQL aliases stay t1/t2/t3; the names only add
        // typed projection members. The root must be a named type (CustomerMetricRow), not an anonymous
        // projection — see the DTO.
        var rows = await ctx.DataContext.From(metrics).WithAlias(Alias.Metrics)
            .LeftJoin(ctx.SalesPerson.Query(), (m, sp) => m.Metrics.SalesPersonID == sp.BusinessEntityID, Alias.SalesPerson)
            .LeftJoin(ctx.Person.Query(), (p, per) => p.SalesPerson.BusinessEntityID == per.BusinessEntityID, Alias.Manager)
            .Where(p => p.Metrics.LifetimeValue > 50000 && p.Metrics.DaysSinceLastOrder > 180)
            .OrderByDescending(p => p.Metrics.LifetimeValue)
            .Select(p => new
            {
                p.Metrics.CustomerID,
                p.Metrics.CustomerName,
                Ltv = Math.Round(p.Metrics.LifetimeValue, 2),
                LastPurchase = DemoUdf.Format(p.Metrics.LastPurchaseDate, "yyyy-MM-dd"),
                p.Metrics.DaysSinceLastOrder,
                Manager = p.Manager.FirstName + " " + p.Manager.LastName
            })
            .ToListAsync(ct);

        return rows;
    }

    // 2. Sql/mssql_rolling_kpi.sql
    // WORKING: typed CTE MonthlySales (`AsCte`); the outer query reads it with member access, formats via
    // DemoUdf and applies cumulative/3-month window frames.
    public static async Task<object> RollingKpi(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        // CTE MonthlySales; AsCte makes it a typed descriptor (member access) instead of reading it by name.
        var monthly = ctx.SalesOrderHeader.Query().WithAlias(Alias.Order)
            .Join(ctx.SalesTerritory.Query(), (soh, st) => soh.Order.TerritoryID == st.TerritoryID, Alias.Territory)
            .SelectGroup(p => new
            {
                RegionName = p.Territory.Name,
                SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Order.OrderDate),
                MonthlyRevenue = SqlFunctions.Sql.sum(p.Order.TotalDue)
            })
            .AsCte("MonthlySales");

        var rows = await ctx.DataContext.From(monthly)
            .OrderBy(m => m.RegionName)
            .OrderByDescending(m => m.SalesMonth)
            .Select(m => new
            {
                Region = m.RegionName,
                Month = DemoUdf.Format(m.SalesMonth, "yyyy-MM"),
                MonthlyRevenue = Math.Round(m.MonthlyRevenue, 2),
                Cumulative = Math.Round(
                    SqlFunctions.Sql.sum_over(m.MonthlyRevenue).Over(
                        partitionBy: () => m.RegionName,
                        orderBy: () => m.SalesMonth,
                        frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow),
                    2),
                MovingAvg3 = Math.Round(
                    SqlFunctions.Sql.avg_over(m.MonthlyRevenue).Over(
                        partitionBy: () => m.RegionName,
                        orderBy: () => m.SalesMonth,
                        frame: WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow))!.Value,
                    2)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 3. Sql/mssql_supply_chain.sql
    // WORKING: two typed CTEs (`AsCte`: SupplierDelays, ProductionImpact), both projected into named row
    // types; the outer query names the root and the joined CTE (`WithAlias(Alias.ProductionImpact)` +
    // `Alias.Supplier`) and reads each side through its named slot. The named types are required by the
    // alias surface (#229): it renders the chain's concrete type and refuses an anonymous one.
    public static async Task<object> SupplyChain(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        // CTE SupplierDelays: named joins like every other chain — the outer aliased chain joins this
        // descriptor with `Alias.Supplier`.
        var supplierDelays = ctx.PurchaseOrderDetail.Query().WithAlias(Alias.OrderDetail)
            .Join(ctx.PurchaseOrderHeader.Query(), (pod, poh) => pod.OrderDetail.PurchaseOrderID == poh.PurchaseOrderID, Alias.PurchaseOrder)
            .Join(ctx.Vendor.Query(), (p, v) => p.PurchaseOrder.VendorID == v.BusinessEntityID, Alias.Vendor)
            .Where(p => p.OrderDetail.ModifiedDate > SqlFunctions.Sql.date_add("day", 5, p.OrderDetail.DueDate))
            .Select(p => new SupplierDelayRow
            {
                ProductID = p.OrderDetail.ProductID,
                VendorID = p.PurchaseOrder.VendorID,
                VendorName = p.Vendor.Name,
                DelayDays = SqlFunctions.Sql.date_diff("day", p.OrderDetail.DueDate, p.OrderDetail.ModifiedDate)
            })
            .AsCte("SupplierDelays");

        // CTE ProductionImpact
        var productionImpact = ctx.WorkOrder.Query().WithAlias(Alias.WorkOrder)
            .Join(ctx.Product.Query(), (wo, pr) => wo.WorkOrder.ProductID == pr.ProductID, Alias.Product)
            .Where(p => p.WorkOrder.EndDate > p.WorkOrder.DueDate)
            .Select(p => new ProductionImpactRow
            {
                ProductID = p.WorkOrder.ProductID,
                ProductName = p.Product.Name,
                OrderQty = p.WorkOrder.OrderQty,
                ProdDelayDays = SqlFunctions.Sql.date_diff("day", p.WorkOrder.DueDate, p.WorkOrder.EndDate)
            })
            .AsCte("ProductionImpact");

        var rows = await ctx.DataContext.From(productionImpact).WithAlias(Alias.ProductionImpact)
            .Join(supplierDelays, (pi, sd) => pi.ProductionImpact.ProductID == sd.ProductID, Alias.Supplier)
            .OrderByDescending(p => new { p.ProductionImpact.ProdDelayDays, p.Supplier.DelayDays })
            .Select(p => new
            {
                Component = p.ProductionImpact.ProductName,
                Vendor = p.Supplier.VendorName,
                SupplyDelay = p.Supplier.DelayDays,
                ProductionQty = p.ProductionImpact.OrderQty,
                AssemblyShift = p.ProductionImpact.ProdDelayDays
            })
            .ToListAsync(ct);

        return rows;
    }

    // 4. Sql/mssql_product_abc_xyz.sql
    // WORKING: three chained typed CTEs (`AsCte`: ProductQuarterlySales, ProductAggregates, AbcRanking); the
    // running-share windows and CASE-based ABC/XYZ classes are computed over the CTEs. `avg`/`stdev` are
    // projected as nullable (`(double?)`) so the one-row-per-group case stays distinguishable from zero.
    public static async Task<object> ProductAbcXyz(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        // CTE ProductQuarterlySales.
        var quarterly = ctx.SalesOrderDetail.Query().WithAlias(Alias.OrderDetail)
            .Join(ctx.SalesOrderHeader.Query(), (sod, soh) => sod.OrderDetail.SalesOrderID == soh.SalesOrderID, Alias.OrderHeader)
            .Join(ctx.Product.Query(), (p, pr) => p.OrderDetail.ProductID == pr.ProductID, Alias.Product)
            .SelectGroup(p => new
            {
                p.OrderDetail.ProductID,
                ProductName = p.Product.Name,
                SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.OrderHeader.OrderDate),
                QuarterlyRevenue = SqlFunctions.Sql.sum(p.OrderDetail.LineTotal),
                QuarterlyQty = SqlFunctions.Sql.sum(p.OrderDetail.OrderQty)
            })
            .AsCte("ProductQuarterlySales");

        // CTE ProductAggregates.
        var aggregates = ctx.DataContext.From(quarterly)
            .SelectGroup(q => new
            {
                q.ProductID,
                q.ProductName,
                TotalRevenue = SqlFunctions.Sql.sum(q.QuarterlyRevenue),
                AvgQty = (double?)SqlFunctions.Sql.avg((double)q.QuarterlyQty),
                StdevQty = (double?)SqlFunctions.Sql.stdev((double)q.QuarterlyQty)
            })
            .AsCte("ProductAggregates");

        // CTE AbcRanking.
        var abc = ctx.DataContext.From(aggregates)
            .Select(a => new
            {
                a.ProductName,
                a.TotalRevenue,
                a.AvgQty,
                a.StdevQty,
                RunningPercent = SqlFunctions.Sql.sum_over(a.TotalRevenue)
                                      .Over(SqlFunctions.Sql.desc(() => a.TotalRevenue))
                                  / SqlFunctions.Sql.sum_over(a.TotalRevenue).Over()
            })
            .AsCte("AbcRanking");

        var rows = await ctx.DataContext.From(abc)
            .OrderByDescending(a => a.TotalRevenue)
            .Select(a => new
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
            })
            .ToListAsync(ct);

        return rows;
    }

    // 5. Sql/mssql_quarterly_pivot.sql
    // WORKING: a derived query (5-table join with a computed Margin and FOR column) reshaped by the
    // native PIVOT. The typed overload declares the result members (the grouping category and the four
    // quarter cells) and each pivot value once, deriving the IN list from the projection.
    public static async Task<object> QuarterlyPivot(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var orderMargins = ctx.SalesOrderDetail.Query().WithAlias(Alias.OrderDetail)
            .Join(ctx.SalesOrderHeader.Query(), (sod, soh) => sod.OrderDetail.SalesOrderID == soh.SalesOrderID, Alias.OrderHeader)
            .Join(ctx.Product.Query(), (p, pr) => p.OrderDetail.ProductID == pr.ProductID, Alias.Product)
            .Join(ctx.ProductSubcategory.Query(), (p, psc) => p.Product.ProductSubcategoryID == psc.ProductSubcategoryID, Alias.Subcategory)
            .Join(ctx.ProductCategory.Query(), (p, pc) => p.Subcategory.ProductCategoryID == pc.ProductCategoryID, Alias.Category)
            .Where(p => SqlFunctions.Sql.extract("year", p.OrderHeader.OrderDate) == 2013)
            .Select(p => new
            {
                CategoryName = p.Category.Name,
                QuarterNum = SqlFunctions.Sql.extract("quarter", p.OrderHeader.OrderDate),
                Margin = p.OrderDetail.LineTotal - p.Product.StandardCost * p.OrderDetail.OrderQty
            });

        var rows = await ctx.DataContext.From(orderMargins)
            .Pivot(
                PivotAggregate.Sum,
                m => m.Margin,
                m => m.QuarterNum,
                (m, pivot) => new
                {
                    m.CategoryName,
                    Q1 = pivot.Value<decimal?>("1"),
                    Q2 = pivot.Value<decimal?>("2"),
                    Q3 = pivot.Value<decimal?>("3"),
                    Q4 = pivot.Value<decimal?>("4")
                })
            .OrderBy(x => x.CategoryName)
            .Select(x => new
            {
                Category = x.CategoryName,
                x.Q1,
                x.Q2,
                x.Q3,
                x.Q4
            })
            .ToListAsync(ct);

        return rows;
    }

    // 6. top_products_by_category
    // WORKING: ROW_NUMBER() OVER (PARTITION BY category ORDER BY revenue DESC) top-N per group.
    public static async Task<object> TopProductsByCategory(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var productRevenue = ctx.SalesOrderDetail.Query().WithAlias(Alias.OrderDetail)
            .Join(ctx.SalesOrderHeader.Query(), (sod, soh) => sod.OrderDetail.SalesOrderID == soh.SalesOrderID, Alias.OrderHeader)
            .Join(ctx.Product.Query(), (p, pr) => p.OrderDetail.ProductID == pr.ProductID, Alias.Product)
            .Join(ctx.ProductSubcategory.Query(), (p, psc) => p.Product.ProductSubcategoryID == psc.ProductSubcategoryID, Alias.Subcategory)
            .Join(ctx.ProductCategory.Query(), (p, pc) => p.Subcategory.ProductCategoryID == pc.ProductCategoryID, Alias.Category)
            .SelectGroup(p => new
            {
                Category = p.Category.Name,
                ProductId = p.OrderDetail.ProductID,
                Product = p.Product.Name,
                Revenue = SqlFunctions.Sql.sum(p.OrderDetail.LineTotal)
            });

        var ranked = ctx.DataContext.From(productRevenue)
            .Select(p => new
            {
                p.Category,
                p.Product,
                p.Revenue,
                Place = SqlFunctions.Sql.row_number().Over(
                    partitionBy: new Expression<Func<object?>>[] { () => p.Category },
                    orderBy: new[] { SqlFunctions.Sql.desc(() => p.Revenue) })
            });

        var rows = await ctx.DataContext.From(ranked)
            .Where(p => p.Place <= 3)
            .OrderBy(p => new { p.Category, p.Place })
            .Select(p => new
            {
                p.Category,
                p.Place,
                p.Product,
                Revenue = Math.Round(p.Revenue, 2)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 7. territory_yoy
    // WORKING: LAG over a yearly aggregate (year-over-year growth).
    public static async Task<object> TerritoryYearOverYear(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var yearly = ctx.SalesOrderHeader.Query().WithAlias(Alias.Order)
            .Join(ctx.SalesTerritory.Query(), (soh, st) => soh.Order.TerritoryID == st.TerritoryID, Alias.Territory)
            .SelectGroup(p => new
            {
                Region = p.Territory.Name,
                Year = p.Order.OrderDate.Year,
                Revenue = SqlFunctions.Sql.sum(p.Order.TotalDue)
            });

        var withPrevious = ctx.DataContext.From(yearly)
            .Select(y => new
            {
                y.Region,
                y.Year,
                y.Revenue,
                PrevRevenue = SqlFunctions.Sql.lag(y.Revenue)
                    .Over(partitionBy: () => y.Region, orderBy: () => y.Year)
            });

        var rows = await ctx.DataContext.From(withPrevious)
            .OrderBy(y => new { y.Region, y.Year })
            .Select(y => new
            {
                y.Region,
                y.Year,
                Revenue = Math.Round(y.Revenue, 2),
                GrowthPct = y.PrevRevenue <= 0m
                    ? (decimal?)null
                    : Math.Round((y.Revenue - y.PrevRevenue) / y.PrevRevenue * 100, 1)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 8. customer_rfm
    // WORKING: NTILE(4) OVER (ORDER BY … DESC) RFM segmentation.
    public static async Task<object> CustomerRfm(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var rfm = ctx.SalesOrderHeader.Query().WithAlias(Alias.Order)
            .Join(ctx.Customer.Query(), (soh, c) => soh.Order.CustomerID == c.CustomerID, Alias.Customer)
            .Join(ctx.Person.Query(), (p, per) => p.Customer.PersonID == per.BusinessEntityID, Alias.Person)
            .SelectGroup(p => new
            {
                CustomerId = p.Order.CustomerID,
                Name = p.Person.FirstName + " " + p.Person.LastName,
                Frequency = SqlFunctions.Sql.count(),
                Monetary = SqlFunctions.Sql.sum(p.Order.TotalDue),
                Recency = SqlFunctions.Sql.max(p.Order.OrderDate)
            });

        var scored = ctx.DataContext.From(rfm)
            .Select(r => new
            {
                r.CustomerId,
                r.Name,
                r.Frequency,
                r.Monetary,
                FScore = SqlFunctions.Sql.ntile(4).Over(SqlFunctions.Sql.desc(() => r.Frequency)),
                MScore = SqlFunctions.Sql.ntile(4).Over(SqlFunctions.Sql.desc(() => r.Monetary)),
                RScore = SqlFunctions.Sql.ntile(4).Over(SqlFunctions.Sql.desc(() => r.Recency))
            });

        var rows = await ctx.DataContext.From(scored)
            .OrderByDescending(r => r.Monetary)
            .Limit(20)
            .Select(r => new
            {
                r.CustomerId,
                r.Name,
                r.Frequency,
                Monetary = Math.Round(r.Monetary, 2),
                Segment = r.RScore == 1 && r.FScore == 1 && r.MScore == 1 ? "Champions"
                    : r.RScore <= 2 && r.FScore <= 2 ? "Loyal"
                    : r.RScore >= 3 && r.FScore >= 3 ? "At risk"
                    : "Others"
            })
            .ToListAsync(ct);

        return rows;
    }

    // 9. quota_attainment
    // WORKING: per-entity target vs actual, computed in Select.
    public static async Task<object> QuotaAttainment(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.SalesPerson.Query().WithAlias(Alias.SalesPerson)
            .Join(ctx.Person.Query(), (sp, per) => sp.SalesPerson.BusinessEntityID == per.BusinessEntityID, Alias.Person)
            .Where(p => p.SalesPerson.SalesQuota != null)
            .OrderByDescending(p => p.SalesPerson.SalesYTD / p.SalesPerson.SalesQuota)
            .Select(p => new
            {
                Name = p.Person.FirstName + " " + p.Person.LastName,
                Quota = p.SalesPerson.SalesQuota,
                SalesYtd = p.SalesPerson.SalesYTD,
                AttainmentPct = Math.Round((p.SalesPerson.SalesYTD / p.SalesPerson.SalesQuota * 100) ?? 0m, 1)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 10. territory_growth_mom
    // WORKING: LAG over a monthly aggregate (month-over-month % change).
    public static async Task<object> TerritoryGrowthMonthOverMonth(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var monthly = ctx.SalesOrderHeader.Query().WithAlias(Alias.Order)
            .Join(ctx.SalesTerritory.Query(), (soh, st) => soh.Order.TerritoryID == st.TerritoryID, Alias.Territory)
            .SelectGroup(p => new
            {
                Region = p.Territory.Name,
                SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Order.OrderDate),
                MonthlyRevenue = SqlFunctions.Sql.sum(p.Order.TotalDue)
            });

        var withPrevious = ctx.DataContext.From(monthly)
            .Select(m => new
            {
                m.Region,
                m.SalesMonth,
                m.MonthlyRevenue,
                PrevRevenue = SqlFunctions.Sql.lag(m.MonthlyRevenue)
                    .Over(partitionBy: () => m.Region, orderBy: () => m.SalesMonth)
            });

        var rows = await ctx.DataContext.From(withPrevious)
            .OrderBy(m => new { m.Region, m.SalesMonth })
            .Select(m => new
            {
                m.Region,
                Month = m.SalesMonth,
                Revenue = Math.Round(m.MonthlyRevenue, 2),
                MomPct = m.PrevRevenue <= 0m
                    ? (decimal?)null
                    : Math.Round((m.MonthlyRevenue - m.PrevRevenue) / m.PrevRevenue * 100, 1)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 11. customer_pareto
    // WORKING: ABC / Pareto 80-20 concentration with running window shares.
    public static async Task<object> CustomerPareto(AdventureWorksRepositoryContext ctx, CancellationToken ct)
    {
        var customerRevenue = ctx.SalesOrderHeader.Query()
            .SelectGroup(soh => new { CustomerId = soh.CustomerID, Revenue = SqlFunctions.Sql.sum(soh.TotalDue) });

        var ranked = ctx.DataContext.From(customerRevenue)
            .Select(c => new
            {
                c.CustomerId,
                c.Revenue,
                RunningRevenue = SqlFunctions.Sql.sum_over(c.Revenue).Over(SqlFunctions.Sql.desc(() => c.Revenue)),
                TotalRevenue = SqlFunctions.Sql.sum_over(c.Revenue).Over()
            });

        var rows = await ctx.DataContext.From(ranked)
            .OrderByDescending(c => c.Revenue)
            .Limit(40)
            .Select(c => new
            {
                c.CustomerId,
                Revenue = Math.Round(c.Revenue, 2),
                CumulativePct = Math.Round(c.RunningRevenue / c.TotalRevenue * 100, 2),
                Class = c.RunningRevenue / c.TotalRevenue <= 0.8m ? "Vital (A)" : "Rest"
            })
            .ToListAsync(ct);

        return rows;
    }

}

// A named projection for the CustomerMetrics CTE. The named join-alias surface (#160) generates an
// extension whose receiver is the chain's concrete type, which cannot be rendered for an anonymous
// type, so the aliased chain must be rooted at a named type.
public sealed class CustomerMetricRow
{
    public int CustomerID { get; init; }
    public string CustomerName { get; init; } = "";
    public int? SalesPersonID { get; init; }
    public decimal LifetimeValue { get; init; }
    public DateTime LastPurchaseDate { get; init; }
    public int? DaysSinceLastOrder { get; init; }
}

// Named projections for the SupplyChain CTEs: both sides of the aliased outer join are named types
// (`Alias.ProductionImpact`/`Alias.Supplier`), so neither may be anonymous (#229).
public sealed class SupplierDelayRow
{
    public int ProductID { get; init; }
    public int VendorID { get; init; }
    public string VendorName { get; init; } = "";
    public int? DelayDays { get; init; }
}

public sealed class ProductionImpactRow
{
    public int ProductID { get; init; }
    public string ProductName { get; init; } = "";
    public int OrderQty { get; init; }
    public int? ProdDelayDays { get; init; }
}
