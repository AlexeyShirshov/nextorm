-- 1. Поиск «спящих» VIP-клиентов и упущенной выгоды (T-SQL CTE + Window Functions)
WITH CustomerOrders AS (
    SELECT 
        soh.CustomerID,
        p.FirstName + ' ' + p.LastName AS CustomerName,
        soh.SalesOrderID,
        soh.OrderDate,
        soh.TotalDue,
        soh.SalesPersonID,
        MAX(soh.OrderDate) OVER() AS LastCompanyOrderDate,
        LAG(soh.OrderDate) OVER(PARTITION BY soh.CustomerID ORDER BY soh.OrderDate) AS PrevOrderDate
    FROM Sales.SalesOrderHeader soh
    JOIN Sales.Customer c ON soh.CustomerID = c.CustomerID
    JOIN Person.Person p ON c.PersonID = p.BusinessEntityID
),
CustomerMetrics AS (
    SELECT 
        CustomerID,
        CustomerName,
        SalesPersonID,
        SUM(TotalDue) AS LifetimeValue,
        MAX(OrderDate) AS LastPurchaseDate,
        DATEDIFF(day, MAX(OrderDate), MAX(LastCompanyOrderDate)) AS DaysSinceLastOrder
    FROM CustomerOrders
    GROUP BY CustomerID, CustomerName, SalesPersonID
)
SELECT 
    cm.CustomerID AS [ID Клиента],
    cm.CustomerName AS [Имя Клиента],
    ROUND(cm.LifetimeValue, 2) AS [LTV (USD)],
    FORMAT(cm.LastPurchaseDate, 'yyyy-MM-dd') AS [Последний заказ],
    cm.DaysSinceLastOrder AS [Дней в оттоке],
    sp_p.FirstName + ' ' + sp_p.LastName AS [Ответственный менеджер]
FROM CustomerMetrics cm
LEFT JOIN Sales.SalesPerson sp ON cm.SalesPersonID = sp.BusinessEntityID
LEFT JOIN Person.Person sp_p ON sp.BusinessEntityID = sp_p.BusinessEntityID
WHERE cm.LifetimeValue > 50000 
  AND cm.DaysSinceLastOrder > 180
ORDER BY cm.LifetimeValue DESC;