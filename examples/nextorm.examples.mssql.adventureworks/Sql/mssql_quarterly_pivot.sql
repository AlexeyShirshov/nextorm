-- 5. Кросс-таблица маржинальности категорий по кварталам (Нативный T-SQL PIVOT)
-- Значения PIVOT объявлены в типизированной проекции (Q1..Q4) и выводятся в список IN один раз.
WITH OrderMargins AS (
    SELECT 
        pc.[Name] AS CategoryName,
        DATEPART(quarter, soh.OrderDate) AS QuarterNum,
        (sod.LineTotal - (p.StandardCost * sod.OrderQty)) AS Margin
    FROM Sales.SalesOrderDetail sod
    JOIN Sales.SalesOrderHeader soh ON sod.SalesOrderID = soh.SalesOrderID
    JOIN Production.Product p ON sod.ProductID = p.ProductID
    JOIN Production.ProductSubcategory psc ON p.ProductSubcategoryID = psc.ProductSubcategoryID
    JOIN Production.ProductCategory pc ON psc.ProductCategoryID = pc.ProductCategoryID
    WHERE DATEPART(year, soh.OrderDate) = 2013
)
SELECT 
    CategoryName AS [Категория товара],
    ROUND([1], 2) AS [Q1 2013 ($)],
    ROUND([2], 2) AS [Q2 2013 ($)],
    ROUND([3], 2) AS [Q3 2013 ($)],
    ROUND([4], 2) AS [Q4 2013 ($)]
FROM OrderMargins
PIVOT (
    SUM(Margin) 
    FOR QuarterNum IN ([1], [2], [3], [4])
) AS PivotTable
ORDER BY CategoryName;