using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// The five demo queries from <c>docs/specs/mssql-demodb</c> plus six extra course-style exercises
/// (see <c>examples/README.md</c>), expressed with the nextorm LINQ API.
/// </summary>
public static class AdventureWorksQueries
{
    // 1. mssql_vip_churn.sql
    public static async Task VipChurn(IDataContext ctx, CancellationToken ct)
    {
        // CTE CustomerOrders; the manager join stays in the outer SELECT.
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

        // Joining a derived query as the primary FROM source is not supported yet; this mirrors the
        // original and is expected to fail during preparation. See
        // docs/specs/roadmap/sql-capabilities-gap-analysis.md, known gap 10.
        var rows = await ctx.From(metrics)
            .LeftJoin(ctx.From<ISalesPerson>(), (m, sp) => m.SalesPersonId == sp.BusinessEntityId)
            .LeftJoin(ctx.From<IPerson>(), (p, per) => p.Item2.BusinessEntityId == per.BusinessEntityId)
            .Where(p => p.Item1.LifetimeValue > 50000 && p.Item1.DaysSinceLastOrder > 180)
            .OrderByDescending(p => p.Item1.LifetimeValue)
            .Select(p => new
            {
                p.Item1.CustomerId,
                p.Item1.CustomerName,
                Ltv = Math.Round(p.Item1.LifetimeValue, 2),
                LastPurchase = p.Item1.LastPurchaseDate,
                p.Item1.DaysSinceLastOrder,
                Manager = p.Item3.FirstName + " " + p.Item3.LastName
            })
            .ToListAsync(ct);

        Print("1. vip_churn", rows);
    }

    // 2. mssql_rolling_kpi.sql
    public static async Task RollingKpi(IDataContext ctx, CancellationToken ct)
    {
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
                Month = m.SalesMonth,
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
            .ToListAsync(ct);

        Print("2. rolling_kpi", rows);
    }

    // 3. mssql_supply_chain.sql
    public static async Task SupplyChain(IDataContext ctx, CancellationToken ct)
    {
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

        // Joining a derived query as the primary FROM source is not supported yet; this mirrors the
        // original and is expected to fail during preparation. See
        // docs/specs/roadmap/sql-capabilities-gap-analysis.md, known gap 10.
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
            .ToListAsync(ct);

        Print("3. supply_chain", rows);
    }

    // 4. mssql_product_abc_xyz.sql
    public static async Task ProductAbcXyz(IDataContext ctx, CancellationToken ct)
    {
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
                QuarterlyQty = SqlFunctions.Sql.sum(p.Item1.OrderQty)
            });

        var aggregates = ctx.From(quarterly)
            .GroupBy(q => new { q.ProductId, q.ProductName })
            .Select(q => new
            {
                q.ProductId,
                q.ProductName,
                TotalRevenue = SqlFunctions.Sql.sum(q.QuarterlyRevenue),
                AvgQty = SqlFunctions.Sql.avg((double)q.QuarterlyQty),
                StdevQty = SqlFunctions.Sql.stdev((double)q.QuarterlyQty)
            });

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
                a.ProductName,
                TotalRevenue = Math.Round(a.TotalRevenue, 2),
                Abc = a.RunningPercent <= 0.80m ? "A"
                    : a.RunningPercent <= 0.95m ? "B"
                    : "C",
                Xyz = a.AvgQty == 0 ? "Z"
                    : a.StdevQty / a.AvgQty < 0.15 ? "X"
                    : a.StdevQty / a.AvgQty <= 0.30 ? "Y"
                    : "Z"
            })
            .ToListAsync(ct);

        Print("4. product_abc_xyz", rows);
    }

    // 5. mssql_quarterly_pivot.sql (native PIVOT replaced by conditional SUM(CASE ...))
    public static async Task QuarterlyPivot(IDataContext ctx, CancellationToken ct)
    {
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
            .ToListAsync(ct);

        Print("5. quarterly_pivot", rows);
    }

    // 6. top_products_by_category (course: ROW_NUMBER() top-N per group)
    public static async Task TopProductsByCategory(IDataContext ctx, CancellationToken ct)
    {
        var productRevenue = ctx.From<ISalesOrderDetail>()
            .Join(ctx.From<ISalesOrderHeader>(), (sod, soh) => sod.SalesOrderId == soh.SalesOrderId)
            .Join(ctx.From<IProduct>(), (p, pr) => p.Item1.ProductId == pr.ProductId)
            .Join(ctx.From<IProductSubcategory>(), (p, psc) => p.Item3.ProductSubcategoryId == psc.ProductSubcategoryId)
            .Join(ctx.From<IProductCategory>(), (p, pc) => p.Item4.ProductCategoryId == pc.ProductCategoryId)
            .GroupBy(p => new { Category = p.Item5.Name, p.Item1.ProductId, Product = p.Item3.Name })
            .Select(p => new
            {
                Category = p.Item5.Name,
                p.Item1.ProductId,
                Product = p.Item3.Name,
                Revenue = SqlFunctions.Sql.sum(p.Item1.LineTotal)
            });

        var ranked = ctx.From(productRevenue)
            .Select(p => new
            {
                p.Category,
                p.Product,
                p.Revenue,
                Place = SqlFunctions.Sql.row_number().Over(
                    partitionBy: new Expression<Func<object?>>[] { () => p.Category },
                    orderBy: new[] { SqlFunctions.Sql.desc(() => p.Revenue) })
            });

        var rows = await ctx.From(ranked)
            .Where(p => p.Place <= 3)
            .OrderBy(p => p.Category)
            .OrderBy(p => p.Place)
            .Select(p => new
            {
                p.Category,
                p.Place,
                p.Product,
                Revenue = Math.Round(p.Revenue, 2)
            })
            .ToListAsync(ct);

        Print("6. top_products_by_category", rows);
    }

    // 7. territory_yoy (course: LAG for year-over-year growth)
    public static async Task TerritoryYearOverYear(IDataContext ctx, CancellationToken ct)
    {
        var yearly = ctx.From<ISalesOrderHeader>()
            .Join(ctx.From<ISalesTerritory>(), (soh, st) => soh.TerritoryId == st.TerritoryId)
            .GroupBy(p => new { Region = p.Item2.Name, Year = p.Item1.OrderDate.Year })
            .Select(p => new
            {
                Region = p.Item2.Name,
                Year = p.Item1.OrderDate.Year,
                Revenue = SqlFunctions.Sql.sum(p.Item1.TotalDue)
            });

        var withPrevious = ctx.From(yearly)
            .Select(y => new
            {
                y.Region,
                y.Year,
                y.Revenue,
                PrevRevenue = SqlFunctions.Sql.lag(y.Revenue)
                    .Over(partitionBy: () => y.Region, orderBy: () => y.Year)
            });

        var rows = await ctx.From(withPrevious)
            .OrderBy(y => y.Region)
            .OrderBy(y => y.Year)
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

        Print("7. territory_yoy", rows);
    }

    // 8. customer_rfm (course: NTILE() RFM segmentation)
    public static async Task CustomerRfm(IDataContext ctx, CancellationToken ct)
    {
        var rfm = ctx.From<ISalesOrderHeader>()
            .Join(ctx.From<ICustomer>(), (soh, c) => soh.CustomerId == c.CustomerId)
            .Join(ctx.From<IPerson>(), (p, per) => p.Item2.PersonId == per.BusinessEntityId)
            .GroupBy(p => new { p.Item1.CustomerId, Name = p.Item3.FirstName + " " + p.Item3.LastName })
            .Select(p => new
            {
                p.Item1.CustomerId,
                Name = p.Item3.FirstName + " " + p.Item3.LastName,
                Frequency = SqlFunctions.Sql.count(),
                Monetary = SqlFunctions.Sql.sum(p.Item1.TotalDue),
                Recency = SqlFunctions.Sql.max(p.Item1.OrderDate)
            });

        var scored = ctx.From(rfm)
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

        var rows = await ctx.From(scored)
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

        Print("8. customer_rfm", rows);
    }

    // 9. quota_attainment (course: per-entity target vs actual)
    public static async Task QuotaAttainment(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<ISalesPerson>()
            .Join(ctx.From<IPerson>(), (sp, per) => sp.BusinessEntityId == per.BusinessEntityId)
            .Where(p => p.Item1.SalesQuota != null)
            .OrderByDescending(p => p.Item1.SalesYtd / p.Item1.SalesQuota)
            .Select(p => new
            {
                Name = p.Item2.FirstName + " " + p.Item2.LastName,
                Quota = p.Item1.SalesQuota,
                SalesYtd = p.Item1.SalesYtd,
                AttainmentPct = Math.Round((p.Item1.SalesYtd / p.Item1.SalesQuota * 100) ?? 0m, 1)
            })
            .ToListAsync(ct);

        Print("9. quota_attainment", rows);
    }

    // 10. territory_growth_mom (course: month-over-month % change)
    public static async Task TerritoryGrowthMonthOverMonth(IDataContext ctx, CancellationToken ct)
    {
        var monthly = ctx.From<ISalesOrderHeader>()
            .Join(ctx.From<ISalesTerritory>(), (soh, st) => soh.TerritoryId == st.TerritoryId)
            .GroupBy(p => new { Region = p.Item2.Name, SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.OrderDate) })
            .Select(p => new
            {
                Region = p.Item2.Name,
                SalesMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.OrderDate),
                MonthlyRevenue = SqlFunctions.Sql.sum(p.Item1.TotalDue)
            });

        var withPrevious = ctx.From(monthly)
            .Select(m => new
            {
                m.Region,
                m.SalesMonth,
                m.MonthlyRevenue,
                PrevRevenue = SqlFunctions.Sql.lag(m.MonthlyRevenue)
                    .Over(partitionBy: () => m.Region, orderBy: () => m.SalesMonth)
            });

        var rows = await ctx.From(withPrevious)
            .OrderBy(m => m.Region)
            .OrderBy(m => m.SalesMonth)
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

        Print("10. territory_growth_mom", rows);
    }

    // 11. customer_pareto (course: ABC / Pareto 80-20 concentration)
    public static async Task CustomerPareto(IDataContext ctx, CancellationToken ct)
    {
        var customerRevenue = ctx.From<ISalesOrderHeader>()
            .GroupBy(soh => new { soh.CustomerId })
            .Select(soh => new { soh.CustomerId, Revenue = SqlFunctions.Sql.sum(soh.TotalDue) });

        var ranked = ctx.From(customerRevenue)
            .Select(c => new
            {
                c.CustomerId,
                c.Revenue,
                RunningRevenue = SqlFunctions.Sql.sum_over(c.Revenue).Over(SqlFunctions.Sql.desc(() => c.Revenue)),
                TotalRevenue = SqlFunctions.Sql.sum_over(c.Revenue).Over()
            });

        var rows = await ctx.From(ranked)
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

        Print("11. customer_pareto", rows);
    }

    private static void Print(string title, IEnumerable<object> rows)
    {
        var list = rows.ToList();
        Console.WriteLine($"-- {title}: {list.Count} row(s)");

        foreach (var row in list.Take(10))
            Console.WriteLine("   " + JsonSerializer.Serialize(row));

        if (list.Count > 10)
            Console.WriteLine("   ...");

        Console.WriteLine();
    }
}
