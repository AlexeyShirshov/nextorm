using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;
using NextORM.Generated.nextorm_examples_mssql_adventureworks;
using NextORM.SqlServer;

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
    // WORKING: two typed CTEs (`AsCte`); the outer query names the root (`WithAlias(Alias.Root)`) and both
    // LEFT JOINs (`Alias.SalesPerson` / `Alias.Manager`). The root projects the named `CustomerMetricRow`
    // DTO because the alias surface cannot render the chain's anonymous type. FORMAT uses DemoUdf.
    public static async Task VipChurn(IDataContext ctx, CancellationToken ct)
    {
        // CTE CustomerOrders; the manager join stays in the outer SELECT. AsCte makes it a typed
        // descriptor (member access) instead of reading it by name through TableAlias.
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
            })
            .AsCte("CustomerOrders");

        // CTE CustomerMetrics; reads the typed CustomerOrders descriptor, so the step is typed too.
        var metrics = ctx.From(customerOrders)
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
        var rows = await ctx.From(metrics).WithAlias(Alias.Metrics)
            .LeftJoin<ISalesPerson>(ctx.From<ISalesPerson>(), (m, sp) => m.Metrics.SalesPersonID == sp.BusinessEntityId, Alias.SalesPerson)
            .LeftJoin<IPerson>(ctx.From<IPerson>(), (p, per) => p.SalesPerson.BusinessEntityId == per.BusinessEntityId, Alias.Manager)
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

        Print("1. vip_churn", rows);
    }

    // 2. Sql/mssql_rolling_kpi.sql
    // WORKING: typed CTE MonthlySales (`AsCte`); the outer query reads it with member access, formats via
    // DemoUdf and applies cumulative/3-month window frames.
    public static async Task RollingKpi(IDataContext ctx, CancellationToken ct)
    {
        // CTE MonthlySales; AsCte makes it a typed descriptor (member access) instead of reading it by name.
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
            })
            .AsCte("MonthlySales");

        var rows = await ctx.From(monthly)
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
                        frame: WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.CurrentRow)),
                    2)
            })
            .ToListAsync(ct);

        Print("2. rolling_kpi", rows);
    }

    // 3. Sql/mssql_supply_chain.sql
    // WORKING: two typed CTEs (`AsCte`: SupplierDelays, ProductionImpact); the outer query joins them and
    // reads each side with member access.
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
            })
            .AsCte("SupplierDelays");

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
            })
            .AsCte("ProductionImpact");

        var rows = await ctx.From(productionImpact)
            .Join(ctx.From(supplierDelays), (pi, sd) => pi.ProductID == sd.ProductID)
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

    // 4. Sql/mssql_product_abc_xyz.sql
    // WORKING: three chained typed CTEs (`AsCte`: ProductQuarterlySales, ProductAggregates, AbcRanking); the
    // running-share windows and CASE-based ABC/XYZ classes are computed over the CTEs. `avg`/`stdev` are
    // projected as nullable (`(double?)`) so the one-row-per-group case stays distinguishable from zero.
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
            })
            .AsCte("ProductQuarterlySales");

        // CTE ProductAggregates.
        var aggregates = ctx.From(quarterly)
            .GroupBy(q => new { q.ProductID, q.ProductName })
            .Select(q => new
            {
                q.ProductID,
                q.ProductName,
                TotalRevenue = SqlFunctions.Sql.sum(q.QuarterlyRevenue),
                AvgQty = (double?)SqlFunctions.Sql.avg((double)q.QuarterlyQty),
                StdevQty = (double?)SqlFunctions.Sql.stdev((double)q.QuarterlyQty)
            })
            .AsCte("ProductAggregates");

        // CTE AbcRanking.
        var abc = ctx.From(aggregates)
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

        var rows = await ctx.From(abc)
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

        Print("4. product_abc_xyz", rows);
    }

    // 5. Sql/mssql_quarterly_pivot.sql
    // WORKING: a derived query (5-table join with a computed Margin and FOR column) reshaped by the
    // native PIVOT. The typed overload declares the result members (the grouping category and the four
    // quarter cells) and each pivot value once, deriving the IN list from the projection.
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
