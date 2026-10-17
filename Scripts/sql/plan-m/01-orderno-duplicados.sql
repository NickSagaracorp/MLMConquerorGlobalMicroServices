/*
Plan M, Tarea 4 (M4). Renumera los pedidos que repiten OrderNo: el mas antiguo de cada grupo conserva
el numero y los demas reciben "-D1", "-D2"... Si un sufijo ya esta ocupado se prueba el siguiente.
Idempotente: si no hay duplicados no cambia nada.

SOLO para la base local MLMConquerorGlobalEditionDb. En produccion NO se ejecuta sin decision de Nick (R15):
alli la migracion PedidosAcotadosEIndices se detiene con 51021 si hay duplicados.

Tres cerrojos, los tres terminan en THROW sin tocar nada:
  1. La base se llama MLMConquerorGlobalEditionDb.
  2. Se pasa la variable explicita -v Confirmo=LOCAL (sin ella, sqlcmd se niega a ejecutar el script).
  3. El servidor es la misma maquina que lo ejecuta: SERVERPROPERTY('MachineName') = HOST_NAME().

Como ejecutarlo (desde la raiz del repositorio, en la maquina del servidor local):
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-m/01-orderno-duplicados.sql
*/
-- sqlcmd arranca con QUOTED_IDENTIFIER OFF, y con el indice filtrado IX_Orders_OrderNo un UPDATE de
-- Orders falla con 1934. Va en su propio lote porque se aplica al compilar.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Los cerrojos van en un lote aparte: con -b, un THROW aqui detiene sqlcmd antes del lote que escribe.
IF DB_NAME() <> N'MLMConquerorGlobalEditionDb'
    THROW 51029, N'Este script es solo para MLMConquerorGlobalEditionDb.', 1;

IF N'$(Confirmo)' <> N'LOCAL'
    THROW 51030, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL.', 1;

IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51031, N'El servidor no es la maquina local: este script solo se ejecuta contra el SQL Server de la misma maquina.', 1;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Renumerados int = 0;

BEGIN TRAN;

-- Los que hay que renumerar: todos menos el mas antiguo de cada grupo.
DECLARE @pendientes TABLE (Id nvarchar(450) PRIMARY KEY, OrderNo nvarchar(40) NOT NULL, rn int NOT NULL);

INSERT INTO @pendientes (Id, OrderNo, rn)
SELECT d.Id, d.OrderNo, d.rn
FROM (
    SELECT o.Id, o.OrderNo,
           ROW_NUMBER() OVER (PARTITION BY o.OrderNo ORDER BY o.CreationDate, o.Id) AS rn
    FROM Orders o WITH (UPDLOCK, HOLDLOCK)
    WHERE o.OrderNo IN (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL
                        GROUP BY OrderNo HAVING COUNT(*) > 1)
) d
WHERE d.rn > 1;

DECLARE @Id nvarchar(450), @Base nvarchar(40), @n int, @Nuevo nvarchar(60);

WHILE EXISTS (SELECT 1 FROM @pendientes)
BEGIN
    SELECT TOP (1) @Id = Id, @Base = OrderNo, @n = rn - 1 FROM @pendientes ORDER BY OrderNo, rn;

    -- El primer sufijo libre a partir de -D(rn-1): ni otro pedido ni uno renumerado en esta pasada lo usa.
    SET @Nuevo = @Base + N'-D' + CAST(@n AS nvarchar(10));
    WHILE EXISTS (SELECT 1 FROM Orders WHERE OrderNo = @Nuevo)
    BEGIN
        SET @n += 1;
        SET @Nuevo = @Base + N'-D' + CAST(@n AS nvarchar(10));
    END;

    IF LEN(@Nuevo) > 40
        THROW 51032, N'El numero renumerado no cabe en Orders.OrderNo (40): decidir a mano.', 1;

    UPDATE Orders
    SET    OrderNo        = @Nuevo,
           LastUpdateDate = GETDATE(),           -- hora de servidor
           LastUpdateBy   = N'plan-m-duplicados'
    WHERE  Id = @Id;

    SET @Renumerados += 1;
    DELETE FROM @pendientes WHERE Id = @Id;
END;

COMMIT;

PRINT CONCAT(N'Pedidos renumerados: ', @Renumerados);

SELECT COUNT(*) AS GruposDuplicadosRestantes
FROM (SELECT OrderNo FROM Orders WHERE OrderNo IS NOT NULL GROUP BY OrderNo HAVING COUNT(*) > 1) x;
