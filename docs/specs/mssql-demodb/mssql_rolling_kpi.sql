-- 2. Скользящие KPI продаж и кумулятивный доход (T-SQL Window Frames ROWS)
WITH MonthlySales AS (
    SELECT 
        st.[Name] AS RegionName,
        DATEADD(month, DATEDIFF(month, 0, soh.OrderDate), 0) AS SalesMonth,
        SUM(soh.TotalDue) AS MonthlyRevenue
    FROM Sales.SalesOrderHeader soh
    JOIN Sales.SalesTerritory st ON soh.TerritoryID = st.TerritoryID
    GROUP BY st.[Name], DATEADD(month, DATEDIFF(month, 0, soh.OrderDate), 0)
)
SELECT 
    RegionName AS [Регион],
    FORMAT(SalesMonth, 'yyyy-MM') AS [Месяц],
    ROUND(MonthlyRevenue, 2) AS [Выручка за месяц],
    ROUND(SUM(MonthlyRevenue) OVER (
        PARTITION BY RegionName 
        ORDER BY SalesMonth 
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ), 2) AS [Кумулятивная выручка],
    ROUND(AVG(MonthlyRevenue) OVER (
        PARTITION BY RegionName 
        ORDER BY SalesMonth 
        ROWS BETWEEN 2 PRECEDING AND CURRENT ROW
    ), 2) AS [3-мес скользящая средняя]
FROM MonthlySales
ORDER BY RegionName, SalesMonth DESC;