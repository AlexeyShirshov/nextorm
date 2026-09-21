-- 4. Комплексный ABC/XYZ-анализ продуктовой матрицы (T-SQL Статистика)
WITH ProductQuarterlySales AS (
    SELECT 
        sod.ProductID,
        p.[Name] AS ProductName,
        DATEADD(quarter, DATEDIFF(quarter, 0, soh.OrderDate), 0) AS SalesQuarter,
        SUM(sod.LineTotal) AS QuarterlyRevenue,
        SUM(sod.OrderQty) AS QuarterlyQty
    FROM Sales.SalesOrderDetail sod
    JOIN Sales.SalesOrderHeader soh ON sod.SalesOrderID = soh.SalesOrderID
    JOIN Production.Product p ON sod.ProductID = p.ProductID
    GROUP BY sod.ProductID, p.[Name], DATEADD(quarter, DATEDIFF(quarter, 0, soh.OrderDate), 0)
),
ProductAggregates AS (
    SELECT 
        ProductID,
        ProductName,
        SUM(QuarterlyRevenue) AS TotalRevenue,
        AVG(CAST(QuarterlyQty AS FLOAT)) AS AvgQty,
        STDEV(QuarterlyQty) AS StdevQty
    FROM ProductQuarterlySales
    GROUP BY ProductID, ProductName
),
AbcRanking AS (
    SELECT 
        *,
        SUM(TotalRevenue) OVER(ORDER BY TotalRevenue DESC) / SUM(TotalRevenue) OVER() AS RunningPercent
    FROM ProductAggregates
)
SELECT 
    ProductName AS [Наименование товара],
    ROUND(TotalRevenue, 2) AS [Общая выручка ($)],
    CASE 
        WHEN RunningPercent <= 0.80 THEN 'A'
        WHEN RunningPercent <= 0.95 THEN 'B'
        ELSE 'C'
    END AS [Класс ABC (Выручка)],
    CASE 
        WHEN AvgQty = 0 OR StdevQty IS NULL THEN 'Z'
        WHEN (StdevQty / AvgQty) < 0.15 THEN 'X'
        WHEN (StdevQty / AvgQty) BETWEEN 0.15 AND 0.30 THEN 'Y'
        ELSE 'Z'
    END AS [Класс XYZ (Стабильность)]
FROM AbcRanking
ORDER BY TotalRevenue DESC;