using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models and states whether it is <c>WORKING</c>; when it is not, the comment says why (with the
/// roadmap document that tracks the gap). <c>README.md</c> maps every SQL construct to the nextorm
/// construct that replaces it.
/// </summary>
public static class AdventureWorksQueries
{
    // 1. Sql/mssql_vip_churn.sql
    // WORKING: two CTEs (CustomerOrders, CustomerMetrics) expressed as CTEs; CustomerMetrics groups
    // CustomerOrders by name; the outer query LEFT JOINs it to SalesPerson/Person. FORMAT uses DemoUdf.
    public static async Task VipChurn(IDataContext ctx, CancellationToken ct)
    {
        // CTE CustomerOrders; the manager join stays in the outer SELECT.
        var customerOrders = ctx.From<ISalesOrderHeader>()
            .Join(ctx.From<ICustomer>(), (soh, c) => soh.CustomerId == c.CustomerId)
            .Join(ctx.From<IPerson>(), (p, per) => p.Item2.PersonId == per.BusinessEntityId)
            .Select(p => new
            {
                CustomerID = p.Item1.CustomerId,
                CustomerName = p.Item3.FirstName + " " + p.Item3.LastName,
                SalesOrderID = p.Item1.SalesOrderId,
                OrderDate = p.Item1.OrderDate,
                TotalDue = p.Item1.TotalDue,
                SalesPersonID = p.Item1.SalesPersonId,
                LastCompanyOrderDate = SqlFunctions.Sql.max_over(p.Item1.OrderDate).Over(),
                PrevOrderDate = SqlFunctions.Sql.lag(p.Item1.OrderDate)
                    .Over(partitionBy: () => p.Item1.CustomerId, orderBy: () => p.Item1.OrderDate)
            });

        // CTE CustomerMetrics
        var metrics = ctx.From("CustomerOrders")
            .GroupBy(c => new
            {
                CustomerID = c.GetInt32("CustomerID"),
                CustomerName = c.GetString("CustomerName"),
                SalesPersonID = c.GetNullableInt32("SalesPersonID")
            })
            .Select(c => new
            {
                CustomerID = c.GetInt32("CustomerID"),
                CustomerName = c.GetString("CustomerName"),
                SalesPersonID = c.GetNullableInt32("SalesPersonID"),
                LifetimeValue = SqlFunctions.Sql.sum(c.GetDecimal("TotalDue")),
                LastPurchaseDate = SqlFunctions.Sql.max(c.GetDateTime("OrderDate")),
                DaysSinceLastOrder = SqlFunctions.Sql.date_diff("day",
                    SqlFunctions.Sql.max(c.GetDateTime("OrderDate")),
                    SqlFunctions.Sql.max(c.GetNullableDateTime("LastCompanyOrderDate")))
            });

        var rows = await ctx.With("CustomerOrders", customerOrders).With("CustomerMetrics", metrics)
            .From("CustomerMetrics")
            .LeftJoin(ctx.From<ISalesPerson>(), (m, sp) => m.GetNullableInt32("SalesPersonID") == sp.BusinessEntityId)
            .LeftJoin(ctx.From<IPerson>(), (p, per) => p.Item2.BusinessEntityId == per.BusinessEntityId)
            .Where(p => p.Item1.GetDecimal("LifetimeValue") > 50000 && p.Item1.GetInt32("DaysSinceLastOrder") > 180)
            .OrderByDescending(p => p.Item1.GetDecimal("LifetimeValue"))
            .Select(p => new
            {
                CustomerID = p.Item1.GetInt32("CustomerID"),
                CustomerName = p.Item1.GetString("CustomerName"),
                Ltv = Math.Round(p.Item1.GetDecimal("LifetimeValue"), 2),
                LastPurchase = DemoUdf.Format(p.Item1.GetDateTime("LastPurchaseDate"), "yyyy-MM-dd"),
                DaysSinceLastOrder = p.Item1.GetInt32("DaysSinceLastOrder"),
                Manager = p.Item3.FirstName + " " + p.Item3.LastName
            })
            .ToListAsync(ct);

        Print("1. vip_churn", rows);
    }

    // 2. Sql/mssql_rolling_kpi.sql
    // WORKING: CTE MonthlySales; the outer query reads it by name, formats via DemoUdf and applies
    // cumulative/3-month window frames.
    public static async Task RollingKpi(IDataContext ctx, CancellationToken ct)
    {
        // CTE MonthlySales.
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

        var rows = await ctx.With("MonthlySales", monthly)
            .From("MonthlySales")
            .OrderBy(m => m.GetString("RegionName"))
            .OrderByDescending(m => m.GetDateTime("SalesMonth"))
            .Select(m => new
            {
                Region = m.GetString("RegionName"),
                Month = DemoUdf.Format(m.GetDateTime("SalesMonth"), "yyyy-MM"),
                MonthlyRevenue = Math.Round(m.GetDecimal("MonthlyRevenue"), 2),
                Cumulative = Math.Round(
                    SqlFunctions.Sql.sum_over(m.GetDecimal("MonthlyRevenue")).Over(
                        partitionBy: () => m.GetString("RegionName"),
                        orderBy: () => m.GetDateTime("SalesMonth"),
                        frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow),
                    2),
                MovingAvg3 = Math.Round(
                    SqlFunctions.Sql.avg_over(m.GetDecimal("MonthlyRevenue")).Over(
                        partitionBy: () => m.GetString("RegionName"),
                        orderBy: () => m.GetDateTime("SalesMonth"),
                        frame: WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow)),
                    2)
            })
            .ToListAsync(ct);

        Print("2. rolling_kpi", rows);
    }

    // 3. Sql/mssql_supply_chain.sql
    // WORKING: two CTEs (SupplierDelays, ProductionImpact); the outer query joins them by name.
    public static async Task SupplyChain(IDataContext ctx, CancellationToken ct)
    {
        // CTE SupplierDelays
        var supplierDelays = ctx.From<IPurchaseOrderDetail>()
            .Join(ctx.From<IPurchaseOrderHeader>(), (pod, poh) => pod.PurchaseOrderId == poh.PurchaseOrderId)
            .Join(ctx.From<IVendor>(), (p, v) => p.Item2.VendorId == v.BusinessEntityId)
            .Where(p => p.Item1.ModifiedDate > SqlFunctions.Sql.date_add("day", 5, p.Item1.DueDate))
            .Select(p => new
            {
                ProductID = p.Item1.ProductId,
                VendorID = p.Item2.VendorId,
                VendorName = p.Item3.Name,
                DelayDays = SqlFunctions.Sql.date_diff("day", p.Item1.DueDate, p.Item1.ModifiedDate)
            });

        // CTE ProductionImpact
        var productionImpact = ctx.From<IWorkOrder>()
            .Join(ctx.From<IProduct>(), (wo, pr) => wo.ProductId == pr.ProductId)
            .Where(p => p.Item1.EndDate > p.Item1.DueDate)
            .Select(p => new
            {
                ProductID = p.Item1.ProductId,
                ProductName = p.Item2.Name,
                OrderQty = p.Item1.OrderQty,
                ProdDelayDays = SqlFunctions.Sql.date_diff("day", p.Item1.DueDate, p.Item1.EndDate)
            });

        var rows = await ctx.With("SupplierDelays", supplierDelays).With("ProductionImpact", productionImpact)
            .From("ProductionImpact")
            .Join(ctx.From("SupplierDelays"), (pi, sd) => pi.GetInt32("ProductID") == sd.GetInt32("ProductID"))
            .OrderByDescending(p => p.Item1.GetInt32("ProdDelayDays"))
            .OrderByDescending(p => p.Item2.GetInt32("DelayDays"))
            .Select(p => new
            {
                Component = p.Item1.GetString("ProductName"),
                Vendor = p.Item2.GetString("VendorName"),
                SupplyDelay = p.Item2.GetInt32("DelayDays"),
                ProductionQty = p.Item1.GetInt32("OrderQty"),
                AssemblyShift = p.Item1.GetInt32("ProdDelayDays")
            })
            .ToListAsync(ct);

        Print("3. supply_chain", rows);
    }

    // 4. Sql/mssql_product_abc_xyz.sql
    // WORKING: three chained CTEs (ProductQuarterlySales, ProductAggregates, AbcRanking); the
    // running-share windows and CASE-based ABC/XYZ classes are computed over the CTEs.
    public static async Task ProductAbcXyz(IDataContext ctx, CancellationToken ct)
    {
        // CTE ProductQuarterlySales.
        var quarterly = ctx.From<ISalesOrderDetail>()
            .Join(ctx.From<ISalesOrderHeader>(), (sod, soh) => sod.SalesOrderId == soh.SalesOrderId)
            .Join(ctx.From<IProduct>(), (p, pr) => p.Item1.ProductId == pr.ProductId)
            .GroupBy(p => new
            {
                p.Item1.ProductId,
                ProductName = p.Item3.Name,
                SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item2.OrderDate)
            })
            .Select(p => new
            {
                ProductID = p.Item1.ProductId,
                ProductName = p.Item3.Name,
                SalesQuarter = SqlFunctions.Sql.date_trunc("quarter", p.Item2.OrderDate),
                QuarterlyRevenue = SqlFunctions.Sql.sum(p.Item1.LineTotal),
                QuarterlyQty = SqlFunctions.Sql.sum(p.Item1.OrderQty)
            });

        // CTE ProductAggregates.
        var aggregates = ctx.From("ProductQuarterlySales")
            .GroupBy(q => new { ProductID = q.GetInt32("ProductID"), ProductName = q.GetString("ProductName") })
            .Select(q => new
            {
                ProductID = q.GetInt32("ProductID"),
                ProductName = q.GetString("ProductName"),
                TotalRevenue = SqlFunctions.Sql.sum(q.GetDecimal("QuarterlyRevenue")),
                AvgQty = SqlFunctions.Sql.avg((double)q.GetInt32("QuarterlyQty")),
                StdevQty = SqlFunctions.Sql.stdev((double)q.GetInt32("QuarterlyQty"))
            });

        // CTE AbcRanking.
        var abc = ctx.From("ProductAggregates")
            .Select(a => new
            {
                ProductName = a.GetString("ProductName"),
                TotalRevenue = a.GetDecimal("TotalRevenue"),
                AvgQty = a.GetNullableDouble("AvgQty"),
                StdevQty = a.GetNullableDouble("StdevQty"),
                RunningPercent = SqlFunctions.Sql.sum_over(a.GetDecimal("TotalRevenue"))
                                      .Over(SqlFunctions.Sql.desc(() => (object?)a.GetDecimal("TotalRevenue")))
                                  / SqlFunctions.Sql.sum_over(a.GetDecimal("TotalRevenue")).Over()
            });

        var rows = await ctx.With("ProductQuarterlySales", quarterly)
            .With("ProductAggregates", aggregates)
            .With("AbcRanking", abc)
            .From("AbcRanking")
            .OrderByDescending(a => a.GetDecimal("TotalRevenue"))
            .Select(a => new
            {
                ProductName = a.GetString("ProductName"),
                TotalRevenue = Math.Round(a.GetDecimal("TotalRevenue"), 2),
                Abc = a.GetDecimal("RunningPercent") <= 0.80m ? "A"
                    : a.GetDecimal("RunningPercent") <= 0.95m ? "B"
                    : "C",
                Xyz = a.GetNullableDouble("AvgQty") == 0 || a.GetNullableDouble("StdevQty") == null ? "Z"
                    : a.GetNullableDouble("StdevQty") / a.GetNullableDouble("AvgQty") < 0.15 ? "X"
                    : a.GetNullableDouble("StdevQty") / a.GetNullableDouble("AvgQty") <= 0.30 ? "Y"
                    : "Z"
            })
            .ToListAsync(ct);

        Print("4. product_abc_xyz", rows);
    }

    // 5. Sql/mssql_quarterly_pivot.sql
    // WORKING: a derived query (5-table join with a computed Margin and FOR column) reshaped by the
    // native PIVOT (EntityBuilder.Pivot now accepts a derived source, not only a plain table/entity).
    public static async Task QuarterlyPivot(IDataContext ctx, CancellationToken ct)
    {
        var orderMargins = ctx.From<ISalesOrderDetail>()
            .Join(ctx.From<ISalesOrderHeader>(), (sod, soh) => sod.SalesOrderId == soh.SalesOrderId)
            .Join(ctx.From<IProduct>(), (p, pr) => p.Item1.ProductId == pr.ProductId)
            .Join(ctx.From<IProductSubcategory>(), (p, psc) => p.Item3.ProductSubcategoryId == psc.ProductSubcategoryId)
            .Join(ctx.From<IProductCategory>(), (p, pc) => p.Item4.ProductCategoryId == pc.ProductCategoryId)
            .Where(p => SqlFunctions.Sql.extract("year", p.Item2.OrderDate) == 2013)
            .Select(p => new
            {
                CategoryName = p.Item5.Name,
                QuarterNum = SqlFunctions.Sql.extract("quarter", p.Item2.OrderDate),
                Margin = p.Item1.LineTotal - p.Item3.StandardCost * p.Item1.OrderQty
            });

        var rows = await ctx.From(orderMargins)
            .Pivot(
                PivotAggregate.Sum,
                m => m.Margin,
                m => m.QuarterNum,
                PivotValue.Create("1"), PivotValue.Create("2"), PivotValue.Create("3"), PivotValue.Create("4"))
            .OrderBy(t => t.GetString("CategoryName"))
            .Select(t => new
            {
                Category = t.GetString("CategoryName"),
                Q1 = t.GetNullableDecimal("[1]"),
                Q2 = t.GetNullableDecimal("[2]"),
                Q3 = t.GetNullableDecimal("[3]"),
                Q4 = t.GetNullableDecimal("[4]")
            })
            .ToListAsync(ct);

        Print("5. quarterly_pivot", rows);
    }

    // 6. top_products_by_category
    // WORKING: ROW_NUMBER() OVER (PARTITION BY category ORDER BY revenue DESC) top-N per group.
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

    // 7. territory_yoy
    // WORKING: LAG over a yearly aggregate (year-over-year growth).
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

    // 8. customer_rfm
    // WORKING: NTILE(4) OVER (ORDER BY … DESC) RFM segmentation.
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

    // 9. quota_attainment
    // WORKING: per-entity target vs actual, computed in Select.
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

    // 10. territory_growth_mom
    // WORKING: LAG over a monthly aggregate (month-over-month % change).
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

    // 11. customer_pareto
    // WORKING: ABC / Pareto 80-20 concentration with running window shares.
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
