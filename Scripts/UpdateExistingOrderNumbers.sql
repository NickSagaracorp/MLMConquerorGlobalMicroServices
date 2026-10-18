-- ============================================================================================
-- AVISO (Plan M, 2026-10-17): desde la migracion PedidosAcotadosEIndices, Orders.OrderNo es
-- nvarchar(40) con indice UNICO filtrado (IX_Orders_OrderNo, OrderNo IS NOT NULL).
--   * Este script numera desde ORD000001 sin mirar los ORD###### que ya existen: volver a
--     ejecutarlo chocaria con ellos y el UPDATE entero fallaria (2601). Hoy no hay pedidos sin
--     numero (0 nulos y 0 vacios en local), asi que no hace falta.
--   * Si algun dia hiciera falta, hay que partir del mayor ORD existente y ejecutarlo con
--     sqlcmd -I (QUOTED_IDENTIFIER ON): sin eso, un UPDATE de Orders falla con 1934 por el
--     indice filtrado.
--   * Los duplicados se tratan con Scripts/sql/plan-m/01-orderno-duplicados.sql (solo local).
-- ============================================================================================
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
