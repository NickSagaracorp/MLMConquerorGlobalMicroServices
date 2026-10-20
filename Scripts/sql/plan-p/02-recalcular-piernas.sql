/*
Plan P, Tarea 3. Recalcula desde cero los puntos de pierna del binario despues de reparar los huecos
(01-reparar-huecos-binario.sql): suma de abajo arriba, nivel a nivel, de MemberStatistics.PersonalPoints,
la misma semantica que LegPointMaintenance (la pierna de un nodo es la suma de los PersonalPoints de todo
el subarbol de su hijo de ese lado, el hijo incluido). Tambien MemberStatistics.DualTeamPoints = Izq + Der.
No basta la version incremental: los valores guardados salen de algoritmos mezclados.

Vacia DualTeamLegFrontiers (la cola cacheada por patrocinador): la reparacion cambia cual es la hoja mas
profunda de cada pierna. El motor la reconstruye solo, con una consulta, en el primer derrame de cada
patrocinador; la Tarea 14 la reconstruye de una vez.

Escribe por tandas de 20.000 filas, una transaccion por tanda, con 'dual-tree' en exclusivo durante todo el script. Idempotente: una segunda ejecucion no cambia
ninguna fila. Se niega a ejecutarse si quedan huecos repetidos (las piernas no estarian bien definidas).

Plan Q (revision del bloque 2, I-2): neutraliza la cola de piernas. Toma 'dual-leg-deltas' en exclusivo (ningun vaciado
a la vez) y pone a cero los PUNTOS pendientes en DualLegDeltas: ya van dentro de este recalculo y aplicarlos despues los
contaria dos veces. Los contadores y calificados pendientes se conservan (este script no los recalcula).

SOLO base local o la base de ensayo del Plan P, con TODOS los servicios parados, Hangfire incluido: BizCenter (coloca y
vacia la cola de piernas), SignupAPI y AdminAPI (colocan y retiran), Billing (UplineAggregator escribe DualTeamPoints y
PersonalPoints), RankEngine y CommissionEngine (vacian la cola antes de leer). Un escritor en marcha durante el script
deja piernas o DualTeamPoints distintos del recalculo.
Cerrojos: DB_NAME() en lista blanca, -v Confirmo=LOCAL, SERVERPROPERTY('MachineName') = HOST_NAME().

  sqlcmd -S . -d MLMConquerorGlobalEditionDb -E -C -b -I -v Confirmo=LOCAL -i Scripts/sql/plan-p/02-recalcular-piernas.sql
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_NAME() NOT IN (N'MLMConquerorGlobalEditionDb', N'MLMConquerorGlobalEditionDb_PlanPEnsayo')
    THROW 51045, N'Este script es solo para MLMConquerorGlobalEditionDb (o su ensayo del Plan P).', 1;
IF N'$(Confirmo)' <> N'LOCAL'
    THROW 51046, N'Falta la confirmacion explicita: ejecutar con -v Confirmo=LOCAL.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> HOST_NAME()
    THROW 51047, N'El servidor no es la maquina local.', 1;
IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND ParentMemberId IS NOT NULL
           GROUP BY ParentMemberId, Side HAVING COUNT(*) > 1)
    THROW 51052, N'Quedan huecos repetidos: ejecutar antes 01-reparar-huecos-binario.sql.', 1;
-- Plan Q (Tarea 14): el nivel sale de Depth (el binario ya no tiene ruta de texto).
IF EXISTS (SELECT 1 FROM DualTeamTree WHERE IsDeleted = 0 AND Depth IS NULL)
    THROW 51105, N'Hay nodos vivos sin Depth: ejecutar antes Scripts/sql/plan-p/03-relleno-clave-binario.sql.', 1;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @inicio datetime2 = SYSDATETIME();

BEGIN TRY
DECLARE @candado int;
EXEC @candado = sp_getapplock @Resource = N'dual-tree', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
IF @candado < 0
    THROW 51065, N'Hay escrituras del binario en curso (dual-tree ocupado): parar los servicios.', 1;

-- Plan Q (revision I3 y revision del bloque 2, I-2): este script recalcula los PUNTOS de pierna desde cero. Lo pendiente
-- de puntos en la cola ya va dentro del recalculo: se pone a cero (los contadores y calificados pendientes se conservan:
-- este script no los recalcula). Con dual-tree en exclusivo ninguna colocacion encola mientras dura, y con
-- dual-leg-deltas en exclusivo ningun vaciado reclama a la vez.
DECLARE @cola int;
EXEC @cola = sp_getapplock @Resource = N'dual-leg-deltas', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
IF @cola < 0
    THROW 51112, N'La cola de piernas se esta vaciando (dual-leg-deltas ocupado): parar los servicios.', 1;
DECLARE @puntosNeutralizados int = 0;
IF OBJECT_ID(N'dbo.DualLegDeltas') IS NOT NULL
BEGIN
    BEGIN TRAN;
    UPDATE DualLegDeltas SET Points = 0 WHERE Points <> 0;
    SET @puntosNeutralizados = @@ROWCOUNT;
    -- Tarea 8b: un delta con calificados pendientes (o un propio) no se borra: este script no los recalcula.
    DELETE FROM DualLegDeltas WHERE Points = 0 AND NodeCount = 0 AND QualifiedCount = 0 AND OwnChange = 0;
    COMMIT;
END

IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
SELECT CAST(d.MemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS MemberId,
       CAST(d.ParentMemberId AS nvarchar(100)) COLLATE DATABASE_DEFAULT AS ParentMemberId,
       d.Side,
       d.Depth AS Nivel,   -- Plan Q: la profundidad bajo su raiz (antes, las barras de la ruta)
       CAST(ISNULL(s.PersonalPoints, 0) AS decimal(18,4)) AS Propios,
       CAST(0 AS decimal(18,4)) AS Izq, CAST(0 AS decimal(18,4)) AS Der
INTO #n
FROM DualTeamTree d
OUTER APPLY (SELECT TOP 1 ms.PersonalPoints FROM MemberStatistics ms WHERE ms.MemberId = d.MemberId ORDER BY ms.Id) s
WHERE d.IsDeleted = 0;
CREATE UNIQUE CLUSTERED INDEX cx ON #n (Nivel, MemberId);
CREATE INDEX ix_p ON #n (ParentMemberId, Side) INCLUDE (Propios, Izq, Der);

-- De la hoja a la raiz: cada nivel suma los totales ya calculados del nivel de debajo.
DECLARE @nivel int = (SELECT MAX(Nivel) FROM #n);
WHILE @nivel >= 0
BEGIN
    UPDATE p SET Izq = ISNULL(l.Total, 0), Der = ISNULL(r.Total, 0)
    FROM #n p
    OUTER APPLY (SELECT SUM(c.Propios + c.Izq + c.Der) AS Total FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 0) l
    OUTER APPLY (SELECT SUM(c.Propios + c.Izq + c.Der) AS Total FROM #n c WHERE c.ParentMemberId = p.MemberId AND c.Side = 1) r
    WHERE p.Nivel = @nivel;
    SET @nivel -= 1;
END

DECLARE @hecho int = 1, @piernas int = 0;
WHILE @hecho > 0
BEGIN
    BEGIN TRAN;
    UPDATE TOP (20000) d SET LeftLegPoints = n.Izq, RightLegPoints = n.Der, LastUpdateDate = GETDATE(), LastUpdateBy = N'plan-p-piernas'
    FROM DualTeamTree d JOIN #n n ON n.MemberId = d.MemberId
    WHERE d.IsDeleted = 0 AND (d.LeftLegPoints <> n.Izq OR d.RightLegPoints <> n.Der);
    SET @hecho = @@ROWCOUNT;
    SET @piernas += @hecho;
    COMMIT;
END

BEGIN TRAN;
UPDATE s SET DualTeamPoints = CAST(n.Izq + n.Der AS int)
FROM MemberStatistics s JOIN #n n ON n.MemberId = s.MemberId
WHERE s.DualTeamPoints <> CAST(n.Izq + n.Der AS int);
DECLARE @estadisticas int = @@ROWCOUNT;

DELETE FROM DualTeamLegFrontiers;
DECLARE @colas int = @@ROWCOUNT;
COMMIT;

EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';

SELECT (SELECT COUNT(*) FROM #n) AS NodosRecalculados,
       @piernas AS PiernasCambiadas,
       @estadisticas AS DualTeamPointsCambiados,
       @colas AS ColasVaciadas,
       @puntosNeutralizados AS PendientesNeutralizados,
       DATEDIFF(second, @inicio, SYSDATETIME()) AS Segundos;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    IF APPLOCK_MODE(N'public', N'dual-leg-deltas', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-leg-deltas', @LockOwner = N'Session';
    IF APPLOCK_MODE(N'public', N'dual-tree', N'Session') <> N'NoLock'
        EXEC sp_releaseapplock @Resource = N'dual-tree', @LockOwner = N'Session';
    THROW;
END CATCH
