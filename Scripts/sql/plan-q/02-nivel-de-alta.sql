/*
Plan Q, Tarea 25 (DQ10). Rellena MemberProfiles.SignupMembershipLevelId con la regla N2 del Plan J (SignupMembershipLevel):
el nivel mas alto del primer pedido del miembro con productos de membresia (por OrderDate y CreationDate), sin filtrar por
estado del pedido. Por lotes de 20.000 perfiles en orden de Id, cada lote en su transaccion. Idempotente: solo toca las
filas con SignupMembershipLevelId IS NULL. Va despues de la migracion BusquedaYNivelDeAlta. Se puede ejecutar con los
servicios en marcha: el codigo nuevo lo escribe en el alta y la activacion, y una fila que ya lo tiene no se toca.
  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-q/02-nivel-de-alta.sql
*/
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
DECLARE @ultimo nvarchar(450) = N'', @n int = 1, @total int = 0;
CREATE TABLE #ids (Id nvarchar(450) COLLATE DATABASE_DEFAULT PRIMARY KEY, MemberId nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL);
WHILE @n > 0
BEGIN
    TRUNCATE TABLE #ids;
    INSERT INTO #ids SELECT TOP (20000) Id, MemberId FROM MemberProfiles WHERE Id > @ultimo ORDER BY Id;
    SET @n = @@ROWCOUNT;
    IF @n = 0 BREAK;
    SELECT @ultimo = MAX(Id) FROM #ids;
    BEGIN TRAN;
    UPDATE m SET SignupMembershipLevelId = x.Nivel
    FROM MemberProfiles m
    JOIN #ids i ON i.Id = m.Id
    CROSS APPLY (SELECT TOP 1 MAX(p.MembershipLevelId) AS Nivel
                 FROM Orders o
                 JOIN OrderDetails od ON od.OrderId = o.Id
                 JOIN Products p ON p.Id = od.ProductId AND p.IsDeleted = 0
                 WHERE o.MemberId = m.MemberId AND p.MembershipLevelId IS NOT NULL
                 GROUP BY o.Id, o.OrderDate, o.CreationDate
                 ORDER BY o.OrderDate, o.CreationDate) x
    WHERE m.SignupMembershipLevelId IS NULL;
    SET @total += @@ROWCOUNT;
    COMMIT;
END
DECLARE @msg nvarchar(200) = CONCAT(N'Perfiles con nivel de alta asignado: ', @total,
    N' (', DATEDIFF(second, @inicio, SYSDATETIME()), N' s)');
RAISERROR(@msg, 0, 1) WITH NOWAIT;
