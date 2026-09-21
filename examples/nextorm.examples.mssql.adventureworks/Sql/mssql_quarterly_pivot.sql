-- 5. Кросс-таблица маржинальности категорий по кварталам (Нативный T-SQL PIVOT)
WITH OrderMargins AS (
    SELECT 
        pc.[Name] AS CategoryName,
        'Q' + CAST(DATEPART(quarter, soh.OrderDate) AS VARCHAR(1)) AS QuarterNum,
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
    ROUND([Q1], 2) AS [Q1 2013 ($)],
    ROUND([Q2], 2) AS [Q2 2013 ($)],
    ROUND([Q3], 2) AS [Q3 2013 ($)],
    ROUND([Q4], 2) AS [Q4 2013 ($)]
FROM OrderMargins
PIVOT (
    SUM(Margin) 
    FOR QuarterNum IN ([Q1], [Q2], [Q3], [Q4])
) AS PivotTable
ORDER BY CategoryName;