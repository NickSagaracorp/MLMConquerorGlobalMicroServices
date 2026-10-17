/*
Plan M, Tarea 4 (M4). Renumera los pedidos que repiten OrderNo: el mas antiguo de cada grupo conserva
el numero y los demas reciben "-D1", "-D2"... Idempotente: si no hay duplicados no cambia nada.
SOLO para la base local MLMConquerorGlobalEditionDb. En produccion NO se ejecuta sin decision de Nick (R15).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51029, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;

DECLARE @Renumerados int;

BEGIN TRAN;

;WITH d AS (
    SELECT o.Id, o.OrderNo,
           ROW_NUMBER() OVER (PARTITION BY o.OrderNo ORDER BY o.CreationDate, o.Id) AS rn
    FROM Orders o
    WHERE o.OrderNo IN (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL
                        GROUP BY OrderNo HAVING COUNT(*) > 1)
)
UPDATE o
SET    o.OrderNo        = d.OrderNo + N'-D' + CAST(d.rn - 1 AS nvarchar(10)),
       o.LastUpdateDate = GETDATE(),           -- hora de servidor
       o.LastUpdateBy   = N'plan-m-duplicados'
FROM   Orders o
JOIN   d ON d.Id = o.Id
WHERE  d.rn > 1;

SET @Renumerados = @@ROWCOUNT;
COMMIT;

PRINT CONCAT(N'Pedidos renumerados: ', @Renumerados);

SELECT COUNT(*) AS GruposDuplicadosRestantes
FROM (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL GROUP BY OrderNo HAVING COUNT(*) > 1) x;
