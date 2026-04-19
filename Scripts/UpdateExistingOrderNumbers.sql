-- Script: Assign OrderNo to existing orders that have no OrderNo yet.
-- Format for legacy orders: ORD + 6-digit zero-padded sequence (by OrderDate ASC).
-- Run once against each environment after deploying the migration.

;WITH Numbered AS (
    SELECT
        Id,
        ROW_NUMBER() OVER (ORDER BY OrderDate ASC, Id ASC) AS RowNum
    FROM Orders
    WHERE OrderNo IS NULL OR OrderNo = ''
)
UPDATE o
SET o.OrderNo = 'ORD' + RIGHT('000000' + CAST(n.RowNum AS VARCHAR(6)), 6)
FROM Orders o
INNER JOIN Numbered n ON o.Id = n.Id;

-- Verify
SELECT Id, OrderNo, OrderDate, Notes
FROM Orders
ORDER BY OrderDate;
