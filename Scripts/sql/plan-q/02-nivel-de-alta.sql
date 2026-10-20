/*
Plan Q, Tarea 25 (DQ10). Rellena MemberProfiles.SignupMembershipLevelId con la regla N2 del Plan J (SignupMembershipLevel):
el nivel mas alto del primer pedido del miembro con productos de membresia (por OrderDate, CreationDate e Id), sin filtrar
por estado del pedido. Idempotente: solo toca las filas con SignupMembershipLevelId IS NULL. Va despues de la migracion
BusquedaYNivelDeAlta. Se puede ejecutar con los servicios en marcha: el codigo nuevo lo escribe en el alta y la
activacion, y una fila que ya lo tiene no se toca.
Revision del bloque 5 (I6): el nivel de cada perfil se calcula una sola vez, fuera de cualquier transaccion, en una tabla
temporal; despues se escribe por lotes de 4.000 (por debajo del umbral de escalado de candados), cada uno en su
transaccion corta. Antes, cada lote de 20.000 calculaba y escribia dentro de la misma transaccion.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/02-nivel-de-alta.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(Confirmo)' NOT IN (N'LOCAL', N'ESCALA')
    THROW 51109, N'Ejecutar con -v Confirmo=LOCAL (o ESCALA en la base de escala).', 1;
IF NOT ((N'$(Confirmo)' = N'LOCAL'  AND DB_NAME() = N'MLMConquerorGlobalEditionDb')
     OR (N'$(Confirmo)' = N'ESCALA' AND DB_NAME() = N'MLMConquerorGlobalEditionDb_Escala'))
    THROW 51110, N'Confirmo no corresponde a esta base.', 1;
IF COL_LENGTH(N'dbo.MemberProfiles', N'SignupMembershipLevelId') IS NULL
    THROW 51111, N'Falta la migracion BusquedaYNivelDeAlta.', 1;

DECLARE @inicio datetime2 = SYSDATETIME();

-- Una pasada, sin transaccion: el nivel de los perfiles que aun no lo tienen y si tienen pedido de membresia.
CREATE TABLE #niveles (Id nvarchar(450) COLLATE DATABASE_DEFAULT PRIMARY KEY, Nivel int NOT NULL);
INSERT INTO #niveles (Id, Nivel)
SELECT m.Id, x.Nivel
FROM MemberProfiles m
CROSS APPLY (SELECT TOP 1 MAX(p.MembershipLevelId) AS Nivel
             FROM Orders o
             JOIN OrderDetails od ON od.OrderId = o.Id
             JOIN Products p ON p.Id = od.ProductId AND p.IsDeleted = 0
             WHERE o.MemberId = m.MemberId AND p.MembershipLevelId IS NOT NULL
             GROUP BY o.Id, o.OrderDate, o.CreationDate
             ORDER BY o.OrderDate, o.CreationDate, o.Id) x
WHERE m.SignupMembershipLevelId IS NULL;
DECLARE @calculados int = @@ROWCOUNT;
DECLARE @msg nvarchar(200) = CONCAT(N'Perfiles con nivel calculado: ', @calculados,
    N' (', DATEDIFF(second, @inicio, SYSDATETIME()), N' s)');
RAISERROR(@msg, 0, 1) WITH NOWAIT;

-- Por lotes de 4.000 en orden de Id, cada uno en su transaccion. Una fila que el alta ya relleno no se toca.
CREATE TABLE #lote (Id nvarchar(450) COLLATE DATABASE_DEFAULT PRIMARY KEY, Nivel int NOT NULL);
DECLARE @ultimo nvarchar(450) = N'', @n int = 1, @total int = 0;
WHILE 1 = 1
BEGIN
    TRUNCATE TABLE #lote;
    INSERT INTO #lote (Id, Nivel) SELECT TOP (4000) Id, Nivel FROM #niveles WHERE Id > @ultimo ORDER BY Id;
    SET @n = @@ROWCOUNT;
    IF @n = 0 BREAK;
    SELECT @ultimo = MAX(Id) FROM #lote;
    BEGIN TRAN;
    UPDATE m SET SignupMembershipLevelId = l.Nivel
    FROM MemberProfiles m
    JOIN #lote l ON l.Id = m.Id
    WHERE m.SignupMembershipLevelId IS NULL;
    SET @total += @@ROWCOUNT;
    COMMIT;
END
SET @msg = CONCAT(N'Perfiles con nivel de alta asignado: ', @total,
    N' (', DATEDIFF(second, @inicio, SYSDATETIME()), N' s)');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
