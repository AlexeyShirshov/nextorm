-- 3. Анализ задержек в цепочке поставок (Производство vs Снабжение)
WITH SupplierDelays AS (
    SELECT 
        pod.ProductID,
        poh.VendorID,
        v.[Name] AS VendorName,
        DATEDIFF(day, pod.DueDate, pod.ModifiedDate) AS DelayDays
    FROM Purchasing.PurchaseOrderDetail pod
    JOIN Purchasing.PurchaseOrderHeader poh ON pod.PurchaseOrderID = poh.PurchaseOrderID
    JOIN Purchasing.Vendor v ON poh.VendorID = v.BusinessEntityID
    WHERE pod.ModifiedDate > DATEADD(day, 5, pod.DueDate)
),
ProductionImpact AS (
    SELECT 
        wo.ProductID,
        p.[Name] AS ProductName,
        wo.OrderQty,
        DATEDIFF(day, wo.DueDate, wo.EndDate) AS ProdDelayDays
    FROM Production.WorkOrder wo
    JOIN Production.Product p ON wo.ProductID = p.ProductID
    WHERE wo.EndDate > wo.DueDate
)
SELECT 
    pi.ProductName AS [Компонент],
    sd.VendorName AS [Проблемный поставщик],
    sd.DelayDays AS [Задержка поставки (дней)],
    pi.OrderQty AS [Объем производства],
    pi.ProdDelayDays AS [Сдвиг сборки (дней)]
FROM ProductionImpact pi
JOIN SupplierDelays sd ON pi.ProductID = sd.ProductID
ORDER BY pi.ProdDelayDays DESC, sd.DelayDays DESC;